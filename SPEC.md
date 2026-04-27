# Clppy — MVP Specification (v0)

> "It looks like you're trying to paste something. Would you like help with that?"

This document was the original contract for the v0 build (shipped as `v0.0.1` on 2026-04-26). It remains authoritative for **product behavior** — sections 2–6, 10, and 11 define what Clppy is and what's in/out of scope. Section 7's done criteria define acceptance for any v0 regression.

The **process/governance** sections (§1 repo posture, §8 LOG.md, §9 conventions, §12 sign-off) have been updated to reflect the current autonomous-maintenance posture, not the original CrewAI-build posture they were written for.

---

## 1. Overview

Clppy is a Windows-only clipboard manager. MIT-licensed FOSS. It is a clean-room reimplementation in the spirit of (but not derived from) the abandoned **Spartan Multi Clipboard** by M8 Software. No code, branding, names, or assets from Spartan are used.

- **Stack:** C# + WPF + .NET 8
- **License:** MIT
- **Platform:** Windows 10 / Windows 11 (64-bit)
- **Repo posture:** Public GitHub. v0 is shipped. The project runs as an autonomous maintenance loop: a heartbeat-driven local-LLM agent reads open issues, commits fixes directly to `main`, and tags patch releases on issue closure. CI on `windows-latest` verifies the build; the release workflow produces a single-file self-contained `Clppy.App.exe` on `v*` tag push.

### 1.1 Target user

The primary target user is **Bob**, an insurance/auto-damage appraiser. Long-time Spartan user. Windows-only. Daily workflow involves repeatedly pasting boilerplate text (customer info, vendor names, vehicle data, document fragments) into emails, Word reports, and an estimating application that does not accept clipboard pastes.

Bob's two confirmed daily frictions, both addressed by v0:

1. **Pristine-formatting paste** — he copies a rich-text block (e.g., an email signature with color/fonts) and wants to paste it into another rich-text destination (e.g., Gmail compose) without losing formatting. Most clipboard managers strip to plain text. Clppy must NOT.
2. **Form-fill keyboard injection** — his estimating software has fields that don't accept clipboard paste. He stores customer-info clips that include tab characters between field values, and wants to invoke a clip such that Clppy *types* the content (including tabs to advance fields) into whichever window has focus.

### 1.2 Non-goals (explicit)

The following are NOT in v0. Do not build them, do not stub them, do not add settings UI for them.

