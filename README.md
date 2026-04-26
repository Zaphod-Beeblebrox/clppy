# Clppy

> "It looks like you're trying to paste something. Would you like help with that?"

A Windows clipboard manager. MIT-licensed FOSS.

## Status

**Pre-alpha — under construction.**

This project is being built primarily by a crew of autonomous AI agents running on local hardware, as an exercise in autonomous local-AI software development. Code is reviewed and merged by a human stakeholder.

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

This project is built by autonomous agents. Human stakeholders review and merge PRs.

### Development workflow

1. Create a feature branch: `crew/<topic>`
2. Make changes and write tests
3. Ensure all tests pass: `dotnet test`
4. Commit with clear messages
5. Push to GitHub for review

## License

MIT License — see [`LICENSE`](LICENSE) for details.

## Issues

To file an issue:

1. Go to the GitHub repository
2. Click "Issues" → "New issue"
3. Describe the problem or feature request
4. Include steps to reproduce (for bugs)

## Done Criteria (v0)

See [`SPEC.md` §7](SPEC.md#7-done-criteria) for the complete list of v0 done criteria.

Current status:
- ✅ Core tests pass (21 tests)
- ✅ Core builds cleanly on Linux
- ✅ WPF UI structure implemented (requires Windows to build)
- ⏳ Clipboard capture implemented (Win32)
- ⏳ Paste engines implemented (Win32)
- ⏳ UI requires final testing on Windows
