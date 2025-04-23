using KSol.NextCloudMailToFolder.Models;
using Microsoft.EntityFrameworkCore;

namespace KSol.NextCloudMailToFolder.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
        if(Database.GetPendingMigrations().Any())
        {
            Database.Migrate();
        }
    }

    public DbSet<Destination> Destinations { get; set; }
    public DbSet<NextCloudUser> NextCloudUsers { get; set; }
}
