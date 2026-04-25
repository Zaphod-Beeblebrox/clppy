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
