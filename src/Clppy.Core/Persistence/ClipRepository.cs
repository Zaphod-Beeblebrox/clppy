using Microsoft.EntityFrameworkCore;
using Clppy.Core.Models;

namespace Clppy.Core.Persistence;

public class ClipRepository : IClipRepository
{
    private readonly ClppyDbContext _context;

    public ClipRepository(ClppyDbContext context)
    {
        _context = context;
    }

    public async Task<Clip?> GetByIdAsync(Guid id)
    {
        return await _context.Clips
            .Where(c => c.Id == id && c.DeletedAt == null)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Clip>> GetAllAsync()
    {
        return await _context.Clips
            .Where(c => c.DeletedAt == null)
            .ToListAsync();
    }

    public async Task<Clip> AddAsync(Clip clip)
    {
        _context.Clips.Add(clip);
        await _context.SaveChangesAsync();
        return clip;
    }

    public async Task UpdateAsync(Clip clip)
    {
        _context.Clips.Update(clip);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var clip = await _context.Clips.FindAsync(id);
        if (clip != null)
        {
            clip.DeletedAt = DateTime.UtcNow;
            _context.Clips.Update(clip);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Models.Settings> GetSettingsAsync()
    {
        var settings = await _context.Settings.FirstOrDefaultAsync();
        return settings ?? new Models.Settings();
    }

    public async Task SaveSettingsAsync(Models.Settings settings)
    {
        settings.Id = 1; // singleton row
        var existing = await _context.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1);
        if (existing == null)
        {
            _context.Settings.Add(settings);
        }
        else
        {
            _context.Settings.Update(settings);
        }
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<Clip>> GetHistoryZoneAsync(int maxRows, int maxCols)
    {
        return await _context.Clips
            .Where(c => c.Pinned == false && c.DeletedAt == null)
            .OrderBy(c => c.HistoryIndex)
            .ToListAsync();
    }

    public async Task<IEnumerable<Clip>> GetPinnedClipsAsync()
    {
        return await _context.Clips
            .Where(c => c.Pinned == true && c.DeletedAt == null)
            .ToListAsync();
    }

    public async Task UpdateClipPositionAsync(Guid clipId, int? row, int? col)
    {
        var clip = await _context.Clips.FindAsync(clipId);
        if (clip != null)
        {
            var settings = await GetSettingsAsync();
            var inHistoryZone = row.HasValue && col.HasValue && 
                               row.Value < settings.HistoryRows && 
                               col.Value < settings.HistoryCols;

            clip.Row = row;
            clip.Col = col;
            clip.Pinned = !inHistoryZone;
            
            if (clip.Pinned)
            {
                clip.HistoryIndex = null;
            }
            else
            {
                // Reassign history indices for all unpinned clips
                var historyClips = await _context.Clips
                    .Where(c => c.Pinned == false && c.DeletedAt == null)
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync();
                
                for (int i = 0; i < historyClips.Count; i++)
                {
                    historyClips[i].HistoryIndex = i;
                }
            }

            clip.UpdatedAt = DateTime.UtcNow;
            _context.Clips.Update(clip);
            await _context.SaveChangesAsync();
        }
    }
}
