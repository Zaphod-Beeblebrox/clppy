# Clppy v0 Implementation Plan

---

## Phase 2 — Core implementation

### 2.1 Solution and project scaffolding

**Files to create:**
- `clppy/Clppy.sln`
- `clppy/src/Clppy.Core/Clppy.Core.csproj` (net8.0)
- `clppy/src/Clppy.App/Clppy.App.csproj` (net8.0-windows10.0.19041.0, WPF)
- `clppy/tests/Clppy.Core.Tests/Clppy.Core.Tests.csproj` (net8.0)

**Dependencies:**
- Clppy.Core: Microsoft.EntityFrameworkCore.Sqlite, Microsoft.EntityFrameworkCore.Design
- Clppy.App: Clppy.Core, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Hosting
- Clppy.Core.Tests: Clppy.Core, Microsoft.EntityFrameworkCore.Sqlite (in-memory), xunit, xunit.runner.visualstudio, Moq

**Public types / methods exposed:** None yet (infrastructure only)

**Inputs:** SPEC.md §5 (project structure), §6 (toolchain)

**Tests to add:** None at this stage

---

### 2.2 Data model (Clip and Settings entities)

**Files to create:**
- `src/Clppy.Core/Models/Clip.cs`
- `src/Clppy.Core/Models/PasteMethod.cs`
- `src/Clppy.Core/Models/Settings.cs`

**Public types / methods exposed:**
```csharp
public enum PasteMethod { Direct, Inject }

public class Clip {
    public Guid Id { get; set; }
    public int? Row { get; set; }
    public int? Col { get; set; }
    public int? HistoryIndex { get; set; }
    public string? Label { get; set; }
    public string? PlainText { get; set; }
    public byte[]? Rtf { get; set; }
    public byte[]? Html { get; set; }
    public byte[]? PngImage { get; set; }
    public PasteMethod Method { get; set; }
    public string? ColorHex { get; set; }
    public string? Hotkey { get; set; }
    public bool Pinned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class Settings {
    public int HistoryRows { get; set; } = 5;
    public int HistoryCols { get; set; } = 4;
    public int CellWidthPx { get; set; } = 140;
    public int CellHeightPx { get; set; } = 32;
    public int InjectKeystrokeDelayMs { get; set; } = 5;
    public string DefaultColorHex { get; set; } = "#F5F5F5";
}
```

**Inputs:** SPEC.md §4.1, §4.2

**Tests to add:**
- `tests/Clppy.Core.Tests/ClipTests.cs`: Clip property defaults, enum values

---

### 2.3 Persistence layer (EF Core DbContext and repository)

**Files to create:**
- `src/Clppy.Core/Persistence/ClppyDbContext.cs`
- `src/Clppy.Core/Persistence/IClipRepository.cs`
- `src/Clppy.Core/Persistence/ClipRepository.cs`
- `src/Clppy.Core/Persistence/Migrations/` (initial migration)

**Public types / methods exposed:**
```csharp
public class ClppyDbContext : DbContext {
    public DbSet<Clip> Clips { get; set; }
    public DbSet<Settings> Settings { get; set; }
}

public interface IClipRepository {
    Task<Clip?> GetByIdAsync(Guid id);
    Task<IEnumerable<Clip>> GetAllAsync();
    Task<Clip> AddAsync(Clip clip);
    Task UpdateAsync(Clip clip);
    Task DeleteAsync(Guid id); // soft delete (sets DeletedAt)
    Task<Settings> GetSettingsAsync();
    Task SaveSettingsAsync(Settings settings);
    Task<IEnumerable<Clip>> GetHistoryZoneAsync(int maxRows, int maxCols);
    Task<IEnumerable<Clip>> GetPinnedClipsAsync();
}
```

**Inputs:** Clip, Settings models; SPEC.md §4.3 (SQLite at %APPDATA%\Clppy\clppy.db, backup on startup)

