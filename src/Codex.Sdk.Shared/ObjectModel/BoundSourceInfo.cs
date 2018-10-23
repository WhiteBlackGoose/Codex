using Codex.Utilities;
using System.Diagnostics;
using System.Linq;
using static Codex.Utilities.SerializationUtilities;

namespace Codex.ObjectModel
{
    partial class BoundSourceInfo
    {
        public int CoerceReferenceCount(int? value)
        {
            return value ?? References.Count;
        }

        public int CoerceDefinitionCount(int? value)
        {
            return value ?? Definitions.Count;
        }
    }

    partial class BoundSourceFile
    {
        public string IndexName { get; set; }

        //protected override void OnSerializingCore()
        //{
        //    string projectId = this.ProjectId;
        //    string containerQualifiedName = null;
        //    string kind = null;
        //    Glyph glyph = default(Glyph);
        //    foreach (var definitionSpan in this.Definitions)
        //    {
        //        var definition = definitionSpan.Definition;
        //        definition.ProjectId = RemoveDuplicate(definition.ProjectId, ref projectId);
        //        definition.Kind = RemoveDuplicate(definition.Kind, ref kind);
        //        definition.ContainerQualifiedName = RemoveDuplicate(definition.ContainerQualifiedName, ref containerQualifiedName);
        //        definition.Glyph = RemoveDuplicate(definition.Glyph, ref glyph);
        //    }

        //    var content = this.SourceFile.Content;
        //    this.SourceFile.Content = null;

        //    SourceFileContentLines.Clear();

        //    int startIndex = 0;
        //    while (true)
        //    {
        //        var newLineIndex = content.IndexOf('\n', startIndex);
        //        if (newLineIndex < 0)
        //        {
        //            if (startIndex < content.Length)
        //            {
        //                SourceFileContentLines.Add(content.Substring(startIndex));
        //            }

        //            break;
        //        }

        //        SourceFileContentLines.Add(content.Substring(startIndex, newLineIndex - startIndex + 1));
        //        startIndex = newLineIndex + 1;
        //    }

        //    Debug.Assert(SourceFileContentLines.Sum(l => l.Length) == content.Length);

        //    base.OnSerializingCore();
        //}

        //protected override void OnDeserializedCore()
        //{
        //    string projectId = this.ProjectId;
        //    string containerQualifiedName = null;
        //    string kind = null;
        //    Glyph glyph = default(Glyph);
        //    foreach (var definitionSpan in this.Definitions)
        //    {
        //        var definition = definitionSpan.Definition;
        //        definition.ProjectId = AssignDuplicate(definition.ProjectId, ref projectId);
        //        definition.Kind = AssignDuplicate(definition.Kind, ref kind);
        //        definition.ContainerQualifiedName = AssignDuplicate(definition.ContainerQualifiedName, ref containerQualifiedName);
        //        definition.Glyph = AssignDuplicate(definition.Glyph, ref glyph);
        //    }

        //    this.SourceFile.Content = string.Join(string.Empty, SourceFileContentLines);

        //    base.OnDeserializedCore();
        //}
    }

    partial class BoundSourceSearchModel
    {
        protected override void OnDeserializedCore()
        {
            if (CompressedClassifications != null)
            {
                BindingInfo.Classifications = CompressedClassifications.ToList();
            }

            if (CompressedReferences != null)
            {
                BindingInfo.References = CompressedReferences.ToList();
            }

            base.OnDeserializedCore();
        }

        protected override void OnSerializingCore()
        {
            base.OnSerializingCore();
        }
    }
}