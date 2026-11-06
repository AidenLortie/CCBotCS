using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CCBot.Data;

public class DesignTimeFactory : IDesignTimeDbContextFactory<CCBotDbContext>
{
    public CCBotDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CCBotDbContext>()
            .UseNpgsql("Host=localhost;Database=ccbotdb;Username=ccbot_dev;Password=ccbot")
            .Options);
}