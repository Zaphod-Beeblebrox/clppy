using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Clppy.Core.Clipboard;
using Clppy.Core.Hotkeys;
using Clppy.Core.Logging;
using Clppy.Core.Models;
using Clppy.Core.Paste;
using Clppy.Core.Persistence;
using Clppy.Core.Settings;
using Clppy.App.Views;
using Models = Clppy.Core.Models;

namespace Clppy.App;

public partial class MainWindow : Window
{
    private readonly IClipRepository _clipRepository;
    private readonly IClipboardCapture _clipboardCapture;
    private readonly PasteRouter _pasteRouter;
    private readonly IHotkeyService _hotkeyService;
    private readonly ISettingsService _settingsService;
    private readonly IFileLogger _logger;
    private readonly TrayIconManager _trayIcon;
    private Clip? _draggedClip;
    private Point _dragStartPoint;

    public ObservableCollection<ClipCellViewModel> Clips { get; } = new();

    public MainWindow(IClipRepository clipRepository,
                      IClipboardCapture clipboardCapture,
                      PasteRouter pasteRouter,
                      IHotkeyService hotkeyService,
                      ISettingsService settingsService,
                      IFileLogger logger)
    {
        _clipRepository = clipRepository;
        _clipboardCapture = clipboardCapture;
        _pasteRouter = pasteRouter;
        _hotkeyService = hotkeyService;
        _settingsService = settingsService;
        _logger = logger;
        
        _logger.Log("Application starting up");

        InitializeComponent();
        
        _logger.Log("Component initialized");
        ClipGrid.ItemsSource = Clips;
        _trayIcon = new TrayIconManager(this);
        _trayIcon.Initialize();
        _logger.Log("Tray icon initialized");

        _clipboardCapture.ClipCaptured += OnClipCaptured;
        _hotkeyService.HotkeyTriggered += OnHotkeyTriggered;
        _logger.Log("Event handlers registered");
        
        try
        {
            _clipboardCapture.StartListening();
            _logger.Log("Clipboard listener started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to start clipboard capture", ex);
            MessageBox.Show($"Failed to start clipboard capture: {ex.Message}\n\nClipboard monitoring will not work.", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        _logger.Log("Loading clips from database");
        _ = LoadClipsAsync();
    }

    private async Task LoadClipsAsync()
    {
        try
        {
            _logger.Log($"LoadClipsAsync: Starting, Clips.Count before clear = {Clips.Count}");
            Clips.Clear();

            await _settingsService.LoadAsync();
            var settings = _settingsService.Current;
            var historyRows = settings.HistoryRows;
            var historyCols = settings.HistoryCols;
            var totalRows = 30;
            var totalCols = 9;

            _logger.Log($"LoadClipsAsync: Settings loaded - HistoryRows={historyRows}, HistoryCols={historyCols}");
            
            var allClips = await _clipRepository.GetAllAsync();
            _logger.Log($"LoadClipsAsync: Retrieved {allClips.Count()} clips from database");
            
            var pinnedClips = allClips.Where(c => c.Pinned && c.Row.HasValue && c.Col.HasValue).ToList();
            var historyClips = allClips.Where(c => !c.Pinned).OrderBy(c => c.HistoryIndex).ToList();

            _logger.Log($"LoadClipsAsync: Pinned={pinnedClips.Count}, History={historyClips.Count}");

            for (int row = 0; row < totalRows; row++)
            {
                for (int col = 0; col < totalCols; col++)
                {
                    var clip = pinnedClips.FirstOrDefault(c => c.Row == row && c.Col == col);
                    var isInHistory = row < historyRows && col < historyCols;
                    
                    if (clip == null && isInHistory)
                    {
                        var historyIndex = historyClips.Count - 1 - Math.Min(historyClips.Count - 1, row * historyCols + col);
                        if (historyIndex >= 0 && historyIndex < historyClips.Count)
                        {
                            clip = historyClips[historyIndex];
                        }
                    }

                    if (clip != null)
                    {
                        Clips.Add(new ClipCellViewModel(clip, isInHistory, settings));
                    }
                    else
                    {
                        Clips.Add(new ClipCellViewModel(null, isInHistory, settings));
                    }
                }
            }
            _logger.Log($"LoadClipsAsync: Complete, Clips.Count = {Clips.Count}");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load clips", ex);
            MessageBox.Show($"Failed to load clips: {ex.Message}\n\nPlease check that the database is accessible.", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnClipCaptured(Clip clip)
    {
        _logger.Log($"OnClipCaptured: Clip captured - Text length={clip.PlainText?.Length ?? 0}");
        try
        {
            Dispatcher.Invoke(async () => await LoadClipsAsync());
            _logger.Log("OnClipCaptured: Reload complete");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error processing captured clip", ex);
            MessageBox.Show($"Error processing captured clip: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnHotkeyTriggered(Guid clipId)
    {
        _logger.Log($"OnHotkeyTriggered: Hotkey triggered for clip {clipId}");
        Dispatcher.Invoke(async () =>
        {
            try
            {
                var clip = await _clipRepository.GetByIdAsync(clipId);
                if (clip != null)
                {
                    _logger.Log($"OnHotkeyTriggered: Found clip, using engine {clip.Method}");
                    var engine = _pasteRouter.GetEngine(clip, false);
                    await engine.PasteAsync(clip, IntPtr.Zero);
                    _logger.Log("OnHotkeyTriggered: Paste complete");
                }
                else
                {
                    _logger.Log($"OnHotkeyTriggered: Clip not found: {clipId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Hotkey paste failed", ex);
                MessageBox.Show($"Hotkey paste failed: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
    }

    private void ClipCell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _logger.Log("ClipCell_MouseLeftButtonUp: Click event received");
        try
        {
            var vm = (ClipCellViewModel)((Border)sender).DataContext;
            _logger.Log($"ClipCell_MouseLeftButtonUp: DataContext clip is null = {vm.Clip == null}");
            if (vm.Clip == null) 
            {
                _logger.Log("ClipCell_MouseLeftButtonUp: No clip, returning");
                return;
            }

            if (e.ClickCount == 2)
            {
                _logger.Log("ClipCell_MouseLeftButtonUp: Double-click, opening editor");
                var editor = new ClipEditorWindow(vm.Clip, _clipRepository, _hotkeyService);
                if (editor.ShowDialog() == true && editor.ResultClip != null)
                {
                    _ = _clipRepository.UpdateAsync(editor.ResultClip);
                    _ = LoadClipsAsync();
                }
                return;
            }

            _logger.Log($"ClipCell_MouseLeftButtonUp: Single-click, pasting clip {vm.Clip.Id}");
            var useOverride = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            var engine = _pasteRouter.GetEngine(vm.Clip, useOverride);
            _ = engine.PasteAsync(vm.Clip, IntPtr.Zero);
            _logger.Log("ClipCell_MouseLeftButtonUp: Paste initiated");
        }
        catch (Exception ex)
        {
            _logger.LogError("Click action failed", ex);
            MessageBox.Show($"Click action failed: {ex.Message}\n\nThis could be due to a paste engine issue or window focus problem.", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClipCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _logger.Log("ClipCell_MouseLeftButtonDown: Drag start event");
        try
        {
            var vm = (ClipCellViewModel)((Border)sender).DataContext;
            if (vm.Clip != null)
            {
                _draggedClip = vm.Clip;
                _dragStartPoint = e.GetPosition(this);
                _logger.Log($"ClipCell_MouseLeftButtonDown: Drag started for clip {vm.Clip.Id}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Drag start failed", ex);
            MessageBox.Show($"Drag start failed: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClipCell_MouseMove(object sender, MouseEventArgs e)
    {
        try
        {
            if (_draggedClip != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var position = e.GetPosition(this);
                var diff = _dragStartPoint - position;
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _logger.Log($"ClipCell_MouseMove: Starting drag operation for clip {_draggedClip.Id}");
                    var data = new DataObject("ClipId", _draggedClip.Id.ToString());
                    DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
                    _draggedClip = null;
                    _logger.Log("ClipCell_MouseMove: Drag operation complete");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Drag operation failed", ex);
            MessageBox.Show($"Drag operation failed: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClipCell_Drop(object sender, DragEventArgs e)
    {
        _logger.Log("ClipCell_Drop: Drop event received");
        try
        {
            if (e.Data.GetDataPresent("ClipId") && sender is Border border)
            {
                var vm = (ClipCellViewModel)border.DataContext;
                var clipId = Guid.Parse(e.Data.GetData("ClipId")!.ToString()!);
                
                _logger.Log($"ClipCell_Drop: Updating clip {clipId} to row {vm.Row}, col {vm.Col}");
                _ = _clipRepository.UpdateClipPositionAsync(clipId, vm.Row, vm.Col);
                _ = LoadClipsAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Drop operation failed", ex);
            MessageBox.Show($"Drop operation failed: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClipCell_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void ClipCell_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _logger.Log("ClipCell_MouseRightButtonUp: Right-click event");
        try
        {
            var vm = (ClipCellViewModel)((Border)sender).DataContext;
            if (vm.Clip != null)
            {
                var contextMenu = CreateClipContextMenu(vm.Clip);
                contextMenu.IsOpen = true;
                _logger.Log("ClipCell_MouseRightButtonUp: Context menu opened");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Context menu failed", ex);
            MessageBox.Show($"Context menu failed: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private ContextMenu CreateClipContextMenu(Clip clip)
    {
        _logger.Log($"CreateClipContextMenu: Creating menu for clip {clip.Id}");
        var menu = new ContextMenu();

        // Paste
        var pasteItem = new MenuItem { Header = "Paste" };
        pasteItem.Click += async (s, e) =>
        {
            try
            {
                _logger.Log("Context menu Paste clicked");
                var engine = _pasteRouter.GetEngine(clip, false);
                await engine.PasteAsync(clip, IntPtr.Zero);
                _logger.Log("Context menu Paste complete");
            }
            catch (Exception ex)
            {
                _logger.LogError("Context menu Paste failed", ex);
                MessageBox.Show($"Paste failed: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        menu.Items.Add(pasteItem);

        // Paste as text
        var pasteAsTextItem = new MenuItem { Header = "Paste as text" };
        pasteAsTextItem.Click += async (s, e) =>
        {
            try
            {
                _logger.Log("Context menu Paste as text clicked");
                var tempClip = new Clip { PlainText = clip.PlainText, Method = PasteMethod.Direct };
                var engine = _pasteRouter.GetEngine(tempClip, false);
                await engine.PasteAsync(tempClip, IntPtr.Zero);
                _logger.Log("Context menu Paste as text complete");
            }
            catch (Exception ex)
            {
                _logger.LogError("Context menu Paste as text failed", ex);
                MessageBox.Show($"Paste as text failed: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        menu.Items.Add(pasteAsTextItem);

        // Paste as keystrokes
        var pasteAsKeystrokesItem = new MenuItem { Header = "Paste as keystrokes" };
        pasteAsKeystrokesItem.Click += async (s, e) =>
        {
            try
            {
                _logger.Log("Context menu Paste as keystrokes clicked");
                var engine = _pasteRouter.GetEngine(clip, true);
                await engine.PasteAsync(clip, IntPtr.Zero);
                _logger.Log("Context menu Paste as keystrokes complete");
            }
            catch (Exception ex)
            {
                _logger.LogError("Context menu Paste as keystrokes failed", ex);
                MessageBox.Show($"Paste as keystrokes failed: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        menu.Items.Add(pasteAsKeystrokesItem);

        menu.Items.Add(new Separator());

        // Edit
        var editItem = new MenuItem { Header = "Edit..." };
        editItem.Click += (s, e) =>
        {
            try
            {
                _logger.Log("Context menu Edit clicked");
                var editor = new ClipEditorWindow(clip, _clipRepository, _hotkeyService);
                if (editor.ShowDialog() == true && editor.ResultClip != null)
                {
                    _ = _clipRepository.UpdateAsync(editor.ResultClip);
                    _ = LoadClipsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Context menu Edit failed", ex);
                MessageBox.Show($"Edit failed: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        menu.Items.Add(editItem);

        // Rename
        var renameItem = new MenuItem { Header = "Rename..." };
        renameItem.Click += (s, e) =>
        {
            try
            {
                _logger.Log("Context menu Rename clicked");
                var dialog = new Window
                {
                    Title = "Rename Clip",
                    Width = 300,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = new StackPanel { Margin = new Thickness(10), Children = {
                        new TextBox { Name = "RenameTextBox", Margin = new Thickness(0,0,0,10), Text = clip.Label ?? "" },
                        new Button { Content = "OK", Width = 70, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,10,0,0) }
                    }}
                };
                dialog.Loaded += (s2, e2) => ((TextBox)dialog.FindName("RenameTextBox")).Focus();
                if (dialog.ShowDialog() == true)
                {
                    var textBox = (TextBox)dialog.FindName("RenameTextBox");
                    clip.Label = textBox.Text;
                    _ = _clipRepository.UpdateAsync(clip);
                    _ = LoadClipsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Context menu Rename failed", ex);
                MessageBox.Show($"Rename failed: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        menu.Items.Add(renameItem);

        // Set color
        var setColorItem = new MenuItem { Header = "Set color..." };
        setColorItem.Click += (s, e) =>
        {
            try
            {
                _logger.Log("Context menu Set color clicked");
                var dialog = new ColorPickerDialog();
                if (dialog.ShowDialog() == true && dialog.SelectedColor.HasValue)
                {
                    clip.ColorHex = dialog.SelectedColor.Value.ToString();
                    _ = _clipRepository.UpdateAsync(clip);
                    _ = LoadClipsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Context menu Set color failed", ex);
                MessageBox.Show($"Set color failed: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        menu.Items.Add(setColorItem);

        // Set hotkey
        var setHotkeyItem = new MenuItem { Header = "Set hotkey..." };
        setHotkeyItem.Click += (s, e) =>
        {
            try
            {
                _logger.Log("Context menu Set hotkey clicked");
                var editor = new ClipEditorWindow(clip, _clipRepository, _hotkeyService);
                if (editor.ShowDialog() == true && editor.ResultClip != null)
                {
                    _ = _clipRepository.UpdateAsync(editor.ResultClip);
                    _ = LoadClipsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Context menu Set hotkey failed", ex);
                MessageBox.Show($"Set hotkey failed: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        menu.Items.Add(setHotkeyItem);

        // Set default paste method
        var setMethodItem = new MenuItem { Header = "Set default paste method" };
        var directMethodItem = new MenuItem { Header = "Direct (▶)", IsCheckable = true, IsChecked = clip.Method == PasteMethod.Direct };
        var injectMethodItem = new MenuItem { Header = "Inject (⌨)", IsCheckable = true, IsChecked = clip.Method == PasteMethod.Inject };
        directMethodItem.Click += async (s, e) =>
        {
            try
            {
                _logger.Log("Context menu Set method Direct clicked");
                clip.Method = PasteMethod.Direct;
                await _clipRepository.UpdateAsync(clip);
                _ = LoadClipsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Context menu Set paste method failed", ex);
                MessageBox.Show($"Set paste method failed: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        injectMethodItem.Click += async (s, e) =>
        {
            try
            {
                _logger.Log("Context menu Set method Inject clicked");
                clip.Method = PasteMethod.Inject;
                await _clipRepository.UpdateAsync(clip);
                _ = LoadClipsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Context menu Set paste method failed", ex);
                MessageBox.Show($"Set paste method failed: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        setMethodItem.Items.Add(directMethodItem);
        setMethodItem.Items.Add(injectMethodItem);
        menu.Items.Add(setMethodItem);

        menu.Items.Add(new Separator());

        // Move to history zone
        var moveToHistoryItem = new MenuItem { Header = "Move to history zone" };
        moveToHistoryItem.Click += async (s, e) =>
        {
            try
            {
                _logger.Log("Context menu Move to history clicked");
                await _clipRepository.UpdateClipPositionAsync(clip.Id, 0, 0);
                _ = LoadClipsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Context menu Move to history failed", ex);
                MessageBox.Show($"Move to history failed: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        menu.Items.Add(moveToHistoryItem);

        // Delete
        var deleteItem = new MenuItem { Header = "Delete" };
        deleteItem.Click += async (s, e) =>
        {
            try
            {
                _logger.Log("Context menu Delete clicked");
                await _clipRepository.DeleteAsync(clip.Id);
                _ = LoadClipsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Context menu Delete failed", ex);
                MessageBox.Show($"Delete failed: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        menu.Items.Add(deleteItem);

        return menu;
    }

    private void MainWindow_Closing(object sender, CancelEventArgs e)
    {
        _logger.Log("MainWindow_Closing: Minimizing to tray");
        e.Cancel = true;
        Hide();
        _trayIcon.ShowNotification("Clppy minimized to tray");
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.F)
        {
            _logger.Log("MainWindow_KeyDown: Filter overlay opening (Ctrl+F)");
            FilterOverlay.Visibility = Visibility.Visible;
            FilterTextBox.Focus();
        }
        else if (e.Key == Key.Escape)
        {
            if (FilterOverlay.Visibility == Visibility.Visible)
            {
                _logger.Log("MainWindow_KeyDown: Filter overlay closing (Escape)");
                FilterOverlay.Visibility = Visibility.Collapsed;
                FilterTextBox.Clear();
            }
            else
            {
                _logger.Log("MainWindow_KeyDown: Hiding window (Escape)");
                Hide();
            }
        }
    }

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            var filterText = FilterTextBox.Text.ToLower();
            foreach (var clip in Clips)
            {
                clip.UpdateOpacity(filterText);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Filter failed", ex);
            MessageBox.Show($"Filter failed: {ex.Message}", "Clppy Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void FilterTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            FilterOverlay.Visibility = Visibility.Collapsed;
            FilterTextBox.Clear();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _logger.Log("OnClosed: Cleaning up resources");
        _clipboardCapture.ClipCaptured -= OnClipCaptured;
        _hotkeyService.HotkeyTriggered -= OnHotkeyTriggered;
        _trayIcon.Dispose();
        _clipboardCapture.StopListening();
        base.OnClosed(e);
    }
}

public class ClipCellViewModel : INotifyPropertyChanged
{
    public Clip? Clip { get; }
    public bool IsInHistoryZone { get; }
    public int? Row { get; }
    public int? Col { get; }
    
    private string _label = "";
    public string Label
    {
        get => _label;
        set { _label = value; OnPropertyChanged(); }
    }

    private string _preview = "";
    public string Preview
    {
        get => _preview;
        set { _preview = value; OnPropertyChanged(); }
    }

    private string _methodIcon = "";
    public string MethodIcon
    {
        get => _methodIcon;
        set { _methodIcon = value; OnPropertyChanged(); }
    }

    private Brush _backgroundBrush = Brushes.White;
    public Brush BackgroundBrush
    {
        get => _backgroundBrush;
        set { _backgroundBrush = value; OnPropertyChanged(); }
    }

    private double _opacity = 1.0;
    public double Opacity
    {
        get => _opacity;
        set { _opacity = value; OnPropertyChanged(); }
    }

    public ClipCellViewModel(Clip? clip, bool isInHistoryZone, Models.Settings settings)
    {
        Clip = clip;
        IsInHistoryZone = isInHistoryZone;
        Row = clip?.Row;
        Col = clip?.Col;

        if (clip != null)
        {
            Label = clip.Label ?? (clip.PlainText?.Length > 20 ? clip.PlainText[..Math.Min(20, clip.PlainText.Length)] + "..." : clip.PlainText ?? "");
            Preview = clip.PlainText ?? "";
            MethodIcon = clip.Method == PasteMethod.Direct ? "▶" : "⌨";
            
            var colorHex = clip.ColorHex ?? settings.DefaultColorHex;
            var color = (Brush)new BrushConverter().ConvertFromString(colorHex)!;
            BackgroundBrush = color;
            
            if (isInHistoryZone)
            {
                var historyBrush = new SolidColorBrush(Color.FromArgb(100, 255, 230, 140));
                BackgroundBrush = historyBrush;
            }
        }
        else
        {
            Label = "";
            Preview = "";
            MethodIcon = "";
            BackgroundBrush = isInHistoryZone 
                ? new SolidColorBrush(Color.FromArgb(50, 255, 230, 140)) 
                : Brushes.White;
        }
    }

    public void UpdateOpacity(string filterText)
    {
        if (string.IsNullOrEmpty(filterText) || Clip == null)
        {
            Opacity = 1.0;
            return;
        }

        var match = (Label?.ToLower().Contains(filterText) ?? false) ||
                    (Clip.PlainText?.ToLower().Contains(filterText) ?? false);
        Opacity = match ? 1.0 : 0.25;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
