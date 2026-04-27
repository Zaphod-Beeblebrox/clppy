# Clppy Agent Run Journal

This log records the autonomous agent crew's progress building Clppy v0. Per `SPEC.md` §8, this is the second deliverable of this exercise — the artifact (clipboard manager) being the first.

Format: terse bullet entries. Forensic reconstruction over literary quality.

---

## Pre-run setup

- **2026-04-25** — Repository initialized. Spec, license, README, gitignore, this log committed. Stakeholder: Mike Wilson. Crew: not yet wired.

(Crew configuration, run start, per-phase entries, and run end will follow.)

---

## Phase 1: Scaffolding and Architecture

**Run start timestamp:** 2026-04-25 09:42 UTC  
**Crew configuration:** Architect model (planning/review), Worker model (code generation)  
**Duration:** ~10 minutes

**Phase 1 summary:**
Scaffolded solution structure with three projects: Clppy.Core (net8.0 class library), Clppy.Core.Tests (net8.0 xunit test project), and Clppy.App (net8.0-windows WPF application). Created base solution file, added project references, and verified builds. Core and test projects compile successfully on Linux; WPF project requires Windows per SPEC.md §6.1. Generated PLAN.md with detailed Phase 2 (7 core tasks) and Phase 3 (9 UI tasks) implementation breakdown.

**Build verification:**
- `dotnet build src/Clppy.Core/Clppy.Core.csproj -c Release` → Exit code 0, "Build succeeded. 0 Warning(s) 0 Error(s)"
- `dotnet build tests/Clppy.Core.Tests/Clppy.Core.Tests.csproj -c Release` → Exit code 0, "Build succeeded. 0 Warning(s) 0 Error(s)"
- `dotnet test tests/Clppy.Core.Tests/Clppy.Core.Tests.csproj` → "Passed! - Failed: 0, Passed: 1, Total: 1"
- `dotnet build -c Release` (full solution) → Exit code 1 on Linux (expected; WPF requires Windows/EnableWindowsTargeting per SPEC.md §6.1)

**Anomalies:**
- WPF project cannot build on Linux without EnableWindowsTargeting flag; this is expected behavior per spec. Clippy.App.csproj updated with `<EnableWindowsTargeting>true</EnableWindowsTargeting>` for cross-platform build compatibility when possible.

**Phase 1 completion timestamp:** 2026-04-25 09:52 UTC

---

## Phase 2: Core implementation

**Run start timestamp:** 2026-04-25 ~10:55 UTC (third attempt; first two crashed on infrastructure issues)
**Architect:** Qwen3.5-122B-A10B-Q4 @ Strix supervisor (192.168.21.4:8080), thinking ON
**Worker:** Qwen3-Coder-30B-A3B-Instruct-Q4 @ Strix worker (192.168.21.4:8081), `/no_think`
**Duration:** ~40 minutes (worker tasks); architect review crashed before completion

### What was built (per PLAN.md §2.2-2.7)

- **§2.2 Data model**: `Models/Clip.cs`, `Models/PasteMethod.cs`, `Models/Settings.cs`
- **§2.3 Persistence**: `Persistence/ClppyDbContext.cs`, `Persistence/IClipRepository.cs`, `Persistence/ClipRepository.cs`
- **§2.4 Clipboard capture**: `Clipboard/IClipboardCapture.cs`, `Clipboard/ClipboardCaptureService.cs`, `Clipboard/ClipboardFormatHandler.cs`, `Clipboard/HistoryBuffer.cs`
- **§2.5 Paste engines**: `Paste/IPasteEngine.cs`, `Paste/Keystroke.cs`, `Paste/DirectPasteEngine.cs`, `Paste/InjectPasteEngine.cs`, `Paste/PasteRouter.cs`
- **§2.6 Hotkeys**: `Hotkeys/IHotkeyService.cs`, `Hotkeys/HotkeyService.cs`, `Hotkeys/HotkeyRegistration.cs`
- **§2.7 Settings service**: `Settings/ISettingsService.cs`, `Settings/SettingsService.cs`
- **Tests**: `ClipTests.cs`, `PersistenceTests.cs`, `HistoryRolloffTests.cs`, `PasteRoutingTests.cs`, `InjectEngineTests.cs`

### Infrastructure failures (run 1 and run 2)

- **Run 1** crashed on first task: worker context overflow (16K per slot due to `--parallel 4` on `-c 65536`). Fix: changed worker to `--parallel 1` for full 64K context.
- **Run 2** crashed on persistence task: hit CrewAI default `max_iter=25` ceiling. Fix: bumped both agents to `max_iter=50`, raised worker context to 128K (`-c 131072`).
- **Run 3** completed all 6 worker tasks but architect review task crashed with `400: Assistant response prefill is incompatible with enable_thinking`. CrewAI's max-iter recovery path uses assistant-message prefill, which Qwen3.5's chat template rejects when thinking mode is on. Architect review was abandoned; human stakeholder verified manually.

