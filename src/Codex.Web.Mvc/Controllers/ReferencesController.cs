using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Codex;
using Codex.ElasticSearch.Search;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Search;
using Codex.Web.Mvc.Utilities;
using Reference = Codex.Sdk.Search.IReferenceSearchResult;
using Codex.Storage.DataModel;

namespace Codex.Web.Mvc.Controllers
{
    public class ReferencesController : Controller
    {
        private readonly ICodex Storage;

        public static readonly IEqualityComparer<IReferenceSymbol> ReferenceSymbolEqualityComparer = new Codex.Utilities.EqualityComparerBuilder<IReferenceSymbol>()
            .CompareByAfter(s => s.ProjectId)
            .CompareByAfter(s => s.Id.Value)
            .CompareByAfter(s => s.ReferenceKind);

        public static readonly IEqualityComparer<IDefinitionSymbol> DefinitionSymbolEqualityComparer = ReferenceSymbolEqualityComparer;

        public ReferencesController(ICodex storage)
        {
            Storage = storage;
        }
        [Route("repos/{repoName}/references/{projectId}")]
        [Route("references/{projectId}")]
        public async Task<ActionResult> References(string projectId, string symbolId, string projectScope = null)
        {
            try
            {
                Requests.LogRequest(this);

                Responses.PrepareResponse(Response);

                var referencesResponse = await Storage.FindAllReferencesAsync(new FindAllReferencesArguments()
                {
                    RepositoryScopeId = this.GetSearchRepo(),
                    SymbolId = symbolId,
                    ProjectId = projectId,
                    ProjectScopeId = projectScope
                });

                var referencesResult = referencesResponse.Result;

                if (referencesResult.Hits.Count != 0)
                {
                    return PartialView((object)GenerateReferencesHtml(referencesResult));
                }

                return PartialView((object)$"No references to project {projectId} and symbol {symbolId} found.");
            }
            catch (Exception ex)
            {
                return Responses.Exception(ex);
            }
        }

        private static void WriteImplementedInterfaceMembers(StringWriter writer, IReadOnlyList<IDefinitionSymbol> implementedInterfaceMembers)
        {
            Write(writer, string.Format(@"<div class=""rH"" onclick=""ToggleExpandCollapse(this);return false;"">Implemented interface member{0}:</div>", implementedInterfaceMembers.Count > 1 ? "s" : ""));

            Write(writer, @"<div class=""rK"">");

            foreach (var implementedInterfaceMember in implementedInterfaceMembers)
            {
                writer.Write(GetSymbolText(implementedInterfaceMember));
            }

            writer.Write("</div>");
        }

        public static string GetSymbolText(IDefinitionSymbol searchResult)
        {
            var resultText = $@"<a onclick=""D('{searchResult.ProjectId.AsJavaScriptStringEncoded()}', '{searchResult.Id.Value.AsJavaScriptStringEncoded()}');return false;"" href=""/?rightProject={searchResult.ProjectId}&rightSymbol={searchResult.Id.Value}"">
 <div class=""resultItem"">
 <img src=""/content/icons/{searchResult.GetGlyph()}.png"" height=""16"" width=""16"" /><div class=""resultKind"">{searchResult.Kind.ToLowerInvariant()}</div><div class=""resultName"">{searchResult.ShortName}</div><div class=""resultDescription"">{searchResult.DisplayName}</div>
 </div>
 </a>";

            return resultText;
        }

        private static void WriteBaseMember(StringWriter writer, IDefinitionSymbol baseDefinition)
        {
            Write(writer, string.Format(@"<div class=""rH"" onclick=""ToggleExpandCollapse(this);return false;"">Base:</div>"));

            Write(writer, @"<div class=""rK"">");

            writer.Write(GetSymbolText(baseDefinition));

            writer.Write("</div>");
        }

