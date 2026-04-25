using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Clppy.Core.Clipboard;
using Clppy.Core.Hotkeys;
using Clppy.Core.Models;
using Clppy.Core.Paste;
using Clppy.Core.Persistence;
using Clppy.Core.Settings;

namespace Clppy.App;

public static class DependencyInjection
{
    public static IServiceCollection AddClppyServices(this IServiceCollection services)
    {
        // Database context
        var dbPath = GetDatabasePath();
        services.AddDbContext<ClppyDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Services
        services.AddSingleton<IClipRepository, ClipRepository>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IClipboardCapture, ClipboardCaptureService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<IPasteEngine>(sp => new DirectPasteEngine());
        services.AddSingleton<IPasteEngine>(sp => new InjectPasteEngine());
        services.AddSingleton<PasteRouter>();

        // Windows
        services.AddSingleton<MainWindow>();
        services.AddSingleton<ClipEditorWindow>();

        return services;
    }

    private static string GetDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var clppyDir = Path.Combine(appData, "Clppy");
        Directory.CreateDirectory(clppyDir);
        return Path.Combine(clppyDir, "clppy.db");
    }
}
