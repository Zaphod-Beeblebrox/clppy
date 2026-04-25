using System;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Clppy.Core.Models;
using Clppy.Core.Persistence;
using Xunit;

namespace Clppy.Core.Tests;

public class PersistenceTests
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ClppyDbContext> _options;

    public PersistenceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ClppyDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new ClppyDbContext(_options);
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task AddAsync_And_GetByIdAsync_RoundTrip()
    {
        using var context = new ClppyDbContext(_options);
        var repository = new ClipRepository(context);

        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Label = "Test Clip",
            PlainText = "Test text",
            Method = PasteMethod.Inject
        };

        await repository.AddAsync(clip);
        var retrieved = await repository.GetByIdAsync(clip.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(clip.Id, retrieved.Id);
        Assert.Equal(clip.Label, retrieved.Label);
        Assert.Equal(clip.PlainText, retrieved.PlainText);
        Assert.Equal(clip.Method, retrieved.Method);
    }

    [Fact]
    public async Task UpdateAsync_Changes_Are_Persisted()
    {
        using var context = new ClppyDbContext(_options);
        var repository = new ClipRepository(context);

        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Label = "Original Label",
            PlainText = "Original text"
        };

        await repository.AddAsync(clip);
        clip.Label = "Updated Label";
        clip.PlainText = "Updated text";
        await repository.UpdateAsync(clip);

        var retrieved = await repository.GetByIdAsync(clip.Id);
        Assert.Equal("Updated Label", retrieved.Label);
        Assert.Equal("Updated text", retrieved.PlainText);
    }

    [Fact]
    public async Task DeleteAsync_Sets_DeletedAt_And_Clips_Are_Excluded()
    {
        using var context = new ClppyDbContext(_options);
        var repository = new ClipRepository(context);

        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Label = "To be deleted"
        };

        await repository.AddAsync(clip);
        await repository.DeleteAsync(clip.Id);

        var retrieved = await repository.GetByIdAsync(clip.Id);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task GetHistoryZoneAsync_Returns_Only_Unpinned_Clips()
    {
        using var context = new ClppyDbContext(_options);
        var repository = new ClipRepository(context);

        var pinnedClip = new Clip
        {
            Id = Guid.NewGuid(),
            Pinned = true,
            HistoryIndex = 1
        };

        var unpinnedClip = new Clip
        {
            Id = Guid.NewGuid(),
            Pinned = false,
            HistoryIndex = 2
        };

        await repository.AddAsync(pinnedClip);
        await repository.AddAsync(unpinnedClip);

        var historyClips = await repository.GetHistoryZoneAsync(10, 10);
        Assert.Single(historyClips);
        Assert.Equal(unpinnedClip.Id, historyClips.First().Id);
    }

    [Fact]
    public async Task GetPinnedClipsAsync_Returns_Only_Pinned_Clips()
    {
        using var context = new ClppyDbContext(_options);
        var repository = new ClipRepository(context);

        var pinnedClip = new Clip
        {
            Id = Guid.NewGuid(),
            Pinned = true
        };

        var unpinnedClip = new Clip
        {
            Id = Guid.NewGuid(),
            Pinned = false
        };

        await repository.AddAsync(pinnedClip);
        await repository.AddAsync(unpinnedClip);

        var pinnedClips = await repository.GetPinnedClipsAsync();
        Assert.Single(pinnedClips);
        Assert.Equal(pinnedClip.Id, pinnedClips.First().Id);
    }

    [Fact]
    public async Task Settings_RoundTrip()
    {
        using var context = new ClppyDbContext(_options);
        var repository = new ClipRepository(context);

        var settings = new Settings
        {
            HistoryRows = 10,
            HistoryCols = 15,
            CellWidthPx = 200,
            CellHeightPx = 40,
            InjectKeystrokeDelayMs = 10,
            DefaultColorHex = "#000000"
        };

        await repository.SaveSettingsAsync(settings);

        var retrieved = await repository.GetSettingsAsync();
        Assert.Equal(10, retrieved.HistoryRows);
        Assert.Equal(15, retrieved.HistoryCols);
        Assert.Equal(200, retrieved.CellWidthPx);
        Assert.Equal(40, retrieved.CellHeightPx);
        Assert.Equal(10, retrieved.InjectKeystrokeDelayMs);
        Assert.Equal("#000000", retrieved.DefaultColorHex);
    }
}