# Windows Dev App

A portable Windows desktop app (WPF, .NET 8) for testing Vercel preview branches locally
without running git/service commands by hand. It lets you, per saved profile:

- Sign in to GitHub with a Personal Access Token (used to authenticate git fetch/pull over HTTPS).
- List local and remote branches of a target repo folder, and checkout/pull/force-sync them.
- Edit a single field of a local `.env` file by pasting a new value and saving.
- Restart a local Windows service.

## Requirements to build

This is a WPF app, so it must be built **on Windows**, with the
[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed. It cannot be built on
macOS/Linux because WPF only runs on Windows.

## Build & run (development)

```
cd src/WindowsDevApp
dotnet run
```

## Publish a portable single-file .exe

```
cd src/WindowsDevApp
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The output `.exe` will be under `src/WindowsDevApp/bin/Release/net8.0-windows/win-x64/publish/`.
Copy that one file anywhere (a USB stick, another machine, etc.) — no install step required.

The app requests Administrator privileges on launch (see `app.manifest`), since restarting a
Windows service normally requires elevation.

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
  your working tree. Select a branch (local or `origin/...`) and click "Checkout selected". "Pull
  / Update" fast-forwards the current branch to match its remote. If the branch has diverged or
  has local edits, use "Force sync to remote" — this discards local changes and untracked files
  and hard-resets to match `origin` exactly, which is normally what you want when just testing a
  preview branch.
- **Environment File tab**: pick a field from the list, paste the new value into the box, and
  click "Save value". Every other line in the file (comments, blank lines, other keys) is left
  untouched.
- **Windows Service tab**: shows the configured service's current status; "Restart service" stops
  then starts it, waiting up to 30 seconds for each transition.

## Notes

- Profiles and the encrypted GitHub token live in `%APPDATA%\WindowsDevApp\`.
- All git operations act on the working tree you point at directly (via LibGit2Sharp, bundled
  into the app) — no separate `git.exe` install is required.