- Encryption / master password
- Bulk paste / paste delay
- Image-into-Word automation pipeline
- Multi-sheet support (Bob uses one sheet)
- A-Z auto-sort of cells
- Year planner, graphics editor, picture browser, screenshot capture/edit (Spartan scope creep)
- Macro recording (the "Inject" engine described below is NOT a macro recorder; it's a typing engine)
- Cross-platform support (Linux/Mac)
- Auto-update mechanism
- Cloud sync

---

## 2. Core concepts

### 2.1 Clip

A unit of stored content. Has zero or more formats (plain text, RTF, HTML, image), an optional label, an optional cell position, an optional color, an optional hotkey, and a default paste method.

### 2.2 Sheet (single, MVP)

A free-form 2D grid of cells. The user picks the cell — there is **no auto-flow** into the next available position. Cells can be empty. Empty cells render blank. The grid is scrollable in both dimensions.

For v0, there is **exactly one sheet**. Do not build a sheet picker, sheet tabs, or multi-sheet metadata.

### 2.3 History zone

A visually distinct region in the **top-left** of the sheet, occupying a configurable area (default: 4 columns × 5 rows = 20 cells). New manual clipboard entries land here automatically as a FIFO buffer. When the buffer is full, the oldest entry rolls off.

A clip in the history zone is **unpinned** (`Pinned = false`). Moving (dragging) a clip out of the history zone into any other cell **pins** it (`Pinned = true`) and removes it from FIFO rolloff.

### 2.4 Paste engines

Two paste engines, both in v0:

- **Direct** — Set the system clipboard to the clip's full multi-format payload (every format we captured), then issue a paste into the focused window via `SendInput` with `Ctrl+V`. The receiving application chooses which format to consume — preserving rich text where the destination supports it.
- **Inject** — Iterate the clip's plain-text content character-by-character and synthesize keystrokes via `SendInput`. Tab characters become `VK_TAB` keystrokes. Newline characters become `VK_RETURN`. Other characters use Unicode keystroke injection (`KEYEVENTF_UNICODE`). Does NOT touch the system clipboard.

Each clip stores a **default paste method**. The user can override per-invocation (see §3.3).

---

## 3. UX specification

### 3.1 Main window

- Resizable WPF window
- Single-sheet grid view fills the main area
- Each cell renders:
  - Background color (clip's color, or default if none)
  - Label text (clip's `Label`, falling back to a truncated preview of plain-text content)
  - Small icon in a corner indicating paste engine (e.g., "▶" for Direct, "⌨" for Inject)
- History-zone cells have a distinct yellow tint applied on top of (or instead of) their per-clip color, to visually separate them
- Tray icon in the system notification area
- Closing the window via the X button **hides to tray**, does not exit the app
- True exit only via tray menu → Quit, or via Alt+F4 with a confirmation prompt

### 3.2 Cell sizing & layout

- Default cell size: 140 px wide × 32 px tall (tunable in code, not user-facing for v0)
- Grid initially shows ~9 columns × ~30 rows visible at default window size
- User can resize the window; grid fills available space
- Empty cells are visually present but blank (not collapsed)

### 3.3 Interactions

| Gesture                       | Action                                                                 |
|-------------------------------|------------------------------------------------------------------------|
| **Left-click on a clip cell** | Invoke the clip's default paste method into the previously-focused window |
| **Shift+left-click**          | Invoke the OTHER paste method (Direct↔Inject)                          |
| **Right-click on a clip cell**| Open context menu (see §3.4)                                            |
| **Right-click on empty cell** | Open small menu: "New clip from clipboard", "Paste here from clipboard" |
| **Drag a clip cell**          | Move the clip to another cell. If dragged out of history zone, set `Pinned = true`. If dragged into history zone, set `Pinned = false`. |
| **Double-click clip cell**    | Open clip editor (see §3.5)                                             |
| **Ctrl+F**                    | Open filter overlay (see §3.6)                                          |
| **Esc**                       | Close any open overlay/dialog. If none, hide window to tray.            |

### 3.4 Right-click context menu (on a clip cell)

Items, in order:
- **Paste** (uses clip's default method)
- **Paste as text** (forces Direct with plain-text only)
- **Paste as keystrokes** (forces Inject)
- ─── separator ───
- **Edit…** (opens clip editor)
- **Rename…** (inline label edit)
- **Set color…** (color picker)
- **Set hotkey…** (capture next key combo)
- **Set default paste method ▶** → Direct / Inject
- ─── separator ───
- **Move to history zone** (sets `Pinned = false`, places in history)
- **Delete** (soft delete; sets `DeletedAt`)

### 3.5 Clip editor (modal dialog)

Opened via Edit… in context menu, or double-click on a cell.

Fields:
- **Label** (single-line text)
- **Plain-text content** (multi-line text editor)
  - **Tab key inserts `\t` character — does NOT shift focus.** This is critical for Inject clips with form-field tab navigation.
  - Right-click in this field shows a small menu: "Insert Tab", "Insert Newline" — for users who prefer explicit insertion.
- **Default paste method** (radio: Direct / Inject)
- **Color** (color picker, optional)
- **Hotkey** (capture button + display, optional)
- **Save** / **Cancel** buttons

Multi-format content (RTF, HTML, image) is preserved on captured clips but not editable in v0. The editor shows a read-only indicator: "Has rich-text formats: RTF, HTML" or "Has image attachment".

### 3.6 Filter overlay (Ctrl+F)

- Overlay that slides in from the top of the main window — does NOT replace the grid view
- Single text input at the top
- As the user types, cells whose label OR plain-text content contain the substring (case-insensitive) remain at full opacity. Non-matching cells fade to ~25% opacity but remain in their grid positions (preserving spatial memory).
- Esc or click-outside dismisses the overlay and restores all cells to full opacity.

### 3.7 Tray icon

- Single-click: toggle main window visibility
- Right-click context menu:
  - **Show / Hide Clppy**
  - **Settings…** (placeholder dialog for v0; just a window with version info)
  - **About**
  - ─── separator ───
  - **Quit**

### 3.8 Global hotkeys

For each clip with a hotkey assigned:
- Register a Win32 global hotkey via `RegisterHotKey`
- When triggered: paste the clip using its default method into the currently focused window (NOT Clppy's window)
- Conflicts (hotkey already taken by another clip) are rejected at clip-edit time with an error message

For v0, do NOT implement a global hotkey to summon the Clppy window itself (could be added later as `Settings → Summon hotkey`).

### 3.9 Capture pipeline

- Subscribe to clipboard updates via `AddClipboardFormatListener` (Win32)
- On clipboard change:
  - Enumerate all available formats via `EnumClipboardFormats`
  - Capture each format's data (priority list: plain text, RTF, HTML, PNG image; ignore exotic formats)
  - Create a new clip with `Pinned = false`, no `Row`/`Col` (lives in history zone), `Method = Direct` by default
  - Push to the front of the history FIFO
  - If history is at capacity (default 20), drop the oldest unpinned clip
- Suppress capture loops: do NOT capture clipboard changes that Clppy itself made via Direct paste (track via a sequence number or sentinel)

---

## 4. Data model

### 4.1 Clip entity

```csharp
public enum PasteMethod { Direct, Inject }

public class Clip {
    public Guid Id { get; set; }
    public int? Row { get; set; }              // null when in history zone
    public int? Col { get; set; }              // null when in history zone
    public int? HistoryIndex { get; set; }     // 0 = newest, increases with age; null when pinned
    public string? Label { get; set; }
    public string? PlainText { get; set; }
    public byte[]? Rtf { get; set; }
    public byte[]? Html { get; set; }
    public byte[]? PngImage { get; set; }
    public PasteMethod Method { get; set; } = PasteMethod.Direct;
    public string? ColorHex { get; set; }      // e.g., "#FFEB3B"
    public string? Hotkey { get; set; }        // e.g., "Ctrl+Alt+A"
    public bool Pinned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }   // soft delete
}
```

### 4.2 Settings (single-row table)

```csharp
public class Settings {
    public int HistoryRows { get; set; } = 5;
    public int HistoryCols { get; set; } = 4;
    public int CellWidthPx { get; set; } = 140;
    public int CellHeightPx { get; set; } = 32;
    public int InjectKeystrokeDelayMs { get; set; } = 5;
    public string DefaultColorHex { get; set; } = "#F5F5F5";
}
```

### 4.3 Persistence

- **Storage:** SQLite database at `%APPDATA%\Clppy\clppy.db`
- **ORM:** Entity Framework Core 8 with SQLite provider, code-first migrations
- **Backup:** On startup, copy `clppy.db` to `clppy.db.bak` (single rolling backup) before applying any migrations
- **Export/import:** Out of scope for v0

---

## 5. Project structure

```
clppy/
├── Clppy.sln
├── README.md
├── LICENSE                         (MIT)
├── LOG.md                          (agent run journal — see §8)
├── .gitignore                      (standard .NET gitignore)
├── src/
│   ├── Clppy.App/                  (WPF application)
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── MainWindow.xaml / .cs
│   │   ├── ClipEditorWindow.xaml / .cs
│   │   ├── Views/                  (smaller XAML views)
│   │   ├── ViewModels/             (MVVM view models)
│   │   ├── Tray/                   (tray icon code)
│   │   ├── Interop/                (P/Invoke wrappers)
│   │   └── Clppy.App.csproj
│   └── Clppy.Core/                 (non-UI logic, testable)
│       ├── Models/                 (Clip, Settings)
│       ├── Persistence/            (DbContext, migrations)
│       ├── Clipboard/              (capture + format handling)
│       ├── Paste/                  (Direct + Inject engines, interface IPasteEngine)
│       ├── Hotkeys/                (registration logic)
│       └── Clppy.Core.csproj
└── tests/
    └── Clppy.Core.Tests/
        ├── ClipTests.cs
        ├── HistoryRolloffTests.cs
        ├── PasteRoutingTests.cs
        └── Clppy.Core.Tests.csproj
```

### 5.1 Key abstractions

- `IClipboardCapture` — produces `Clip` objects from system clipboard updates. UI subscribes.
- `IPasteEngine` — `Task PasteAsync(Clip clip, IntPtr targetWindow);`. Two implementations: `DirectPasteEngine`, `InjectPasteEngine`. Routed by `PasteRouter` based on `clip.Method` (or override).
- `IClipRepository` — CRUD + history rolloff. Thin EF Core wrapper.
- `IHotkeyService` — register/unregister global hotkeys. Raises events when triggered.

UI layer (`Clppy.App`) consumes these via constructor injection (Microsoft.Extensions.DependencyInjection).

---

## 6. Build, run, test

### 6.1 Toolchain

- .NET 8 SDK (assumed installed on dev machine)
- Building on Windows is the target. Building on Linux is acceptable for non-UI parts (`Clppy.Core` and `Clppy.Core.Tests`) using `dotnet build` with the appropriate targeting; the WPF `Clppy.App` project must build on Windows.

### 6.2 Build

```
dotnet restore
dotnet build -c Release
```

### 6.3 Run

```
dotnet run --project src/Clppy.App
```

### 6.4 Test

```
dotnet test
```

All tests in `Clppy.Core.Tests` must pass. Tests should be deterministic, in-memory where possible (use SQLite `:memory:` for repo tests).

### 6.5 Required test coverage (minimum)

- Clip serialization round-trip (write → read from SQLite)
- History FIFO rolloff (add 21 clips with capacity 20 → oldest dropped)
- Pinning a history clip (drag-out simulation) removes it from rolloff
- `PasteRouter` selects the correct engine for `clip.Method`
- `PasteRouter` honors override (Shift+click → opposite engine)
- Inject engine correctly enumerates `\t` as `VK_TAB` and `\n` as `VK_RETURN` in synthesized keystroke list (test the keystroke list, not the actual SendInput call)

---

## 7. "Done" criteria (v0 — shipped)

All criteria below were satisfied as of `v0.0.1` (2026-04-26). Retained as the acceptance contract for any v0 regression filed against the maintenance loop:

1. `dotnet build -c Release` succeeds with zero errors and zero warnings related to Clppy code.
2. `dotnet test` passes all tests.
3. Running `Clppy.App.exe` on a Windows machine:
   - Opens a window with a visible empty grid and a yellow-tinted history zone in the top-left.
   - Shows a tray icon.
   - Closing the window with X hides to tray; clicking tray icon restores the window.
4. **Capture works:** Copy text from any other app → a new clip appears in the history zone with that text as label/preview. Repeat 21 times → history holds exactly 20, oldest removed.
5. **Multi-format preserved:** Copy a formatted block (e.g., colored text from Word) → the resulting clip has Rtf and/or Html populated in addition to PlainText (verifiable by inspecting the SQLite DB).
6. **Direct paste works:** Left-click a Direct clip into a Word document → the formatting is preserved in the paste.
7. **Inject paste works:** Left-click an Inject clip whose content includes a tab character → focused Notepad receives the typed text including a tab.
8. **Override works:** Shift+left-click a Direct clip → Clppy injects via keystrokes instead.
9. **Right-click menu:** All entries listed in §3.4 are present and functional (Paste, Paste as text, Paste as keystrokes, Edit, Rename, Set color, Set hotkey, Set default paste method, Move to history, Delete).
10. **Clip editor:** Double-click a clip → editor opens. Tab key in the content field inserts a tab character. Save persists changes.
11. **Drag to pin:** Dragging a history clip into an empty cell pins it (becomes `Pinned = true`, gains `Row`/`Col`).
12. **Persistence:** Quit Clppy fully (tray → Quit) → relaunch → all pinned clips are present at their grid positions; history clips persist as well.
13. **Global hotkey:** Set a hotkey on a clip → press it from another focused app → clip pastes into that app.
14. **Filter:** Ctrl+F → type substring → matching cells stay opaque, non-matching fade. Esc dismisses.
15. **README** documents: what Clppy is, how to build, how to run, how to file an issue.
16. **LICENSE** is MIT.
17. **LOG.md** exists at repo root with the agent run journal (see §8).

---

## 8. LOG.md — Agent run journal (closed)

`LOG.md` is the historical narrative of the v0 build: scaffolding, Core implementation, the pivot from CrewAI to the heartbeat harness, the WPF debug pass on Windows, and the v0.0.1 ship. It was the build's "second deliverable" alongside the clipboard manager itself.

The log is **closed** as of v0.0.1. Subsequent activity lives in:
- **Commit history** on `main` — every maintenance fix the loop ships
- **GitHub Issues + PRs** — the user-facing record of what got reported and what shipped
- **GitHub Releases** — tagged build artifacts (`v*`)
- **`AUDIT.md`** — local-only forensic record of every heartbeat tick (gitignored; lives only on the agent host)

---

## 9. Repository conventions

- **Default branch:** `main`. The autonomous maintenance loop commits directly to `main`. Branch protection rules (CI-must-pass) live in the GitHub repo settings; the safety boundary is enforced there, not in the harness.
- **Feature branches:** Optional. The maintenance loop typically commits straight to `main`; longer-running work can use a topic branch.
- **Commits:** atomic and self-explanatory commit messages. No `wip`, no `fixed stuff`. First line ≤ 72 chars; body if needed.
- **No force-pushes** to `main`.

---

## 10. Open questions (do NOT block on these — make reasonable defaults)

These were not resolved with Bob before the run. The crew should pick a reasonable default and proceed; the stakeholder can iterate post-v0.

1. **Color semantics in Bob's existing clips** — unknown. Default: let user pick any color per clip; do not assign semantic meaning.
2. **Total clip count** — assumed ~200-500 organized clips. Persistence and grid rendering should handle 1000 clips without lag.
3. **Hotkey usage frequency** — assumed sparse. No need to optimize the hotkey UI for power users.
4. **Default cell color / theme** — light theme only for v0. Default cell color `#F5F5F5`.

---

## 11. Out of scope (explicit list — do not build)

- Encryption, master password, authentication of any kind
- Bulk paste, paste delay, scheduled paste
- Image-into-Word automation, image resizing, image markup
- Multi-sheet support, sheet picker, sheet tabs
- A-Z auto-sort
- Year planner, graphics editor, picture browser, screenshot tools
- Macro recording (with conditionals/waits/mouse — Inject is straight typing only)
- Cloud sync, settings sync
- Auto-update / installer
- Cross-platform support
- Localization (English only)
- Accessibility audit (light a11y is fine; full audit is post-v0)

---

## 12. Status

- v0 build: **shipped** (v0.0.1, 2026-04-26)
- Mode: **autonomous maintenance** (heartbeat-driven loop responding to GitHub issues)
- Stakeholder: Mike Wilson; primary user / field tester: Bob
