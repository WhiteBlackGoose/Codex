using Codex.ObjectModel;
using Codex.Storage.DataModel;
using Codex.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using static Codex.Utilities.SerializationUtilities;

namespace Codex.ElasticSearch.Store
{
    public class StoredBoundSourceFile : EntityBase
    {
        public List<string> SourceFileContentLines { get; set; }

        public BoundSourceFile BoundSourceFile { get; set; }

        public ReferenceListModel CompressedReferences { get; set; }

        public ClassificationListModel CompressedClassifications { get; set; }

        public void BeforeSerialize(bool optimize, bool optimizeLineInfo = true)
        {
            PopulateSourceFileLines();

            if (optimize)
            {
                string projectId = this.BoundSourceFile.ProjectId;
                string containerQualifiedName = null;
                string kind = null;
                Glyph glyph = default(Glyph);
                foreach (var definitionSpan in this.BoundSourceFile.Definitions)
                {
                    var definition = definitionSpan.Definition;
                    definition.ProjectId = RemoveDuplicate(definition.ProjectId, ref projectId);
                    definition.Kind = RemoveDuplicate(definition.Kind, ref kind);
                    definition.ContainerQualifiedName = RemoveDuplicate(definition.ContainerQualifiedName, ref containerQualifiedName);
                    definition.Glyph = RemoveDuplicate(definition.Glyph, ref glyph);
                    definition.ReferenceKind = null;
                }

                CompressedClassifications = ClassificationListModel.CreateFrom(BoundSourceFile.Classifications);
                CompressedReferences = ReferenceListModel.CreateFrom(BoundSourceFile.References, includeLineInfo: true, externalLineTextPersistence: optimizeLineInfo);
                BoundSourceFile.References = CollectionUtilities.Empty<ReferenceSpan>.Array;
                BoundSourceFile.Classifications = CollectionUtilities.Empty<ClassificationSpan>.Array;
            }
        }

        private void PopulateSourceFileLines()
        {
            var content = this.BoundSourceFile.SourceFile.Content;
            if (content == null)
            {
                return;
            }

            this.BoundSourceFile.SourceFile.Content = null;

            SourceFileContentLines = new List<string>(content.GetLines(includeLineBreak: true));

            Debug.Assert(SourceFileContentLines.Sum(l => l.Length) == content.Length);
        }

        public void AfterDeserialization()
        {
            if (CompressedClassifications != null)
            {
                BoundSourceFile.Classifications = CompressedClassifications.ToList();
            }

            if (CompressedReferences != null)
            {
                if (SourceFileContentLines != null && SourceFileContentLines.Count != 0)
                {
                    ResplitLines();

                    var lineSpans = new List<SymbolSpan>();
                    var lineSpanStart = 0;
                    for (int i = 0; i < SourceFileContentLines.Count; i++)
                    {
                        var lineSpanText = SourceFileContentLines[i];
                        var lineSpan = new SymbolSpan()
                        {
                            // LineSpanStart is modified by Trim(), but floored at zero
                            // This is only set to recompute the start value so we set it to max
                            // value and then use the changed amount after trim to compute start
                            LineSpanStart = int.MaxValue,
                            LineSpanText = lineSpanText,
                        };

                        lineSpan.Trim();
                        lineSpan.Start = lineSpanStart + (int.MaxValue - lineSpan.LineSpanStart);
                        lineSpans.Add(lineSpan);

                        lineSpanStart += lineSpanText.Length;
                    }

                    foreach (var span in CompressedReferences.LineSpanModel.SharedValues)
                    {
                        var lineSpan = lineSpans[span.LineIndex];
                        span.LineSpanText = lineSpan.LineSpanText;
                        span.Start = lineSpan.Start;
                    }
                }

                BoundSourceFile.References = CompressedReferences.ToList();
            }

            string projectId = this.BoundSourceFile.ProjectId;
            string containerQualifiedName = null;
            string kind = null;
            Glyph glyph = default(Glyph);
            foreach (var definitionSpan in this.BoundSourceFile.Definitions)
            {
                var definition = definitionSpan.Definition;
                definition.ProjectId = AssignDuplicate(definition.ProjectId, ref projectId);
                definition.Kind = AssignDuplicate(definition.Kind, ref kind);
                definition.ContainerQualifiedName = AssignDuplicate(definition.ContainerQualifiedName, ref containerQualifiedName);
                definition.Glyph = AssignDuplicate(definition.Glyph, ref glyph);
                definition.ReferenceKind = nameof(ReferenceKind.Definition);
            }

            if (this.BoundSourceFile.SourceFile.Content == null && SourceFileContentLines != null)
            {
                this.BoundSourceFile.SourceFile.Content = string.Join(string.Empty, SourceFileContentLines);
            }
        }

        private void ResplitLines()
        {
            bool needsResplit = false;
            foreach (var line in SourceFileContentLines)
            {
                if (line.Length == 0) continue;

                int index = line.IndexOf('\r');
                if (index == -1) continue;

                // Line with just carriage return
                if (line.Length == 1) continue;

                if ((index < line.Length - 2) || 
                    (index == line.Length - 2 && line[line.Length - 1] != '\n'))
                {
                    needsResplit = true;
                }
            }

            if (!needsResplit) return;

            SourceFileContentLines = string.Join(string.Empty, SourceFileContentLines)
                                    .GetLines(includeLineBreak: true).ToList();
        }
    }
}
