using System.ComponentModel.DataAnnotations;

namespace Clppy.Core.Models;

public class Settings
{
    [Key]
    public int Id { get; set; } = 1;

    public int HistoryRows { get; set; } = 5;

    public int HistoryCols { get; set; } = 4;

    public int CellWidthPx { get; set; } = 140;

    public int CellHeightPx { get; set; } = 32;

    public int InjectKeystrokeDelayMs { get; set; } = 5;

    public string DefaultColorHex { get; set; } = "#F5F5F5";
}