        public static string GenerateReferencesHtml(ReferencesResult symbolReferenceResult)
        {
            var references = symbolReferenceResult.Hits;
            var definitionProjectId = symbolReferenceResult.ProjectId;
            var symbolId = symbolReferenceResult.SymbolId;
            var symbolName = symbolReferenceResult.SymbolDisplayName;

            int totalReferenceCount = 0;
            var referenceKindGroups = CreateReferences(references, out totalReferenceCount);

            using (var writer = new StringWriter())
            {
                if ((symbolReferenceResult.RelatedDefinitions?.Count ?? 0) != 0)
                {
                    var baseMember = symbolReferenceResult.RelatedDefinitions
                        .Where(r => r.ReferenceKind == nameof(ReferenceKind.Override))
                        .FirstOrDefault();
                    if (baseMember != null)
                    {
                        WriteBaseMember(writer, baseMember.Symbol);
                    }

                    var implementedMembers = symbolReferenceResult.RelatedDefinitions
                        .Where(r => r.ReferenceKind == nameof(ReferenceKind.InterfaceMemberImplementation))
                        .ToList();

                    if (implementedMembers.Count != 0)
                    {
                        WriteImplementedInterfaceMembers(writer, implementedMembers.Select(s => s.Symbol).Distinct(DefinitionSymbolEqualityComparer).ToList());
                    }
                }

                foreach (var referenceKind in referenceKindGroups.OrderBy(t => (int)t.Item1))
                {
                    string formatString = "";
                    switch (referenceKind.Item1)
                    {
                        case ReferenceKind.Reference:
                        case ReferenceKind.UsingDispose:
                            formatString = "{0} reference{1} to {2}";
                            break;
                        case ReferenceKind.Definition:
                            formatString = "{0} definition{1} of {2}";
                            break;
                        case ReferenceKind.Constructor:
                            formatString = "{0} constructor{1} of {2}";
                            break;
                        case ReferenceKind.Instantiation:
                            formatString = "{0} instantiation{1} of {2}";
                            break;
                        case ReferenceKind.DerivedType:
                            formatString = "{0} type{1} derived from {2}";
                            break;
                        case ReferenceKind.InterfaceInheritance:
                            formatString = "{0} interface{1} inheriting from {2}";
                            break;
                        case ReferenceKind.InterfaceImplementation:
                            formatString = "{0} implementation{1} of {2}";
                            break;
                        case ReferenceKind.Override:
                            formatString = "{0} override{1} of {2}";
                            break;
                        case ReferenceKind.InterfaceMemberImplementation:
                            formatString = "{0} implementation{1} of {2}";
                            break;
                        case ReferenceKind.Write:
                            formatString = "{0} write{1} to {2}";
                            break;
                        case ReferenceKind.Read:
                            formatString = "{0} read{1} of {2}";
                            break;
                        case ReferenceKind.GuidUsage:
                            formatString = "{0} usage{1} of Guid {2}";
                            break;
                        case ReferenceKind.EmptyArrayAllocation:
                            formatString = "{0} allocation{1} of empty arrays";
                            break;
                        case ReferenceKind.MSBuildPropertyAssignment:
                            formatString = "{0} assignment{1} to MSBuild property {2}";
                            break;
                        case ReferenceKind.MSBuildPropertyUsage:
                            formatString = "{0} usage{1} of MSBuild property {2}";
                            break;
                        case ReferenceKind.MSBuildItemAssignment:
                            formatString = "{0} assignment{1} to MSBuild item {2}";
                            break;
                        case ReferenceKind.MSBuildItemUsage:
                            formatString = "{0} usage{1} of MSBuild item {2}";
                            break;
                        case ReferenceKind.MSBuildTargetDeclaration:
                            formatString = "{0} declaration{1} of MSBuild target {2}";
                            break;
                        case ReferenceKind.MSBuildTargetUsage:
                            formatString = "{0} usage{1} of MSBuild target {2}";
                            break;
                        case ReferenceKind.MSBuildTaskDeclaration:
                            formatString = "{0} import{1} of MSBuild task {2}";
                            break;
                        case ReferenceKind.MSBuildTaskUsage:
                            formatString = "{0} call{1} to MSBuild task {2}";
                            break;
                        case ReferenceKind.Text:
                            formatString = "{0} text search hit{1} for '{2}'";
                            break;
                        default:
                            throw new NotImplementedException("Missing case for " + referenceKind.Item1);
                    }

                    var referencesOfSameKind = referenceKind.Item2.OrderBy(g => g.Item1, StringComparer.OrdinalIgnoreCase);
                    totalReferenceCount = CountItems(referenceKind);
                    string headerText = string.Format(
                        formatString,
                        totalReferenceCount,
                        totalReferenceCount == 1 ? "" : "s",
                        symbolName);

                    Write(writer, string.Format(@"<div class=""rH"" onclick=""ToggleExpandCollapse(this); return false;"">{0}</div>", headerText));

                    Write(writer, @"<div class=""rK"">");

                    foreach (var sameAssemblyReferencesGroup in referencesOfSameKind)
                    {
                        string assemblyName = sameAssemblyReferencesGroup.Item1;
                        Write(writer, "<div class=\"rA\" onclick=\"ToggleExpandCollapse(this); return false;\">{0} ({1})</div>", assemblyName, CountItems(sameAssemblyReferencesGroup));
                        Write(writer, "<div class=\"rG\" id=\"{0}\">", assemblyName);

                        foreach (var sameFileReferencesGroup in sameAssemblyReferencesGroup.Item2.OrderBy(g => g.Item1))
                        {
                            Write(writer, "<div class=\"rF\">");
                            var fileName = sameFileReferencesGroup.Item1;
                            var glyph = "url('/content/icons/" + GetGlyph(fileName) + ".png');";
                            WriteLine(writer, "<div class=\"rN\" style=\"background-image: {2}\">{0} ({1})</div>", fileName, CountItems(sameFileReferencesGroup), glyph);

                            foreach (var sameLineReferencesGroup in sameFileReferencesGroup.Item2)
                            {
                                var first = sameLineReferencesGroup.First();
                                var lineNumber = first.ReferenceSpan.LineNumber;
                                string onClick = $@"LoadSourceCode('{first.ProjectId}', '{first.ProjectRelativePath.AsJavaScriptStringEncoded()}', null, '{lineNumber}');return false;";
                                var url = $"/?leftProject={definitionProjectId}&leftSymbol={symbolId}&rightProject={first.ProjectId}&file={HttpUtility.UrlEncode(first.ProjectRelativePath)}&line={lineNumber}";
                                Write(writer, "<a class=\"rL\" onclick=\"{0}\" href=\"{1}\">", onClick, url);

                                Write(writer, "<b>{0}</b>", sameLineReferencesGroup.Key);
                                MergeOccurrences(writer, sameLineReferencesGroup);
                                WriteLine(writer, "</a>");
                            }

                            WriteLine(writer, "</div>");
                        }

                        WriteLine(writer, "</div>");
                    }

                    WriteLine(writer, "</div>");
                }

                return writer.ToString();
            }
        }