### Bugs introduced by agents (human-corrected post-run)

The worker did not actually run `dotnet test` as a per-task gate (or did and ignored failures during max_iter recovery). All five issues below would have been caught by an enforced test gate:

1. **Namespace collision (`Settings`)** — service placed in `namespace Clppy.Core.Settings`, conflicting with `Clppy.Core.Models.Settings` type via C#'s parent-namespace resolution. Fix: renamed service namespace to `Clppy.Core.Configuration`. Root cause: the Phase 2 task description specified the conflicting namespace; lesson encoded for future task descriptions.
2. **`HistoryBuffer.Add` order reversed** — appended to end instead of inserting at front, contradicting the "newest at index 0" requirement. Fix: `Insert(0, clip)` + remove from end.
3. **`int`/`uint` type mismatch in `InjectEngineTests`** — `Assert.Equal(0x09, keystrokes[1].VirtualKeyCode)` failed overload resolution because `0x09` is `int` and `VirtualKeyCode` is `uint`. xUnit picked a DateTime overload. Fix: `0x09u` literal suffix.
4. **`PersistenceTests` had no schema bootstrap** — in-memory SQLite connection opened but `Database.EnsureCreated()` never called, so all 6 persistence tests failed with "no such table: Clips". Fix: added `EnsureCreated()` call in test constructor + promoted connection to a field for lifetime safety.
5. **`Settings` entity had wrong primary key** — worker placed `[Key]` on `HistoryRows`, making the value field the PK. Plus `SaveSettingsAsync` used `Update()` on a non-existent row, throwing on first save. Fix: added proper `Id` PK with default 1; rewrote `SaveSettingsAsync` as upsert pattern.

### Test verification (after fixes)

- `dotnet build src/Clppy.Core/Clppy.Core.csproj` → 0 errors, 0 warnings.
- `dotnet test tests/Clppy.Core.Tests/Clppy.Core.Tests.csproj` → **Passed! Failed: 0, Passed: 21, Skipped: 0, Total: 21, Duration: 443 ms**.

### Lessons for future phase task descriptions

- Stronger test-gate language: "if `dotnet test` reports any failure, STOP — do not move past it under any circumstance, including max-iter recovery."
- Avoid namespace-vs-type collisions: don't create a sub-namespace whose final segment matches a sibling type name.
- For EF Core entity tests: include `Database.EnsureCreated()` in the test class constructor when using in-memory SQLite, and hold the `SqliteConnection` as a field.
- For singleton EF entities (like Settings): require an explicit `Id` PK and upsert pattern in the repository.
- The CrewAI 1.12 `max_iter` fallback uses assistant-message prefill, which conflicts with Qwen3.5 thinking mode. Resolve before Phase 3 — either patch `llama_compat.py` to strip prefill, or run the architect with thinking-off for review tasks.

**Phase 2 completion timestamp:** 2026-04-25 ~12:00 UTC (worker runs done ~11:40; manual fixes + verification through ~12:00)

---

## Phase 3: UI implementation — pivot to heartbeat harness

**Run start timestamp:** 2026-04-25 ~16:00 UTC
**Driver:** new heartbeat-driven ReAct harness (`~/code/agent-loop/`) — NOT CrewAI
**Supervisor:** Qwen3.5-122B-A10B-Q4 @ Strix supervisor (192.168.21.4:8080), thinking ON
**Worker:** none (single-LLM v0; supervisor handles plan + execute)
**Working dir:** `~/code/clppy/` on Optiplex (sacrificial agent host)
**Cadence:** systemd-user timer firing every ~10 minutes; 15-action budget per tick

### Why the pivot away from CrewAI

CrewAI's `force_final_answer` recovery prompt overrode task instructions, leading to passing-but-broken outputs in Phase 2 (the five bugs documented above). The architect-review crash on assistant-message prefill (Qwen3.5 thinking-mode incompatibility) was the proximate trigger; the deeper reason is the framework took control away from the goal text. Replaced with a thin heartbeat harness that just wakes the model up, executes one action, logs the result, and yields back to the model — no opinionated recovery, no prompt overrides.

### What the loop built

Over an overnight run (27 ticks), the agent shipped 5 substantive commits implementing Phase 3 from PLAN.md: WPF MainWindow with grid, ClipEditor dialog, drag-to-pin, Tray icon (NotifyIcon), filter overlay, hotkey wiring, App.xaml DI bootstrap. Agent called `done` and the loop wrote `PHASE_DONE`.

### Bugs surfaced during Windows verification (human-corrected)

The agent host is Linux; WPF cannot build there. The agent had no compile feedback on UI code — it shipped what looked right but couldn't be verified. Eight rounds of fix-build-fail-fix on a Windows laptop:

