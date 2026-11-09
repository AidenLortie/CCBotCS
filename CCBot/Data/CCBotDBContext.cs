using CCBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CCBot.Data;

public class Tag
{
    public ulong GuildId { get; set; }
    public required string Name { get; set; }
    public required string Content { get; set; }
}

public class CCBotDbContext(DbContextOptions<CCBotDbContext> options) : DbContext(options)
{
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Group> Groups => Set<Group>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
        => builder.Properties<ulong>().HaveConversion<long>(); // Discord IDs stored as bigint

    protected override void OnModelCreating(ModelBuilder builder)
        => builder.Entity<Tag>().HasKey(t => new { t.GuildId, t.Name });
}