**Tests to add:**
- `tests/Clppy.Core.Tests/PersistenceTests.cs`: CRUD operations, soft delete, history zone filtering

---

### 2.4 Clipboard capture service

**Files to create:**
- `src/Clppy.Core/Clipboard/IClipboardCapture.cs`
- `src/Clppy.Core/Clipboard/ClipboardCaptureService.cs`
- `src/Clppy.Core/Clipboard/ClipboardFormatHandler.cs`

**Public types / methods exposed:**
```csharp
public interface IClipboardCapture {
    event Action<Clip>? ClipCaptured;
    void StartListening();
    void StopListening();
}

public class ClipboardCaptureService : IClipboardCapture {
    // Implements StartListening via AddClipboardFormatListener
    // On change: EnumClipboardFormats, capture text/RTF/HTML/PNG
    // Creates Clip with Pinned=false, HistoryIndex=0, Method=Direct
    // Pushes to history FIFO, drops oldest if over capacity
}
```

**Inputs:** IClipRepository (to save captured clips); SPEC.md §3.9 (capture pipeline, FIFO rolloff, suppress self-capture)

**Tests to add:**
- `tests/Clppy.Core.Tests/HistoryRolloffTests.cs`: Add 21 clips with capacity 20 → oldest dropped

---

### 2.5 Paste engines (Direct and Inject)

**Files to create:**
- `src/Clppy.Core/Paste/IPasteEngine.cs`
- `src/Clppy.Core/Paste/DirectPasteEngine.cs`
- `src/Clppy.Core/Paste/InjectPasteEngine.cs`
- `src/Clppy.Core/Paste/PasteRouter.cs`

**Public types / methods exposed:**
```csharp
public interface IPasteEngine {
    Task PasteAsync(Clip clip, IntPtr targetWindow);
}

public class DirectPasteEngine : IPasteEngine {
    // Sets system clipboard to clip's multi-format payload
    // Sends Ctrl+V via SendInput
}

public class InjectPasteEngine : IPasteEngine {
    // Iterates PlainText char-by-char
    // \t → VK_TAB, \n → VK_RETURN, others → KEYEVENTF_UNICODE
    // Does NOT touch system clipboard
    public List<Keystroke> BuildKeystrokeSequence(string text); // testable
}

public class PasteRouter {
    public IPasteEngine GetEngine(PasteMethod method);
    public IPasteEngine GetEngine(Clip clip, bool forceOpposite); // forceOpposite for Shift+click
}
```

**Keystroke helper type:**
```csharp
public class Keystroke {
    public uint VirtualKeyCode { get; set; }
    public ushort UnicodeChar { get; set; }
    public bool IsUnicode { get; set; }
}
```

**Inputs:** Clip model; SPEC.md §2.4 (paste engines), §6.5 (PasteRouter tests, Inject keystroke test)

**Tests to add:**
- `tests/Clppy.Core.Tests/PasteRoutingTests.cs`: Router selects correct engine, honors override
- `tests/Clppy.Core.Tests/InjectEngineTests.cs`: Keystroke sequence includes VK_TAB for \t, VK_RETURN for \n

---

### 2.6 Global hotkey service

**Files to create:**
- `src/Clppy.Core/Hotkeys/IHotkeyService.cs`
- `src/Clppy.Core/Hotkeys/HotkeyService.cs`
- `src/Clppy.Core/Hotkeys/HotkeyRegistration.cs`

**Public types / methods exposed:**
```csharp
public class HotkeyRegistration {
    public Guid ClipId { get; set; }
    public string ModifierKeys { get; set; } // e.g., "Ctrl+Alt"
    public char Key { get; set; }
    public int Id { get; set; } // Win32 hotkey ID
}

public interface IHotkeyService {
    event Action<Guid>? HotkeyTriggered; // raises ClipId
    bool RegisterHotkey(HotkeyRegistration reg);
    void UnregisterHotkey(Guid clipId);
    void UnregisterAll();
    bool IsHotkeyAvailable(string modifierKeys, char key);
}

public class HotkeyService : IHotkeyService, IDisposable {
    // Uses RegisterHotKey/UnregisterHotKey Win32
    // Track conflicts, raise event on trigger
}
```

