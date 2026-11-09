using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CCBot.Data.Entities;

[Index(nameof(Occurred), nameof(RemindAt))]
public class Reminder
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public DateTimeOffset RemindAt { get; set; } // UTC; Npgsql requires offset 0
    public bool Occurred { get; set; }
}

public class Event
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public List<Reminder> Reminders { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } // UTC
    public DateTimeOffset OccursAt { get; set; }  // UTC
    [MaxLength(50)]
    public string TimeZone { get; set; } = "America/Toronto";
    [MaxLength(50)]
    public string Name { get; set; }
    public ulong ChannelId { get; set; }
}

public class Group
{
    public int Id { get; set; }
    public ulong[] Users { get; set; } = [];
    [MaxLength(100)]
    public required string Name { get; set; }
    public List<Event> Events { get; set; } = [];
}