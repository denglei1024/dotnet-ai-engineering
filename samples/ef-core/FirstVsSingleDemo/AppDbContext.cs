using Microsoft.EntityFrameworkCore;

namespace FirstVsSingleDemo;

public class AppDbContext: DbContext
{
    public DbSet<User> Users => Set<User>();
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=app.db");
    }
}