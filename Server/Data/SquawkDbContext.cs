using Microsoft.EntityFrameworkCore;
using System.IO;

namespace Server.Data
{
    public class SquawkDbContext : DbContext
    {
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<ScoreEntry> Scores { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Puts the database file in the same directory as the executable
            string dbPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "squawk.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}
