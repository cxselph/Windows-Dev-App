# Windows Dev App

A portable Windows desktop app (WPF, .NET 10 LTS) for testing Vercel preview branches locally
without running git/service commands by hand. It lets you, per saved profile:

- Sign in to GitHub with a Personal Access Token (used to authenticate git fetch/pull over HTTPS).
- List local and remote branches of a target repo folder, and checkout/pull/force-sync them.
- Clean up local branches that are already merged — including squash/rebase merges — and delete
  any single local branch by hand.
- Edit a single field of a local `.env` file by pasting a new value and saving.
- Restart a local Windows service.

## Requirements to build

This is a WPF app, so it must be built **on Windows**, with the
[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed. `global.json` pins the
SDK to 10.0.401 (later 10.0.4xx patches are allowed); run `dotnet --version` to check. WPF apps only
run on Windows.

.NET 10 is an LTS release, supported until November 2028. (The app was on .NET 8 until October 2026;
.NET 8 reaches end of life on November 10, 2026.)

## Build & run (development)

```
cd src/WindowsDevApp
dotnet run
```

## Publish a portable single-file .exe

```
cd src/WindowsDevApp
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:RestoreLockedMode=true
```

The output `.exe` will be under `src/WindowsDevApp/bin/Release/net10.0-windows/win-x64/publish/`.
`-p:RestoreLockedMode=true` makes the build fail rather than change `packages.lock.json`, so a release
only ever contains the reviewed package versions.

The exe is self-contained: it carries its own .NET runtime. That means **every copy has to be
rebuilt and replaced** to get .NET security fixes; a .NET update installed on the PC doesn't reach it.
Copy that one file anywhere (a USB stick, another machine, etc.) — no install step required.

The app requests Administrator privileges on launch (see `app.manifest`), since restarting a
Windows service normally requires elevation.

## Dependency updates

House rule: dependencies only change through a reviewed PR.

- **Locked restores:** `packages.lock.json` records every NuGet package's exact version and content
  hash (`RestorePackagesWithLockFile` in the `.csproj`). Commit it with any package change. Release
  builds use `-p:RestoreLockedMode=true` (above). To change a package, edit its version in the
  `.csproj`, run `dotnet restore`, and commit both files.
- **SDK pin:** `global.json` (10.0.401, patch roll-forward only). Move to a new SDK band or major
  version in its own PR.
- **Dependabot** (`.github/dependabot.yml`) proposes NuGet and SDK updates weekly. A release has to be
  public for 14 days first, and major versions are skipped. Security fixes skip both rules. Never
  auto-merge them. Each .NET monthly patch (security fixes in the runtime) means rebuilding and
  redistributing the exe.
- **Runtime:** stay on an LTS line. Plan the move off .NET 10 well before its end of support
  (November 2028).

## First-time setup

1. **GitHub token**: create a token at <https://github.com/settings/tokens> — a classic token
   with the `repo` scope is simplest, or a fine-grained token scoped to the repo(s) you'll test.
   Paste it into the top bar and click **Login**. It's stored encrypted on disk (Windows DPAPI,
   tied to your Windows user account) so you only need to do this once per machine.
2. **Add a profile** (left panel → Add): give it a name, then set:
   - **Local repo folder** — the existing git clone you want to control (must already have an
     `origin` remote configured; the app does not clone new repos).
   - **.env file path** — the env file whose values you want to edit for this target.
   - **Windows service name** — pick via the **Pick...** button (searches all installed services)
     or type the service's short name (not its display name) directly.
3. Click **Save profile**.

## Using it

- **Branches tab**: "Refresh (fetch)" pulls down the latest refs from `origin` without changing
  your working tree — this also prunes local `origin/...` entries whose branch was deleted on
  GitHub (e.g. after a PR merges and its head branch gets auto-deleted). Select a branch (local or
  `origin/...`) and click "Checkout selected". "Pull / Update" fast-forwards the current branch to
  match its remote. If the branch has diverged or has local edits, use "Force sync to remote" —
  this discards local changes and untracked files and hard-resets to match `origin` exactly, which
  is normally what you want when just testing a preview branch.
  - "Clean up merged branches" lists local branches already reflected in the current branch and
    lets you pick which to delete. This detects both regular merges (branch is an ancestor of
    HEAD) and squash/rebase merges (GitHub's default "Squash and merge" produces a new commit SHA,
    so it fingerprints each branch's total diff and checks it against commits already on the
    current branch — the same trick the `git-delete-squashed` tool uses). It's a content-based
    heuristic, so a branch with extra unmerged commits, or one whose PR hasn't merged yet, won't
    show up here.
  - "Delete branch" removes whichever local branch is selected, unconditionally (after a
    confirmation) — use this for branches the cleanup heuristic above doesn't catch. It won't let
    you delete the currently checked-out branch or a remote-tracking (`origin/...`) entry. Neither
    this nor "Clean up merged branches" ever deletes anything on GitHub — only local branch refs
    in the target repo folder.
- **Environment File tab**: pick a field from the list, paste the new value into the box, and
  click "Save value". Every other line in the file (comments, blank lines, other keys) is left
  untouched.
- **Windows Service tab**: shows the configured service's current status; "Restart service" stops
  then starts it, waiting up to 30 seconds for each transition.

## Notes

- Profiles and the encrypted GitHub token live in `%APPDATA%\WindowsDevApp\`.
- All git operations act on the working tree you point at directly (via LibGit2Sharp, bundled
  into the app) — no separate `git.exe` install is required.
- Any unexpected error is shown in a dialog with the full exception details rather than the app
  silently closing — if you hit one, that text is exactly what's needed to diagnose it.
