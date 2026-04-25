using Microsoft.EntityFrameworkCore;
using Clppy.Core.Models;

namespace Clppy.Core.Persistence;

public class ClppyDbContext : DbContext
{
    public ClppyDbContext(DbContextOptions<ClppyDbContext> options) : base(options)
    {
    }

    public DbSet<Clip> Clips { get; set; }
    public DbSet<Models.Settings> Settings { get; set; }
}
