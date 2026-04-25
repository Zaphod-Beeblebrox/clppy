using System;
using Clppy.Core.Models;
using Xunit;

namespace Clppy.Core.Tests;

public class ClipTests
{
    [Fact]
    public void Clip_Default_Values_Are_Sensible()
    {
        var clip = new Clip();

        Assert.Equal(false, clip.Pinned);
        Assert.Null(clip.DeletedAt);
        Assert.Equal(PasteMethod.Direct, clip.Method);
    }

    [Fact]
    public void PasteMethod_Enum_Has_Correct_Values()
    {
        var values = Enum.GetValues<PasteMethod>();
        Assert.Equal(2, values.Length);
        Assert.Contains(PasteMethod.Direct, values);
        Assert.Contains(PasteMethod.Inject, values);
    }

    [Fact]
    public void Settings_Default_Values_Match_Spec()
    {
        var settings = new Settings();

        Assert.Equal(5, settings.HistoryRows);
        Assert.Equal(4, settings.HistoryCols);
        Assert.Equal(140, settings.CellWidthPx);
        Assert.Equal(32, settings.CellHeightPx);
        Assert.Equal(5, settings.InjectKeystrokeDelayMs);
        Assert.Equal("#F5F5F5", settings.DefaultColorHex);
    }
}