1. csproj path bug: `..\src\Clppy.Core\` from `src/Clppy.App` resolved to `src/src/...`. Fixed to `..\Clppy.Core\`.
2. `MC3072`: `MouseDoubleClick` event on a `Border` (Border inherits Decorator, not Control). Folded into `MouseLeftButtonUp` with `e.ClickCount == 2`.
3. `Margin = 10` on a WPF dialog (needs `Thickness`). Fixed.
4. Stray `;` in collection initializer (`}};` → `}}`). Fixed.
5. `UseWindowsForms` missing in csproj — needed for `NotifyIcon`. Added.
6. `Icon.FromHandle(bitmap.GetHicon())` instead of `new Icon(bitmap)`. `ToolTipIcon.Info` not `ToolTipInfo.Info`. `ContextMenuStrip.Show(Cursor.Position)` not `Show(notifyIcon, e.Location)`. `Application.Current.Shutdown()` not static `Application.Shutdown()`.
7. App.xaml `StartupUri="MainWindow.xaml"` conflicted with DI lifetime management. Removed; DI manages MainWindow.
8. Runtime crash: SQLite schema not bootstrapped on first launch. Added `EnsureCreatedAsync()` in OnStartup; wrapped in try/catch with MessageBox to surface async-void exceptions.

Lesson encoded for future loops (harness-level): **agent host must have build-verification parity with the project target**. If you can't compile, you can't validate, and the agent ships latent bugs that only surface on the target platform. Saved as a feedback memory in the operator's notes.

### CI + release wiring (human, not agent)

- `.github/workflows/build.yml`: restore + build (Release) + test on `windows-latest`, triggers on push/PR to main.
- `.github/workflows/release.yml`: on `v*` tag push, `dotnet publish` produces single-file self-contained `Clppy.App.exe`; `gh release create` attaches it as a Pre-release.

### v0.0.1 published

**2026-04-26** — `Clppy v0.0.1` Pre-release on GitHub. All §7 done criteria verified on a Windows laptop. End-to-end: clipboard capture, history rolloff, Direct + Inject paste, drag-to-pin, tray, filter, hotkey registration. Downloadable `.exe` is the working artifact.

### Phase 3 completion timestamp

**2026-04-26 ~13:00 UTC** (build verification + CI/release wiring + v0.0.1 published)

---

## Maintenance handoff

**2026-04-26 mid-day** — Project handed to the heartbeat loop in *perpetual issue-driven maintenance* mode:

- `agent-loop/projects/clppy/config.toml` rewritten: each tick reads open GH issues, picks one (priority/high → oldest), comments pickup, fixes, commits to main, ends naturally. Next tick checks CI on prior commits, comments closure or failure.
- `auto_commit_branch = "main"` (was `agent-loop/clppy-v0`); main-branch commit guard removed from harness `tools.py`. Safety boundary moved to GH branch protection + the sacrificial Optiplex (owner can pull power / delete repo).
- Auto-tag flow added: on issue closure with green CI, agent reads latest tag, bumps patch (`v0.0.1 → v0.0.2`), tags HEAD, pushes the tag (firing the release workflow), comments the release URL on the issue, closes. Minor/major bumps remain a human decision.

### v0.0.2

**2026-04-26 evening** — First true autonomous cycle: Bob filed Issue #3 ("Runs, but doesn't show clips"). Agent picked it up, diagnosed missing `_clipboardCapture.StartListening()` call in MainWindow, committed the fix, CI green, tagged `v0.0.2`, posted the release URL, closed the issue. Round-trip "filed → fix on main": ~12 min. Filed → downloadable: ~25 min including release workflow.

---

## Harness defects observed and fixed

- **PROGRESS.md collision**: agent overwrote harness's tick journal via `write_file`. Fixed: `write_file` refuses harness-owned files; system prompt declares ownership contract.
- **Missing git push**: `tools.git_commit` only ran `git commit`, not `git push`. Agent thought it shipped fixes that were sitting local-only. Fixed: `git_commit` now pushes.
- **PROGRESS.md context bloat**: file grew to 467KB / ~117k tokens over 118 maintenance ticks; loaded into prompt every tick; eventually exceeded Strix's `n_ctx`, killing the loop with HTTP 400. Fixed (2026-04-27): bifurcated the journal — `AUDIT.md` (full forensic, never prompt-loaded) + `PROGRESS.md` (last 5 ticks, regenerated each tick). Added `idle` action so perpetual loops can end ticks early without burning the action budget.
- **SPEC/LOG/PLAN drift**: docs froze at the v0 build moment and continued to be auto-loaded into every prompt. Fixed (2026-04-27): SPEC.md governance bits updated to current reality, this LOG.md completed and closed, PLAN.md deleted (build-time artifact), `read_files` trimmed to PROGRESS.md only.

---

## Log closed

This journal is **closed**. Subsequent activity lives in `git log`, GitHub Issues / PRs / Releases, and the harness `AUDIT.md` (local-only on the agent host).
