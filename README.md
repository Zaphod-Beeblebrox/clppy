# Clppy

> *"It looks like you're trying to paste something. Would you like help with that?"*

A Windows clipboard manager. MIT-licensed FOSS.

## Status

**Pre-alpha — under construction.**

This project is being built primarily by a crew of autonomous AI agents running on local hardware, as an exercise in autonomous local-AI software development. Code is reviewed and merged by a human stakeholder.

The full v0 specification is at [`SPEC.md`](SPEC.md).

## What it does (planned for v0)

- Replaces the abandoned **Spartan Multi Clipboard** with a clean-room FOSS implementation
- Single-sheet free-form 2D clip grid (you pick the cell)
- Auto-rolling clipboard history zone
- Multi-format clipboard preservation (text, RTF, HTML, image)
- Two paste engines:
  - **Direct** — standard clipboard paste with full format preservation
  - **Inject** — synthesizes keystrokes via `SendInput` for apps that don't accept paste (e.g., legacy form fields with tab navigation)
- Global hotkeys, per-clip color, soft delete, persistent storage

See [`SPEC.md`](SPEC.md) for the full scope.

## Building

Build instructions will be added once the v0 scaffold lands.

Target stack: C# + WPF + .NET 8. Windows 10 / 11 (x64) only.

## License

MIT. See [`LICENSE`](LICENSE).