**Inputs:** IClipRepository (to look up clip by hotkey-triggered ID); SPEC.md §3.8 (global hotkeys, conflict detection)

**Tests to add:** None (Win32 interop, tested via integration)

---

### 2.7 Settings service

**Files to create:**
- `src/Clppy.Core/Settings/ISettingsService.cs`
- `src/Clppy.Core/Settings/SettingsService.cs`

**Public types / methods exposed:**
```csharp
public interface ISettingsService {
    Settings Current { get; }
    Task LoadAsync();
    Task SaveAsync();
    int HistoryZoneRowCount => Current.HistoryRows;
    int HistoryZoneColumnCount => Current.HistoryCols;
    int CellWidth => Current.CellWidthPx;
    int CellHeight => Current.CellHeightPx;
    int InjectDelay => Current.InjectKeystrokeDelayMs;
}
```

**Inputs:** IClipRepository (Settings table), SPEC.md §4.2

**Tests to add:** None (simple wrapper)

---

## Phase 3 — UI implementation

### 3.1 Application startup and DI container

**Files to create:**
- `src/Clppy.App/App.xaml`
- `src/Clppy.App/App.xaml.cs`
- `src/Clppy.App/DependencyInjection.cs`

**Public types / methods exposed:**
```csharp
public static class DependencyInjection {
    public static IServiceCollection AddClppyServices(this IServiceCollection services);
}
```

**Implementation:**
- Configure IClipRepository → ClipRepository (with DbContext)
- Configure IClipboardCapture → ClipboardCaptureService
- Configure IPasteEngine implementations → DirectPasteEngine, InjectPasteEngine
- Configure PasteRouter
- Configure IHotkeyService → HotkeyService
- Configure ISettingsService → SettingsService
- Register MainWindow, ClipEditorWindow

**Inputs:** All Core services

**Tests to add:** None

---

### 3.2 Main window shell and tray icon

**Files to create:**
- `src/Clppy.App/MainWindow.xaml`
- `src/Clppy.App/MainWindow.xaml.cs`
- `src/Clppy.App/Tray/TrayIconManager.cs`
- `src/Clppy.App/Views/SettingsWindow.xaml`
- `src/Clppy.App/Views/SettingsWindow.xaml.cs`

**Public types / methods exposed:**
```csharp
public class MainWindow : Window {
    // Contains grid placeholder, filter overlay placeholder
    // Handles X button → Hide to tray
    // Handles Alt+F4 → Confirm exit
}

public class TrayIconManager : IDisposable {
    public void Initialize();
    public void Show();
    public void Hide();
    public void ShowContextMenu();
}
```

**Inputs:** ISettingsService (for default colors), IClipboardCapture (to start listening)

**Tests to add:** None

---

### 3.3 Grid view and cell rendering

**Files to create:**
- `src/Clppy.App/Views/ClipGridView.xaml`
- `src/Clppy.App/Views/ClipGridView.xaml.cs`
- `src/Clppy.App/ViewModels/MainViewModel.cs`
- `src/Clppy.App/Views/ClipCellControl.xaml`
- `src/Clppy.App/Views/ClipCellControl.xaml.cs`

**Public types / methods exposed:**
```csharp
public class MainViewModel : INotifyPropertyChanged {
    public ObservableCollection<ClipCellViewModel> Cells { get; }
    public int GridRows { get; } // ~30
    public int GridCols { get; } // ~9
    public bool IsFilterActive { get; set; }
    public string FilterText { get; set; }
}

public class ClipCellViewModel : INotifyPropertyChanged {
    public Guid ClipId { get; set; }
    public string? Label { get; }
    public string? PreviewText { get; }
    public string? ColorHex { get; }
    public PasteMethod DefaultMethod { get; }
    public bool IsInHistoryZone { get; }
    public double Opacity { get; set; } // 1.0 or 0.25 when filtered
}
```

