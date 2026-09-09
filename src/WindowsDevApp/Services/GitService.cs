using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace WindowsDevApp.Services;

public class BranchItem
{
    public string Name { get; set; } = "";
    public bool IsRemote { get; set; }
    public bool IsCurrent { get; set; }
    public override string ToString() => IsCurrent ? $"{Name}  (current)" : Name;
}

public class GitOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public static GitOperationResult Ok(string message) => new() { Success = true, Message = message };
    public static GitOperationResult Fail(string message) => new() { Success = false, Message = message };
}

/// <summary>
/// Local git operations (fetch/branch/checkout/pull) against a target repo folder,
/// authenticating over HTTPS with the signed-in GitHub token when one is needed.
/// </summary>
public class GitService
{
    private readonly Func<string?> _getToken;

    public GitService(Func<string?> getToken)
    {
        _getToken = getToken;
    }

    private CredentialsHandler CredentialsProvider => (_, _, _) =>
        new UsernamePasswordCredentials { Username = _getToken() ?? "", Password = string.Empty };

    public bool IsValidRepo(string path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && Repository.IsValid(path);
        }
        catch
        {
            return false;
        }
    }

    public string GetCurrentBranch(string repoPath)
    {
        using var repo = new Repository(repoPath);
        return repo.Head.FriendlyName;
    }

    public GitOperationResult Fetch(string repoPath)
    {
        try
        {
            using var repo = new Repository(repoPath);
            var remote = repo.Network.Remotes["origin"];
            if (remote == null) return GitOperationResult.Fail("Repo has no 'origin' remote.");

            var refSpecs = remote.FetchRefSpecs.Select(r => r.Specification);
            var options = new FetchOptions { CredentialsProvider = CredentialsProvider };
            Commands.Fetch(repo, remote.Name, refSpecs, options, "fetch via WindowsDevApp");
            return GitOperationResult.Ok("Fetched latest from origin.");
        }
        catch (Exception ex)
        {
            return GitOperationResult.Fail($"Fetch failed: {ex.Message}");
        }
    }

    public List<BranchItem> ListBranches(string repoPath)
    {
        var results = new List<BranchItem>();
        using var repo = new Repository(repoPath);
        var localNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var b in repo.Branches.Where(b => !b.IsRemote))
        {
            results.Add(new BranchItem { Name = b.FriendlyName, IsRemote = false, IsCurrent = b.IsCurrentRepositoryHead });
            localNames.Add(b.FriendlyName);
        }

        foreach (var b in repo.Branches.Where(b => b.IsRemote && !b.FriendlyName.EndsWith("/HEAD", StringComparison.Ordinal)))
        {
            var slash = b.FriendlyName.IndexOf('/');
            var shortName = slash >= 0 ? b.FriendlyName[(slash + 1)..] : b.FriendlyName;
            if (localNames.Contains(shortName)) continue;
            results.Add(new BranchItem { Name = b.FriendlyName, IsRemote = true, IsCurrent = false });
        }

        return results.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public GitOperationResult Checkout(string repoPath, BranchItem branchItem)
    {
        try
        {
            using var repo = new Repository(repoPath);
            Branch? target;

            if (!branchItem.IsRemote)
            {
                target = repo.Branches[branchItem.Name];
                if (target == null) return GitOperationResult.Fail($"Local branch '{branchItem.Name}' not found.");
            }
            else
            {
                var remoteBranch = repo.Branches[branchItem.Name];
                if (remoteBranch == null) return GitOperationResult.Fail($"Remote branch '{branchItem.Name}' not found.");

                var slash = branchItem.Name.IndexOf('/');
                var shortName = slash >= 0 ? branchItem.Name[(slash + 1)..] : branchItem.Name;

                var local = repo.Branches[shortName];
                if (local == null)
                {
                    local = repo.CreateBranch(shortName, remoteBranch.Tip);
                    repo.Branches.Update(local, b => b.TrackedBranch = remoteBranch.CanonicalName);
                }
                target = local;
            }

            Commands.Checkout(repo, target);
            return GitOperationResult.Ok($"Checked out '{target.FriendlyName}'.");
        }
        catch (CheckoutConflictException)
        {
            return GitOperationResult.Fail("Checkout failed: local changes would be overwritten. Use Force Sync to discard them.");
        }
        catch (Exception ex)
        {
            return GitOperationResult.Fail($"Checkout failed: {ex.Message}");
        }
    }

    /// <summary>Fetches, then fast-forwards the current branch to match its remote tracking branch.</summary>
    public GitOperationResult Pull(string repoPath)
    {
        try
        {
            using var repo = new Repository(repoPath);
            var remote = repo.Network.Remotes["origin"];
            if (remote == null) return GitOperationResult.Fail("Repo has no 'origin' remote.");

            var refSpecs = remote.FetchRefSpecs.Select(r => r.Specification);
            var fetchOptions = new FetchOptions { CredentialsProvider = CredentialsProvider };
            Commands.Fetch(repo, remote.Name, refSpecs, fetchOptions, "fetch via WindowsDevApp");

            var signature = new Signature("WindowsDevApp", "devapp@local", DateTimeOffset.Now);
            var pullOptions = new PullOptions
            {
                FetchOptions = fetchOptions,
                MergeOptions = new MergeOptions { FastForwardStrategy = FastForwardStrategy.Default }
            };
            var result = Commands.Pull(repo, signature, pullOptions);

            return result.Status switch
            {
                MergeStatus.UpToDate => GitOperationResult.Ok("Already up to date."),
                MergeStatus.FastForward => GitOperationResult.Ok("Updated (fast-forward)."),
                MergeStatus.NonFastForward => GitOperationResult.Fail("Local branch has diverged from remote. Use Force Sync to discard local changes and match remote."),
                MergeStatus.Conflicts => GitOperationResult.Fail("Merge produced conflicts. Use Force Sync to discard local changes and match remote."),
                _ => GitOperationResult.Ok("Pull completed.")
            };
        }
        catch (Exception ex)
        {
            return GitOperationResult.Fail($"Pull failed: {ex.Message}");
        }
    }

    /// <summary>Fetches, then hard-resets the current branch and cleans untracked files to exactly match origin.</summary>
    public GitOperationResult ForceSyncToRemote(string repoPath)
    {
        try
        {
            using var repo = new Repository(repoPath);
            var remote = repo.Network.Remotes["origin"];
            if (remote == null) return GitOperationResult.Fail("Repo has no 'origin' remote.");

            var refSpecs = remote.FetchRefSpecs.Select(r => r.Specification);
            var fetchOptions = new FetchOptions { CredentialsProvider = CredentialsProvider };
            Commands.Fetch(repo, remote.Name, refSpecs, fetchOptions, "fetch via WindowsDevApp");

            var currentBranch = repo.Head;
            var trackedName = currentBranch.TrackedBranch?.CanonicalName ?? $"refs/remotes/origin/{currentBranch.FriendlyName}";
            var remoteBranch = repo.Branches[trackedName];
            if (remoteBranch == null) return GitOperationResult.Fail("No remote tracking branch found for the current branch.");

            repo.Reset(ResetMode.Hard, remoteBranch.Tip);
            repo.RemoveUntrackedFiles();
            return GitOperationResult.Ok($"Force-synced '{currentBranch.FriendlyName}' to match '{remoteBranch.FriendlyName}'.");
        }
        catch (Exception ex)
        {
            return GitOperationResult.Fail($"Force sync failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Local branches whose tip is an ancestor of (i.e. fully merged into) the current branch,
    /// excluding the current branch itself and common protected branch names.
    /// </summary>
    public List<BranchItem> GetMergedLocalBranches(string repoPath)
    {
        using var repo = new Repository(repoPath);
        var head = repo.Head;
        var protectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "main", "master", head.FriendlyName };

        var result = new List<BranchItem>();
        foreach (var b in repo.Branches.Where(b => !b.IsRemote))
        {
            if (protectedNames.Contains(b.FriendlyName)) continue;

            var mergeBase = repo.ObjectDatabase.FindMergeBase(b.Tip, head.Tip);
            if (mergeBase != null && mergeBase.Sha == b.Tip.Sha)
            {
                result.Add(new BranchItem { Name = b.FriendlyName, IsRemote = false, IsCurrent = false });
            }
        }

        return result.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Deletes the named local branches. Remote branches are never touched.</summary>
    public GitOperationResult DeleteLocalBranches(string repoPath, IEnumerable<string> branchNames)
    {
        try
        {
            using var repo = new Repository(repoPath);
            var deleted = new List<string>();

            foreach (var name in branchNames)
            {
                var branch = repo.Branches[name];
                if (branch == null || branch.IsRemote) continue;
                repo.Branches.Remove(branch);
                deleted.Add(name);
            }

            return deleted.Count > 0
                ? GitOperationResult.Ok($"Deleted {deleted.Count} branch(es): {string.Join(", ", deleted)}")
                : GitOperationResult.Ok("No branches were deleted.");
        }
        catch (Exception ex)
        {
            return GitOperationResult.Fail($"Delete failed: {ex.Message}");
        }
    }
}
