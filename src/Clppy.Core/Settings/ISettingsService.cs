using System.Threading.Tasks;
using Clppy.Core.Models;
using Clppy.Core.Persistence;

namespace Clppy.Core.Settings;

public interface ISettingsService
{
    Models.Settings Current { get; }
    Task LoadAsync();
    Task SaveAsync();
    int HistoryZoneRowCount => Current.HistoryRows;
    int HistoryZoneColumnCount => Current.HistoryCols;
    int CellWidth => Current.CellWidthPx;
    int CellHeight => Current.CellHeightPx;
    int InjectDelay => Current.InjectKeystrokeDelayMs;
}