**Implementation details:**
- Use ItemsControl with uniform grid panel
- History zone: first N×M cells (default 4×5) with yellow tint
- ClipCellControl renders: label, truncated preview, engine icon (▶ or ⌨)
- Empty cells render as blank containers

**Inputs:** MainViewModel (from IClipRepository), ISettingsService

**Tests to add:** None (UI)

---

### 3.4 Context menus and right-click actions

**Files to create:**
- `src/Clppy.App/Views/ClipContextMenu.xaml`
- `src/Clppy.App/Views/EmptyCellContextMenu.xaml`
- `src/Clppy.App/ViewModels/ClipContextMenuViewModel.cs`

**Public types / methods exposed:**
```csharp
public class ClipContextMenuViewModel {
    public ClipCellViewModel TargetClip { get; }
    public ICommand PasteCommand { get; }
    public ICommand PasteAsTextCommand { get; }
    public ICommand PasteAsKeystrokesCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand SetColorCommand { get; }
    public ICommand SetHotkeyCommand { get; }
    public ICommand SetDefaultMethodCommand { get; }
    public ICommand MoveToHistoryCommand { get; }
    public ICommand DeleteCommand { get; }
}

public class EmptyCellContextMenuViewModel {
    public int Row { get; }
    public int Col { get; }
    public ICommand NewClipFromClipboardCommand { get; }
    public ICommand PasteHereCommand { get; }
}
```

**Inputs:** PasteRouter, IClipRepository, IClipboardCapture

**Tests to add:** None (UI)

---

### 3.5 Clip editor dialog

**Files to create:**
- `src/Clppy.App/ClipEditorWindow.xaml`
- `src/Clppy.App/ClipEditorWindow.xaml.cs`
- `src/Clppy.App/ViewModels/ClipEditorViewModel.cs`

**Public types / methods exposed:**
```csharp
public class ClipEditorViewModel : INotifyPropertyChanged {
    public string Label { get; set; }
    public string PlainText { get; set; }
    public PasteMethod DefaultMethod { get; set; }
    public string ColorHex { get; set; }
    public string? HotkeyDisplay { get; set; }
    public bool HasRichTextFormats { get; }
    public bool HasImage { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
}
```

**Implementation details:**
- Tab key in PlainText TextBox inserts `\t` (not focus change)
- Right-click in PlainText shows menu: "Insert Tab", "Insert Newline"
- Hotkey capture button listens for key combo
- Conflict check via IHotkeyService.IsHotkeyAvailable

**Inputs:** Clip model, IHotkeyService, IClipRepository

**Tests to add:** None (UI)

---

### 3.6 Filter overlay

**Files to create:**
- `src/Clppy.App/Views/FilterOverlayControl.xaml`
- `src/Clppy.App/Views/FilterOverlayControl.xaml.cs`

**Public types / methods exposed:**
```csharp
public class FilterOverlayControl : UserControl {
    public static readonly DependencyProperty FilterTextProperty;
    public string FilterText { get; set; }
}
```

**Implementation details:**
- Slides in from top (Grid overlay with TranslateTransform animation)
- Textbox filters cells (MainViewModel.FilterText updates ClipCellViewModel.Opacity)
- Click outside or Esc dismisses

**Inputs:** MainViewModel

**Tests to add:** None (UI)

---

### 3.7 Drag-and-drop (clip movement and pinning)

**Files to create:**
- `src/Clppy.App/Behaviors/ClipDragDropBehavior.cs`

