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
    private readonly TrayIconManager _trayIcon;
    private Clip? _draggedClip;
    private Point _dragStartPoint;

    public ObservableCollection<ClipCellViewModel> Clips { get; } = new();

    public MainWindow(IClipRepository clipRepository,
                      IClipboardCapture clipboardCapture,
                      PasteRouter pasteRouter,
                      IHotkeyService hotkeyService,
                      ISettingsService settingsService)
    {
        _clipRepository = clipRepository;
        _clipboardCapture = clipboardCapture;
        _pasteRouter = pasteRouter;
        _hotkeyService = hotkeyService;
        _settingsService = settingsService;

        InitializeComponent();
        
        ClipGrid.ItemsSource = Clips;
        _trayIcon = new TrayIconManager(this);
        _trayIcon.Initialize();

        _clipboardCapture.ClipCaptured += OnClipCaptured;
        _hotkeyService.HotkeyTriggered += OnHotkeyTriggered;

        _ = LoadClipsAsync();
    }

    private async Task LoadClipsAsync()
    {
        Clips.Clear();

        await _settingsService.LoadAsync();
        var settings = _settingsService.Current;
        var historyRows = settings.HistoryRows;
        var historyCols = settings.HistoryCols;
        var totalRows = 30;
        var totalCols = 9;

        var allClips = await _clipRepository.GetAllAsync();
        var pinnedClips = allClips.Where(c => c.Pinned && c.Row.HasValue && c.Col.HasValue).ToList();
        var historyClips = allClips.Where(c => !c.Pinned).OrderBy(c => c.HistoryIndex).ToList();

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
    }

    private void OnClipCaptured(Clip clip)
    {
        Dispatcher.Invoke(async () => await LoadClipsAsync());
    }

    private void OnHotkeyTriggered(Guid clipId)
    {
        Dispatcher.Invoke(async () =>
        {
            var clip = await _clipRepository.GetByIdAsync(clipId);
            if (clip != null)
            {
                var engine = _pasteRouter.GetEngine(clip, false);
                await engine.PasteAsync(clip, IntPtr.Zero);
            }
        });
    }

    private void ClipCell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var vm = (ClipCellViewModel)((Border)sender).DataContext;
        if (vm.Clip == null) return;

        if (e.ClickCount == 2)
        {
            var editor = new ClipEditorWindow(vm.Clip, _clipRepository, _hotkeyService);
            if (editor.ShowDialog() == true && editor.ResultClip != null)
            {
                _ = _clipRepository.UpdateAsync(editor.ResultClip);
                _ = LoadClipsAsync();
            }
            return;
        }

        var useOverride = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var engine = _pasteRouter.GetEngine(vm.Clip, useOverride);
        _ = engine.PasteAsync(vm.Clip, IntPtr.Zero);
    }

    private void ClipCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var vm = (ClipCellViewModel)((Border)sender).DataContext;
        if (vm.Clip != null)
        {
            _draggedClip = vm.Clip;
            _dragStartPoint = e.GetPosition(this);
        }
    }

    private void ClipCell_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedClip != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var position = e.GetPosition(this);
            var diff = _dragStartPoint - position;
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var data = new DataObject("ClipId", _draggedClip.Id.ToString());
                DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
                _draggedClip = null;
            }
        }
    }

    private void ClipCell_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("ClipId") && sender is Border border)
        {
            var vm = (ClipCellViewModel)border.DataContext;
            var clipId = Guid.Parse(e.Data.GetData("ClipId")!.ToString()!);
            
            _ = _clipRepository.UpdateClipPositionAsync(clipId, vm.Row, vm.Col);
            _ = LoadClipsAsync();
        }
    }

    private void ClipCell_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void ClipCell_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var vm = (ClipCellViewModel)((Border)sender).DataContext;
        if (vm.Clip != null)
        {
            var contextMenu = CreateClipContextMenu(vm.Clip);
            contextMenu.IsOpen = true;
        }
    }

    private ContextMenu CreateClipContextMenu(Clip clip)
    {
        var menu = new ContextMenu();

        // Paste
        var pasteItem = new MenuItem { Header = "Paste" };
        pasteItem.Click += async (s, e) =>
        {
            var engine = _pasteRouter.GetEngine(clip, false);
            await engine.PasteAsync(clip, IntPtr.Zero);
        };
        menu.Items.Add(pasteItem);

        // Paste as text
        var pasteAsTextItem = new MenuItem { Header = "Paste as text" };
        pasteAsTextItem.Click += async (s, e) =>
        {
            var tempClip = new Clip { PlainText = clip.PlainText, Method = PasteMethod.Direct };
            var engine = _pasteRouter.GetEngine(tempClip, false);
            await engine.PasteAsync(tempClip, IntPtr.Zero);
        };
        menu.Items.Add(pasteAsTextItem);

        // Paste as keystrokes
        var pasteAsKeystrokesItem = new MenuItem { Header = "Paste as keystrokes" };
        pasteAsKeystrokesItem.Click += async (s, e) =>
        {
            var engine = _pasteRouter.GetEngine(clip, true);
            await engine.PasteAsync(clip, IntPtr.Zero);
        };
        menu.Items.Add(pasteAsKeystrokesItem);

        menu.Items.Add(new Separator());

        // Edit
        var editItem = new MenuItem { Header = "Edit..." };
        editItem.Click += (s, e) =>
        {
            var editor = new ClipEditorWindow(clip, _clipRepository, _hotkeyService);
            if (editor.ShowDialog() == true && editor.ResultClip != null)
            {
                _ = _clipRepository.UpdateAsync(editor.ResultClip);
                _ = LoadClipsAsync();
            }
        };
        menu.Items.Add(editItem);

        // Rename
        var renameItem = new MenuItem { Header = "Rename..." };
        renameItem.Click += (s, e) =>
        {
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
        };
        menu.Items.Add(renameItem);

        // Set color
        var setColorItem = new MenuItem { Header = "Set color..." };
        setColorItem.Click += (s, e) =>
        {
            var dialog = new ColorPickerDialog();
            if (dialog.ShowDialog() == true && dialog.SelectedColor.HasValue)
            {
                clip.ColorHex = dialog.SelectedColor.Value.ToString();
                _ = _clipRepository.UpdateAsync(clip);
                _ = LoadClipsAsync();
            }
        };
        menu.Items.Add(setColorItem);

        // Set hotkey
        var setHotkeyItem = new MenuItem { Header = "Set hotkey..." };
        setHotkeyItem.Click += (s, e) =>
        {
            var editor = new ClipEditorWindow(clip, _clipRepository, _hotkeyService);
            if (editor.ShowDialog() == true && editor.ResultClip != null)
            {
                _ = _clipRepository.UpdateAsync(editor.ResultClip);
                _ = LoadClipsAsync();
            }
        };
        menu.Items.Add(setHotkeyItem);

        // Set default paste method
        var setMethodItem = new MenuItem { Header = "Set default paste method" };
        var directMethodItem = new MenuItem { Header = "Direct (▶)", IsCheckable = true, IsChecked = clip.Method == PasteMethod.Direct };
        var injectMethodItem = new MenuItem { Header = "Inject (⌨)", IsCheckable = true, IsChecked = clip.Method == PasteMethod.Inject };
        directMethodItem.Click += async (s, e) =>
        {
            clip.Method = PasteMethod.Direct;
            await _clipRepository.UpdateAsync(clip);
            _ = LoadClipsAsync();
        };
        injectMethodItem.Click += async (s, e) =>
        {
            clip.Method = PasteMethod.Inject;
            await _clipRepository.UpdateAsync(clip);
            _ = LoadClipsAsync();
        };
        setMethodItem.Items.Add(directMethodItem);
        setMethodItem.Items.Add(injectMethodItem);
        menu.Items.Add(setMethodItem);

        menu.Items.Add(new Separator());

        // Move to history zone
        var moveToHistoryItem = new MenuItem { Header = "Move to history zone" };
        moveToHistoryItem.Click += async (s, e) =>
        {
            await _clipRepository.UpdateClipPositionAsync(clip.Id, 0, 0);
            _ = LoadClipsAsync();
        };
        menu.Items.Add(moveToHistoryItem);

        // Delete
        var deleteItem = new MenuItem { Header = "Delete" };
        deleteItem.Click += async (s, e) =>
        {
            await _clipRepository.DeleteAsync(clip.Id);
            _ = LoadClipsAsync();
        };
        menu.Items.Add(deleteItem);

        return menu;
    }

    private void MainWindow_Closing(object sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        _trayIcon.ShowNotification("Clppy minimized to tray");
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.F)
        {
            FilterOverlay.Visibility = Visibility.Visible;
            FilterTextBox.Focus();
        }
        else if (e.Key == Key.Escape)
        {
            if (FilterOverlay.Visibility == Visibility.Visible)
            {
                FilterOverlay.Visibility = Visibility.Collapsed;
                FilterTextBox.Clear();
            }
            else
            {
                Hide();
            }
        }
    }

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var filterText = FilterTextBox.Text.ToLower();
        foreach (var clip in Clips)
        {
            clip.UpdateOpacity(filterText);
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
        _clipboardCapture.ClipCaptured -= OnClipCaptured;
        _hotkeyService.HotkeyTriggered -= OnHotkeyTriggered;
        _trayIcon.Dispose();
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
