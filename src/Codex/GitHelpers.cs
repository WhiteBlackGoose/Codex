using System;
using System.IO;
using System.Linq;
using Codex.Logging;
using Codex.ObjectModel;
using Codex.Utilities;
using Git = LibGit2Sharp;

namespace Codex.Application
{
    public class GitHelpers
    {
        public static void DetectGit((Repository repository, Commit commit, Branch branch) repoData, string root, Logger logger)
        {
            try
            {
                (var repository, var commit, var branch) = repoData;
                root = Path.GetFullPath(root);
                logger.LogMessage($"DetectGit: Using LibGit2Sharp to load repo info for {root}");
                using (var repo = new Git.Repository(root))
                {
                    var tip = repo.Head.Tip;
                    var firstRemote = repo.Network.Remotes.FirstOrDefault();

                    commit.CommitId = Set(logger, "commit.CommitId", () => tip.Id.Sha);
                    commit.DateCommitted = Set(logger, "commit.DateCommited", () => tip.Committer.When.DateTime.ToUniversalTime());
                    commit.Description = Set(logger, "commit.Description", () => tip.Message?.Trim());
                    commit.ParentCommitIds.AddRange(Set(logger, "commit.ParentCommitIds", () => tip.Parents?.Select(c => c.Sha).ToArray() ?? CollectionUtilities.Empty<string>.Array, v => string.Join(", ", v)));
                    branch.Name = Set(logger, "branch.Name", () => GetBranchName(repo.Head));
                    branch.HeadCommitId = Set(logger, "branch.HeadCommitId", () => commit.CommitId);
                    repository.SourceControlWebAddress = Set(logger, "repository.SourceControlWebAddress", () => firstRemote?.Url?.TrimEndIgnoreCase(".git"), defaultValue: repository.SourceControlWebAddress);
                    
                    // TODO: Add changed files?
                }
            }
            catch (Exception ex)
            {
                logger.LogExceptionError("DetectGit", ex);
            }
        }

        private static string GetBranchName(Git.Branch head)
        {
            var name = head.TrackedBranch?.FriendlyName;
            if (name != null)
            {
                if (head.RemoteName != null)
                {
                    return name.TrimStartIgnoreCase(head.RemoteName).TrimStart('/');
                }

                return name;
            }

            return head.FriendlyName;
        }

        private static T Set<T>(Logger logger, string valueName, Func<T> get, Func<T, string> print = null, T defaultValue = default)
        {
            print = print ?? (v => v?.ToString());
            var value = get();

            if (!(value is object obj))
            {
                value = defaultValue;
            }

            logger.LogMessage($"DetectGit: Updating {valueName} to [{print(value)}]");
            return value;
        }
    }
}