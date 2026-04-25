using Clppy.Core.Models;

namespace Clppy.Core.Persistence;

public interface IClipRepository
{
    Task<Clip?> GetByIdAsync(Guid id);
    Task<IEnumerable<Clip>> GetAllAsync();
    Task<Clip> AddAsync(Clip clip);
    Task UpdateAsync(Clip clip);
    Task DeleteAsync(Guid id); // soft delete (sets DeletedAt)
    Task<Models.Settings> GetSettingsAsync();
    Task SaveSettingsAsync(Models.Settings settings);
    Task<IEnumerable<Clip>> GetHistoryZoneAsync(int maxRows, int maxCols);
    Task<IEnumerable<Clip>> GetPinnedClipsAsync();
    Task UpdateClipPositionAsync(Guid clipId, int? row, int? col);
}
