﻿using Codex.ObjectModel;
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

                CompressedClassifications = new ClassificationListModel(BoundSourceFile.Classifications);
                CompressedReferences = new ReferenceListModel(BoundSourceFile.References, includeLineInfo: true, externalLineTextPersistence: optimizeLineInfo);
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

            SourceFileContentLines = new List<string>();

            int startIndex = 0;
            while (true)
            {
                var newLineIndex = content.IndexOf('\n', startIndex);
                if (newLineIndex < 0)
                {
                    if (startIndex < content.Length)
                    {
                        SourceFileContentLines.Add(content.Substring(startIndex));
                    }

                    break;
                }

                SourceFileContentLines.Add(content.Substring(startIndex, newLineIndex - startIndex + 1));
                startIndex = newLineIndex + 1;
            }

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

    }
}