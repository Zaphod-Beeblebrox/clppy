using System.Threading.Tasks;
using Clppy.Core.Models;
using Clppy.Core.Persistence;

namespace Clppy.Core.Settings;

public class SettingsService : ISettingsService
{
    private readonly IClipRepository _clipRepository;
    private Models.Settings _current;

    public Models.Settings Current => _current;

    public SettingsService(IClipRepository clipRepository)
    {
        _clipRepository = clipRepository;
        _current = new Models.Settings();
    }

    public async Task LoadAsync()
    {
        _current = await _clipRepository.GetSettingsAsync();
    }

    public async Task SaveAsync()
    {
        await _clipRepository.SaveSettingsAsync(_current);
    }

    public int HistoryZoneRowCount => _current.HistoryRows;
    public int HistoryZoneColumnCount => _current.HistoryCols;
    public int CellWidth => _current.CellWidthPx;
    public int CellHeight => _current.CellHeightPx;
    public int InjectDelay => _current.InjectKeystrokeDelayMs;
}
