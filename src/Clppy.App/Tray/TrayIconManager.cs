using System;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace Clppy.App;

public class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Window _window;
    private bool _disposed;

    public TrayIconManager(Window window)
    {
        _window = window;
        _notifyIcon = new NotifyIcon
        {
            Icon = GetDefaultIcon(),
            Text = "Clppy",
            Visible = false
        };

        _notifyIcon.MouseClick += NotifyIcon_MouseClick;
        _notifyIcon.ContextMenuStrip = CreateContextMenu();
    }

    private static System.Drawing.Icon GetDefaultIcon()
    {
        // Create a simple placeholder icon
        var bitmap = new System.Drawing.Bitmap(32, 32);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            g.Clear(System.Drawing.Color.Gray);
            using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
            {
                g.FillEllipse(brush, 4, 4, 24, 24);
            }
        }
        return new System.Drawing.Icon(bitmap);
    }

    public void Initialize()
    {
        _notifyIcon.Visible = true;
    }

    public void Show()
    {
        _window.Show();
        _window.Activate();
    }

    public void Hide()
    {
        _window.Hide();
    }

    public void ShowNotification(string message)
    {
        _notifyIcon.ShowBalloonTip(1000, "Clppy", message, ToolTipInfo.Info);
    }

    private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (_window.Visibility == Visibility.Visible)
                _window.Hide();
            else
                Show();
        }
        else if (e.Button == MouseButtons.Right)
        {
            _notifyIcon.ContextMenuStrip.Show(_notifyIcon, e.Location);
        }
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        var showHideItem = new ToolStripMenuItem("Show / Hide Clppy");
        showHideItem.Click += (s, e) =>
        {
            if (_window.Visibility == Visibility.Visible)
                _window.Hide();
            else
                Show();
        };
        menu.Items.Add(showHideItem);

        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (s, e) => ShowSettingsDialog();
        menu.Items.Add(settingsItem);

        var aboutItem = new ToolStripMenuItem("About");
        aboutItem.Click += (s, e) => ShowAboutDialog();
        menu.Items.Add(aboutItem);

        menu.Items.Add(new ToolStripSeparator());

        var quitItem = new ToolStripMenuItem("Quit");
        quitItem.Click += (s, e) => Application.Shutdown();
        menu.Items.Add(quitItem);

        return menu;
    }

    private void ShowSettingsDialog()
    {
        var settingsWindow = new Window
        {
            Title = "Settings",
            Width = 300,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new System.Windows.Controls.TextBlock
            {
                Text = "Settings placeholder for v0\n\nVersion: 0.1.0",
                TextAlignment = System.Windows.TextAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            }
        };
        settingsWindow.ShowDialog();
    }

    private void ShowAboutDialog()
    {
        var aboutWindow = new Window
        {
            Title = "About Clppy",
            Width = 300,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new System.Windows.Controls.StackPanel
            {
                Children = {
                    new System.Windows.Controls.TextBlock { Text = "Clppy v0.1.0", FontSize = 16, FontWeight = System.Windows.FontWeights.Bold, HorizontalAlignment = System.Windows.HorizontalAlignment.Center },
                    new System.Windows.Controls.TextBlock { Text = "Clipboard Manager", HorizontalAlignment = System.Windows.HorizontalAlignment.Center },
                    new System.Windows.Controls.TextBlock { Text = "", },
                    new System.Windows.Controls.TextBlock { Text = "MIT License", HorizontalAlignment = System.Windows.HorizontalAlignment.Center }
                },
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            }
        };
        aboutWindow.ShowDialog();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _notifyIcon?.Dispose();
            _disposed = true;
        }
    }
}