**Public types / methods exposed:**
```csharp
public class ClipDragDropBehavior : Behavior<ClipCellControl> {
    // On drag start: capture ClipId
    // On drop: update Clip.Row/Col via IClipRepository
    // If dragged from history zone → set Pinned=true
    // If dragged into history zone → set Pinned=false
}
```

**Inputs:** IClipRepository, MainViewModel

**Tests to add:** None (UI)

---

### 3.8 Keyboard bindings (Ctrl+F, Esc, Shift+click)

**Files to create:**
- `src/Clppy.App/Behaviors/KeyboardBindingBehavior.cs` (or integrate into MainWindow)

**Public types / methods exposed:**
```csharp
// In MainWindow.xaml.cs:
private void HandleKeyDown(KeyEventArgs e) {
    // Ctrl+F → show filter overlay
    // Esc → hide overlay or hide window to tray
}

// In ClipCellControl:
// Shift+left-click → invoke PasteRouter with forceOpposite=true
```

**Inputs:** MainViewModel (filter), MainWindow (tray hide)

**Tests to add:** None (UI)

---

### 3.9 Integration: Clipboard capture → UI update

**Files to modify:**
- `src/Clppy.App/MainWindow.xaml.cs` (subscribe to IClipboardCapture.ClipCaptured)
- `src/Clppy.App/ViewModels/MainViewModel.cs` (refresh cells on new clip)

**Public types / methods exposed:** None new

**Inputs:** IClipboardCapture, IClipRepository

**Tests to add:** None (integration)

---

## Cross-cutting conventions

### Dependency injection
- **Framework:** Microsoft.Extensions.DependencyInjection
- **Registration:** Centralized in `DependencyInjection.cs` (Phase 3.1)
- **Lifetime:** Services singleton (ClipboardCaptureService, HotkeyService, SettingsService), Repository scoped per operation or singleton (singleton for simplicity)

### Naming conventions
- **Interfaces:** Prefix with `I` (IClipRepository, IPasteEngine)
- **Services:** Suffix with `Service` (ClipboardCaptureService, HotkeyService)
- **ViewModels:** Suffix with `ViewModel` (MainViewModel, ClipEditorViewModel)
- **Views:** Suffix with `Window` or `Control` (MainWindow, ClipCellControl)

### Async/await
- All repository and service methods returning data use `Task<T>` or `Task`
- UI event handlers use `async void` (WPF pattern), call async services

### Win32 interop
- **Location:** `src/Clppy.App/Interop/` for P/Invoke signatures
- **SendInput:** Used by DirectPasteEngine (Ctrl+V) and InjectPasteEngine (keystrokes)
- **RegisterHotKey:** Used by HotkeyService
- **Clipboard:** Use System.Windows.Clipboard for managed access, but P/Invoke for low-level capture (AddClipboardFormatListener, EnumClipboardFormats, GetClipboardData)

### Configuration paths
- **Database:** `%APPDATA%\Clppy\clppy.db`
- **Backup:** `%APPDATA%\Clppy\clppy.db.bak`
- **Create directory on startup if not exists**

### Error handling
- **Hotkey conflicts:** Show error in clip editor, prevent save
- **Database errors:** Log and show MessageBox, allow graceful degradation
- **Clipboard access failures:** Log, skip capture (don't crash)

### Testing
- **Framework:** xunit
- **In-memory SQLite:** Use `:memory:` or temp file with `Database.EnsureCreated()`
- **Moq:** For IClipboardCapture, IPasteEngine in UI-adjacent tests (if any)
- **Coverage target:** Core logic (models, repository, paste engines, history rolloff)

---

## Phase dependency order (summary)

**Phase 2:**
1. Scaffolding → 2. Data model → 3. Persistence → 4. Clipboard capture → 5. Paste engines → 6. Hotkey service → 7. Settings service

**Phase 3:**
1. DI container → 2. Main window + tray → 3. Grid + cells → 4. Context menus → 5. Clip editor → 6. Filter overlay → 7. Drag-drop → 8. Keyboard bindings → 9. Integration
