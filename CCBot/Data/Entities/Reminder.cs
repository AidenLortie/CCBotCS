namespace CCBot.Data.Entities;

public class Reminder
{
    public int Id { get; set; }
    public ulong UserId { get; set; }
    public ulong ChannelId { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset RemindAt { get; set; } // always UTC; Npgsql requires offset 0
}