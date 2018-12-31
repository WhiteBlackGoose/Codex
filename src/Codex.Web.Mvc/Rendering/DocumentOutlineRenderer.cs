﻿using System;
using System.Linq;
using System.Text;
using System.Web;
using Codex;
using Codex.ObjectModel;
using Codex.Storage;

namespace Codex.Web.Mvc.Rendering
{
    public class DocumentOutlineRenderer
    {
        private IBoundSourceFile boundSourceFile;
        private string projectId;

        public DocumentOutlineRenderer(string projectId, IBoundSourceFile boundSourceFile)
        {
            this.projectId = projectId;
            this.boundSourceFile = boundSourceFile;
        }

        public string Generate()
        {
            var sb = new StringBuilder();

            sb.AppendLine("<div id=\"documentOutline\">");

            int current = 0;
            GenerateCore(sb, ref current, -1);

            sb.AppendLine("</div>");

            return sb.ToString();
        }

        public void GenerateCore(StringBuilder sb, ref int current, int parentDepth, string parentPrefix = "")
        {
            for (; current < boundSourceFile.Definitions.Count; current++)
            {
                int nextIndex = current + 1;
                var definition = boundSourceFile.Definitions[current];

                if (definition.Definition.IsImplicitlyDeclared)
                {
                    continue;
                }

                var symbol = definition.Definition;
                var depth = symbol.SymbolDepth;

                if (depth <= parentDepth)
                {
                    return;
                }

                var text = symbol.DisplayName;
                if (text.StartsWith(parentPrefix))
                {
                    text = text.Substring(parentPrefix.Length);
                }

                bool hasChildren = nextIndex != boundSourceFile.Definitions.Count &&
                    boundSourceFile.Definitions[nextIndex].Definition.SymbolDepth > depth;

                WriteFolderName(text, sb, definition.Definition.Id.Value, definition.Definition.Kind.ToLowerInvariant(), definition.Definition.GetGlyph(boundSourceFile?.ProjectRelativePath) + ".png", hasChildren);
                if (hasChildren)
                {
                    WriteFolderChildrenContainer(sb);
                    current++;
                    GenerateCore(sb, ref current, depth, symbol.DisplayName + ".");
                    sb.Append("</div>");
                }
            }
        }

        private void WriteFolderName(string folderName, StringBuilder sb, string symbolId, string kind, string folderIcon = "202.png", bool hasChildren = false)
        {
            folderName = HttpUtility.HtmlEncode(folderName);
            var url = $"/?left=outline&rightProject={projectId}&file={HttpUtility.UrlEncode(boundSourceFile.ProjectRelativePath)}&rightSymbol={symbolId}";
            var folderNameText = $"<a href=\"{url}\" onclick=\"event.stopPropagation();S('{symbolId}');return false;\"><span class=\"k\">{kind}</span>&nbsp;{folderName}</a>";
            var icon = $"<img src=\"/content/icons/{folderIcon}\" class=\"imageFolder\" />";
            if (hasChildren)
            {
                sb.Append($"<div class=\"folderTitle\" onclick=\"ToggleExpandCollapse(this);ToggleFolderIcon(this);\" style=\"background-image:url('/content/images/minus.png');\">{icon}{folderNameText}</div>");
            }
            else
            {
                sb.Append($"<div class=\"folderTitle\">{icon}{folderNameText}</div>");
            }
        }

        private static void WriteFolderChildrenContainer(StringBuilder sb)
        {
            sb.AppendLine("<div class=\"folder\" style=\"display: block;\">");
        }
    }
}