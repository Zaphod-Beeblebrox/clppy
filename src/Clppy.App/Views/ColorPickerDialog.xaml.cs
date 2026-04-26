using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Clppy.App.Views;

public partial class ColorPickerDialog : Window
{
    public Color? SelectedColor { get; private set; }

    public ColorPickerDialog()
    {
        InitializeComponent();
        InitializePresetColors();
        
        OkButton.Click += OnOkClick;
        CancelButton.Click += OnCancelClick;
    }

    private void InitializePresetColors()
    {
        var grid = (Grid)this.Content;
        var presetPanel = new WrapPanel { Margin = new System.Windows.Thickness(0, 0, 0, 10) };
        
        var colors = new[]
        {
            "#F5F5F5", "#FFEB3B", "#4CAF50", "#2196F3",
            "#9C27B0", "#FF5722", "#795548", "#607D8B",
            "#E91E63", "#00BCD4", "#FF9800", "#8BC34A"
        };
        
        foreach (var colorHex in colors)
        {
            var button = new Button
            {
                Width = 30,
                Height = 30,
                Margin = new System.Windows.Thickness(2),
                Background = (Brush)new BrushConverter().ConvertFromString(colorHex)!,
                Tag = colorHex
            };
            button.Click += OnPresetColorClick;
            presetPanel.Children.Add(button);
        }
        
        grid.Children.Insert(1, presetPanel);
        Grid.SetRow(presetPanel, 1);
    }

    private void OnPresetColorClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var hex = (string)button.Tag;
        var brush = (Brush)new BrushConverter().ConvertFromString(hex)!;
        ColorPreview.Background = brush;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        var color = ((SolidColorBrush)ColorPreview.Background).Color;
        SelectedColor = color;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
