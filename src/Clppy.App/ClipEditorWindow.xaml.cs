using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clppy.Core.Models;
using Clppy.Core.Persistence;
using Clppy.Core.Hotkeys;

namespace Clppy.App;

public partial class ClipEditorWindow : Window
{
    private readonly Clip? _originalClip;
    private readonly IClipRepository _clipRepository;
    private readonly IHotkeyService _hotkeyService;
    private bool _isCapturingHotkey;

    public Clip? ResultClip { get; private set; }

    public ClipEditorWindow(Clip? clip, IClipRepository clipRepository, IHotkeyService hotkeyService)
    {
        _originalClip = clip;
        _clipRepository = clipRepository;
        _hotkeyService = hotkeyService;

        InitializeComponent();

        if (clip != null)
        {
            LabelTextBox.Text = clip.Label ?? "";
            ContentTextBox.Text = clip.PlainText ?? "";
            DirectMethodRadio.IsChecked = clip.Method == PasteMethod.Direct;
            InjectMethodRadio.IsChecked = clip.Method == PasteMethod.Inject;
            ColorTextBlock.Text = clip.ColorHex ?? "#F5F5F5";
            HotkeyTextBlock.Text = clip.Hotkey ?? "(none)";

            if (clip.Rtf != null || clip.Html != null)
            {
                RichTextIndicator.Visibility = Visibility.Visible;
            }
        }
    }

    private void ContentTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Intercept Tab key to insert tab character instead of changing focus
        if (e.Key == Key.Tab)
        {
            e.Handled = true;
            var textBox = (TextBox)sender;
            var caretIndex = textBox.CaretIndex;
            var text = textBox.Text;
            textBox.Text = text.Insert(caretIndex, "\t");
            textBox.CaretIndex = caretIndex + 1;
        }
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerDialog();
        if (dialog.ShowDialog() == true && dialog.SelectedColor.HasValue)
        {
            var color = dialog.SelectedColor.Value;
            var hex = color.ToString();
            ColorTextBlock.Text = hex;
        }
    }

    private void HotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCapturingHotkey)
        {
            // Stop capturing
            _isCapturingHotkey = false;
            HotkeyButton.Content = "Capture";
            HotkeyButton.IsEnabled = true;
        }
        else
        {
            // Start capturing
            _isCapturingHotkey = true;
            HotkeyButton.Content = "Press keys...";
            HotkeyButton.IsEnabled = false;
            HotkeyTextBlock.Text = "Listening...";
            Keyboard.Focus(this);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_isCapturingHotkey)
        {
            var modifiers = new System.Collections.Generic.List<string>();
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                modifiers.Add("Ctrl");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                modifiers.Add("Alt");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                modifiers.Add("Shift");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows))
                modifiers.Add("Win");

            var keyStr = e.Key.ToString();
            var hotkey = string.Join("+", modifiers) + "+" + keyStr;

            // Check for conflicts
            if (!_hotkeyService.IsHotkeyAvailable(string.Join("+", modifiers), keyStr[0]))
            {
                MessageBox.Show("This hotkey is already assigned to another clip.", "Hotkey Conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
                _isCapturingHotkey = false;
                HotkeyButton.Content = "Capture";
                HotkeyButton.IsEnabled = true;
                HotkeyTextBlock.Text = "(none)";
                return;
            }

            HotkeyTextBlock.Text = hotkey;
            _isCapturingHotkey = false;
            HotkeyButton.Content = "Capture";
            HotkeyButton.IsEnabled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
        else
        {
            base.OnKeyDown(e);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var clip = _originalClip ?? new Clip();
        clip.Label = LabelTextBox.Text;
        clip.PlainText = ContentTextBox.Text;
        clip.Method = DirectMethodRadio.IsChecked == true ? PasteMethod.Direct : PasteMethod.Inject;
        clip.ColorHex = ColorTextBlock.Text;
        clip.Hotkey = HotkeyTextBlock.Text != "(none)" ? HotkeyTextBlock.Text : null;
        clip.UpdatedAt = DateTime.UtcNow;

        ResultClip = clip;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

// Simple color picker dialog
class ColorPickerDialog : Window
{
    public Color? SelectedColor { get; private set; }

    public ColorPickerDialog()
    {
        Title = "Pick Color";
        Width = 250;
        Height = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var grid = new Grid { Margin = 10 };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock { Text = "Select a color:", Margin = new Thickness(0, 0, 0, 10) };
        grid.Children.Add(label);
        Grid.SetRow(label, 0);

        var colorPicker = new System.Windows.Controls.ColorPicker();
        colorPicker.SelectedColor = Colors.White;
        colorPicker.Margin = new Thickness(0, 0, 0, 10);
        grid.Children.Add(colorPicker);
        Grid.SetRow(colorPicker, 1);

        var preview = new Border { Height = 50, Background = Brushes.White, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 10) };
        colorPicker.SelectedColorChanged += (s, e) => preview.Background = new SolidColorBrush(colorPicker.SelectedColor ?? Colors.White);
        grid.Children.Add(preview);
        Grid.SetRow(preview, 2);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        
        var okButton = new Button { Content = "OK", Width = 70, Margin = new Thickness(0, 0, 10, 0) };
        okButton.Click += (s, e) =>
        {
            SelectedColor = colorPicker.SelectedColor;
            DialogResult = true;
            Close();
        };
        buttons.Children.Add(okButton);

        var cancelButton = new Button { Content = "Cancel", Width = 70 };
        cancelButton.Click += (s, e) =>
        {
            DialogResult = false;
            Close();
        };
        buttons.Children.Add(cancelButton);

        grid.Children.Add(buttons);
        Grid.SetRow(buttons, 3);

        Content = grid;
    }
}
