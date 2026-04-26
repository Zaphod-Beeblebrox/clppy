# Clppy v0 Implementation Progress

## Current Status: Core Complete, UI Ready for Windows Testing

**Last Updated:** 2026-04-25

## Completed

### Core Implementation (Clppy.Core)
- ✅ Data models: Clip, Settings, PasteMethod enum
- ✅ Persistence: EF Core 8 with SQLite, ClipRepository with CRUD + history rolloff
- ✅ Clipboard capture: Win32 AddClipboardFormatListener, format enumeration (UnicodeText, OEMText, RTF)
- ✅ Paste engines:
  - DirectPasteEngine: Win32 clipboard + SendInput for Ctrl+V
  - InjectPasteEngine: BuildKeystrokeSequence with VK_TAB/VK_RETURN
- ✅ Hotkey service: Win32 RegisterHotKey with conflict detection
- ✅ Settings service: Load/save from database
- ✅ HistoryBuffer: FIFO rolloff with configurable capacity

### Tests (Clppy.Core.Tests)
- ✅ 21 tests passing
- ✅ ClipTests: defaults, enum values, settings defaults
- ✅ HistoryRolloffTests: 21 items with capacity 20 → holds 20
- ✅ InjectEngineTests: keystroke sequence with tabs/newlines
- ✅ PasteRoutingTests: router selects correct engine
- ✅ PersistenceTests: CRUD, soft delete, settings roundtrip

### WPF UI Structure (Clppy.App)
- ✅ DI container: DependencyInjection.cs with EF Core 8 SQLite
- ✅ MainWindow: grid view, filter overlay (Ctrl+F), tray icon integration
- ✅ ClipEditorWindow: tab key inserts \t, color/hotkey capture
- ✅ TrayIconManager: notify icon with context menu
- ✅ ClipCellViewModel: binding, opacity for filter, history zone tint

### Project Configuration
- ✅ EF Core upgraded from 7.0.18 to 8.0.0
- ✅ All project references fixed
- ✅ Namespace conflicts resolved (Settings vs Models.Settings)

### Documentation
- ✅ README.md: build instructions, project structure, contributing
- ✅ LICENSE: MIT
- ✅ SPEC.md: authoritative specification
- ✅ PLAN.md: implementation phases
- ✅ LOG.md: agent run journal

## Remaining (Requires Windows Testing)

### UI Functionality
- ⏳ Context menus on clip cells (Paste, Edit, Delete, etc.)
- ⏳ Drag-drop to pin clips
- ⏳ Filter overlay fade effect
- ⏳ Double-click to open editor
- ⏳ Shift+click override paste method

### Integration Testing on Windows
- ⏳ Build Clppy.App.exe on Windows
- ⏳ Verify window opens with grid and history zone
- ⏳ Verify tray icon appears
- ⏳ Test clipboard capture from other apps
- ⏳ Test multi-format capture (RTF/HTML from Word)
- ⏳ Test Direct paste preserves formatting
- ⏳ Test Inject paste types into Notepad
- ⏳ Test global hotkeys
- ⏳ Test persistence across restarts

## Known Issues

### Warnings (Non-blocking)
- HotkeyService._windowHandle unused (Win32 integration partial)
- HotkeyService.HotkeyTriggered event unused (UI integration pending)

## Next Steps

1. Build on Windows and verify UI renders correctly
2. Test clipboard capture end-to-end
3. Test paste engines in real applications
4. Verify all 17 done criteria from SPEC.md §7