        private static string GetGlyph(string fileName)
        {
            return GlyphUtilities.GetFileNameGlyph(fileName);
        }

        private static int CountItems(Tuple<string, IEnumerable<IGrouping<int, Reference>>> sameFileReferencesGroup)
        {
            int count = 0;

            foreach (var line in sameFileReferencesGroup.Item2)
            {
                foreach (var occurrence in line)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountItems(
            Tuple<string, IEnumerable<Tuple<string, IEnumerable<IGrouping<int, Reference>>>>> resultsInAssembly)
        {
            int count = 0;
            foreach (var file in resultsInAssembly.Item2)
            {
                count += CountItems(file);
            }

            return count;
        }

        private static int CountItems(
            Tuple<ReferenceKind, IEnumerable<Tuple<string, IEnumerable<Tuple<string, IEnumerable<IGrouping<int, Reference>>>>>>> results)
        {
            int count = 0;
            foreach (var item in results.Item2)
            {
                count += CountItems(item);
            }

            return count;
        }

        private static
            IEnumerable<Tuple<ReferenceKind,
                IEnumerable<Tuple<string,
                    IEnumerable<Tuple<string,
                        IEnumerable<IGrouping<int, Reference>>
                    >>
                >>
            >> CreateReferences(
            IEnumerable<IReferenceSearchResult> list,
            out int totalReferenceCount)
        {
            totalReferenceCount = 0;

            var result = list.GroupBy
            (
                r0 => ParseReferenceKind(r0.ReferenceSpan.Reference.ReferenceKind),
                (kind, referencesOfSameKind) => Tuple.Create
                (
                    kind,
                    referencesOfSameKind.GroupBy
                    (
                        r1 => r1.ProjectId,
                        (assemblyName, referencesInSameAssembly) => Tuple.Create
                        (
                            assemblyName,
                            referencesInSameAssembly.GroupBy
                            (
                                r2 => r2.ProjectRelativePath,
                                (filePath, referencesInSameFile) => Tuple.Create
                                (
                                    filePath,
                                    referencesInSameFile.GroupBy
                                    (
                                        r3 => r3.ReferenceSpan.LineNumber
                                    )
                                )
                            )
                        )
                    )
                )
            );

            return result;
        }

        private static ReferenceKind ParseReferenceKind(string kind)
        {
            ReferenceKind referenceKind;
            if (!Enum.TryParse<ReferenceKind>(kind, true, out referenceKind))
            {
                referenceKind = ReferenceKind.Reference;
            }

            return referenceKind;
        }

        private static void MergeOccurrences(TextWriter writer, IEnumerable<Reference> referencesOnTheSameLineEx)
        {
            foreach (var referencesOnTheSameLineGroup in referencesOnTheSameLineEx.GroupBy(r => r.ReferenceSpan.LineSpanText))
            {
                var text = referencesOnTheSameLineGroup.Key;
                var referencesOnTheSameLine = referencesOnTheSameLineGroup.OrderBy(r => r.ReferenceSpan.LineSpanStart);
                int current = 0;
                foreach (var occurrence in referencesOnTheSameLine)
                {
                    if (occurrence.ReferenceSpan.LineSpanStart < current)
                    {
                        continue;
                    }

                    if (occurrence.ReferenceSpan.LineSpanStart > current)
                    {
                        var length = occurrence.ReferenceSpan.LineSpanStart - current;
                        length = Math.Min(length, text.Length);
                        var substring = text.Substring(current, length);
                        Write(writer, HttpUtility.HtmlEncode(substring));
                    }

                    Write(writer, "<i>");

                    var highlightStart = occurrence.ReferenceSpan.LineSpanStart;
                    var highlightLength = occurrence.ReferenceSpan.Length;
                    if (highlightStart >= 0 && highlightStart < text.Length && highlightStart + highlightLength <= text.Length)
                    {
                        var highlightText = text.Substring(highlightStart, highlightLength);
                        Write(writer, HttpUtility.HtmlEncode(highlightText));
                    }

                    Write(writer, "</i>");
                    current = occurrence.ReferenceSpan.LineSpanStart + occurrence.ReferenceSpan.Length;
                }

                if (current < text.Length)
                {
                    Write(writer, HttpUtility.HtmlEncode(text.Substring(current, text.Length - current)));
                }
            }
        }

        private static void Write(TextWriter sw, string text)
        {
            sw.Write(text);
        }

        private static void Write(TextWriter sw, string format, params object[] args)
        {
            sw.Write(string.Format(format, args));
        }

        private static void WriteLine(TextWriter sw, string text)
        {
            sw.WriteLine(text);
        }

        private static void WriteLine(TextWriter sw, string format, params object[] args)
        {
            sw.WriteLine(string.Format(format, args));
        }
    }
}