using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Codex.ObjectModel;
using System;
using System.Text.RegularExpressions;

namespace Codex.Utilities
{
    public static class StoreUtilities
    {
        private static Tuple<Regex, string> vstsReplacement
            = Tuple.Create(
                new Regex(@"(?<host>https?://[^/]+/)(?<project>[^/]+/)?(_[^/]+/)+(?<repoName>[^/?#]+)"), 
                "${host}${project}_git/${repoName}?path=");

        private static Tuple<Regex, string> azDevReplacement
            = Tuple.Create(
                new Regex(@"(?<host>https?://[^/]+/[^/]+/)(?<project>[^/]+/)?(_[^/]+/)+(?<repoName>[^/?#]+)"),
                "${host}${project}_git/${repoName}?path=");

        private static Tuple<Regex, string> githubReplacement
            = Tuple.Create(
                new Regex(@"(?<host>https?://[^/]+/)(?<owner>[^/]+/)(?<project>[^/]+)/?"), 
                "${host}${owner}${project}/blob/master/");

        public static string GetSafeRepoName(string repoName)
        {
            var safeName = repoName
                .Replace('#', '_')
                .Replace('.', '_')
                .Replace(',', '_')
                .Replace(' ', '_')
                .Replace('\\', '_')
                .Replace('/', '_')
                .Replace('+', '_')
                .Replace('*', '_')
                .Replace('?', '_')
                .Replace('"', '_')
                .Replace('<', '_')
                .Replace('>', '_')
                .Replace('|', '_')
                .Replace(':', '_')
                .TrimStart('_');

            return safeName;
        }

        public static string GetSafeIndexName(string repoName)
        {
            var safeName = GetSafeRepoName(repoName.ToLowerInvariant());
            return safeName;
        }

        public static string GetTargetIndexName(string repoName)
        {
            return $"{GetSafeIndexName(repoName)}.{DateTime.UtcNow.ToString("yyMMdd.HHmmss")}";
        }

        private static void ApplyReplacement(ref string value, Tuple<Regex, string> replacementParameters)
        {
            value = replacementParameters.Item1.Replace(value, replacementParameters.Item2);
        }

        public static string GetFileWebAddress(string repoSourceControlAddress, string fileRepoRelativePath)
        {
            repoSourceControlAddress = repoSourceControlAddress.Trim();

            if (repoSourceControlAddress.ContainsIgnoreCase(".visualstudio.com"))
            {
                // VSTS web address
                // Ensure not already property formatted
                if (!repoSourceControlAddress.ContainsIgnoreCase("?path=") &&
                    !repoSourceControlAddress.ContainsIgnoreCase("#path="))
                {
                    ApplyReplacement(ref repoSourceControlAddress, vstsReplacement);
                }
            }
            else if (repoSourceControlAddress.ContainsIgnoreCase("dev.azure.com"))
            {
                // VSTS web address
                // Ensure not already property formatted
                if (!repoSourceControlAddress.ContainsIgnoreCase("?path=") &&
                    !repoSourceControlAddress.ContainsIgnoreCase("#path="))
                {
                    ApplyReplacement(ref repoSourceControlAddress, azDevReplacement);
                }
            }
            else if (repoSourceControlAddress.ContainsIgnoreCase("github.com"))
            {
                // Remove .git suffix if any
                repoSourceControlAddress = repoSourceControlAddress.TrimEndIgnoreCase("/");
                repoSourceControlAddress = repoSourceControlAddress.TrimEndIgnoreCase(".git");

                // GitHub web address
                // Ensure not already property formatted
                if (!repoSourceControlAddress.ContainsIgnoreCase("/blob/") && 
                    !repoSourceControlAddress.ContainsIgnoreCase("/tree/"))
                {
                    ApplyReplacement(ref repoSourceControlAddress, githubReplacement);
                }
            }

            return (repoSourceControlAddress.EnsureTrailingSlash() + fileRepoRelativePath).Replace("\\", "/");
        }
    }
}
