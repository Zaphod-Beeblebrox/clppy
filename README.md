# Clppy

> "It looks like you're trying to paste something. Would you like help with that?"

A Windows clipboard manager. MIT-licensed FOSS.

## Status

**Pre-alpha — autonomously built. "Buyer beware".**

Clppy is built primarily by AI agents running on local hardware, as an
exploration of autonomous local-AI software development. Agents write code,
push to the repo, and (via GitHub Actions) build and release without a human
reviewer in the per-change critical path. The repository owner retains full
control of the project but should not be assumed to have audited each commit.

This is intentional — the project is partly an experiment in how far
autonomous-agent development can go and how it should be disclosed. If you
plan to use Clppy on real systems: read the source, build it yourself, or
wait for releases that have third-party scrutiny.

The full v0 specification is at [`SPEC.md`](SPEC.md).

## What it does (planned for v0)

- **Pristine-formatting paste** — Copy rich-text blocks and paste them without losing formatting
- **Form-fill keyboard injection** — Store clips with tab characters and have Clppy type them into fields that don't accept paste
- **History zone** — Automatic FIFO buffer for recent clipboard entries
- **Pinned clips** — Drag clips out of history to pin them permanently
- **Global hotkeys** — Assign hotkeys to clips for quick access
- **Filter overlay** — Ctrl+F to search clips by label or content

## Stack

- C# + WPF + .NET 8
- SQLite + Entity Framework Core 8
- Windows 10/11 only

## Build

### Prerequisites

- .NET 8 SDK
- Windows 10/11 (for building and running the WPF UI)

### Commands

```bash
# Restore dependencies
dotnet restore

# Build (Release mode)
dotnet build -c Release

# Run the application
dotnet run --project src/Clppy.App

# Run tests
dotnet test
```

## Project Structure

```
clppy/
├── src/
│   ├── Clppy.Core/        # Non-UI logic (models, persistence, paste engines)
│   └── Clppy.App/         # WPF UI application
└── tests/
    └── Clppy.Core.Tests/  # Unit tests for Core
```

## Contributing

The primary developer of this project is an autonomous AI agent loop. It
reads issues, writes code, runs CI on `windows-latest`, and produces
releases. Human PRs are welcome but are not the main mechanism by which
the project advances.

If you want to suggest a change, the most reliable channel is filing a
GitHub issue (see below) — it becomes input to the agent's next iteration.
You can also open a PR; the repository owner reviews them asynchronously.

## License

MIT License — see [`LICENSE`](LICENSE) for details.

## Issues

To file an issue:

1. Go to the GitHub repository
2. Click "Issues" → "New issue"
3. Describe the problem or feature request
4. Include steps to reproduce (for bugs)

Issues feed the autonomous agent loop and may be addressed in a subsequent
build without human triage. If you want a discussion before the agent acts,
say so in the issue body.

## Done Criteria (v0)

See [`SPEC.md` §7](SPEC.md#7-done-criteria) for the complete list of v0 done criteria.

Current status:
- ✅ Core tests pass (21 tests)
- ✅ Core builds cleanly on Linux
- ✅ WPF UI structure implemented (requires Windows to build)
- ⏳ Clipboard capture implemented (Win32)
- ⏳ Paste engines implemented (Win32)
- ⏳ UI requires final testing on Windows
