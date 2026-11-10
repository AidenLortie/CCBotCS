using System.Text.RegularExpressions;
using CCBot.Data;
using CCBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Group = CCBot.Data.Entities.Group;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace CCBot;

[SlashCommand("schedule", "Schedule a reminder")]
public partial class ReminderModule(IDbContextFactory<CCBotDbContext> dbFactory)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SubSlashCommand("in", "Remind you after a delay")]
    public async Task ScheduleInAsync(
        [SlashCommandParameter(Description = "What to remind you about", MaxLength = 50)] string name,
        [SlashCommandParameter(Description = "How long from now, e.g. 30m, 2h, 1d 4h")] string duration)
    {
        if (ParseDuration(duration) is not TimeSpan delay || delay < TimeSpan.FromMinutes(1))
        {
            await ReplyErrorAsync("Couldn't read that time. Try something like `30m`, `2h`, or `1d 4h`.");
            return;
        }

        await SaveEventAsync(name, DateTimeOffset.UtcNow + delay);
    }

    [SubSlashCommand("at", "Remind you at a specific date and time")]
    public async Task ScheduleAtAsync(
        [SlashCommandParameter(Description = "What to remind you about", MaxLength = 50)] string name,
        [SlashCommandParameter(Description = "Month (1-12)", MinValue = 1, MaxValue = 12)] int month,
        [SlashCommandParameter(Description = "Day of the month", MinValue = 1, MaxValue = 31)] int day,
        [SlashCommandParameter(Description = "24-hour time, e.g. 1800, 0030, 1245", MinValue = 0, MaxValue = 2359)] int time,
        [SlashCommandParameter(Description = "Year (defaults to this year)", MinValue = 2000, MaxValue = 9999)] int? year = null,
        [SlashCommandParameter(Description = "Time zone, e.g. America/New_York or UTC (defaults to America/Toronto)")] string timezone = "America/Toronto")
    {
        // "0030" arrives as 30, so integer math still gives 00:30.
        int hour = time / 100, minute = time % 100;
        if (hour > 23 || minute > 59)
        {
            await ReplyErrorAsync("That time doesn't look right. Use 24-hour format like `0930` or `1800`.");
            return;
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out var tz))
        {
            await ReplyErrorAsync($"I don't recognize the time zone `{timezone}`. Try something like `America/New_York` or `Europe/London`.");
            return;
        }

        var y = year ?? TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).Year;
        if (day > DateTime.DaysInMonth(y, month))
        {
            await ReplyErrorAsync($"That month only has {DateTime.DaysInMonth(y, month)} days in {y}.");
            return;
        }

        var local = new DateTime(y, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        if (tz.IsInvalidTime(local))
        {
            await ReplyErrorAsync("That time is skipped by a daylight saving change in that time zone. Pick a slightly different time.");
            return;
        }

        var remindAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, tz), TimeSpan.Zero);
        if (remindAt < DateTimeOffset.UtcNow.AddMinutes(1))
        {
            await ReplyErrorAsync("That time is in the past (or less than a minute away).");
            return;
        }

        await SaveEventAsync(name, remindAt, tz.Id);
    }

    private async Task SaveEventAsync(string name, DateTimeOffset occursAt, string? timeZone = null)
    {
        await RespondAsync(InteractionCallback.DeferredMessage());

        await using var db = await dbFactory.CreateDbContextAsync();
        var group = await GetOrCreatePersonalGroupAsync(db);

        var ev = new Event
        {
            Group = group,
            Name = name,
            ChannelId = Context.Channel.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            OccursAt = occursAt,
            Reminders = [new Reminder { RemindAt = occursAt }],
        };
        if (timeZone is not null)
            ev.TimeZone = timeZone;

        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var unix = occursAt.ToUnixTimeSeconds();
        await ModifyResponseAsync(m =>
            m.Content = $"Got it! I'll remind you about **{name}** <t:{unix}:R> (<t:{unix}:f>).");
    }

    // Each user gets a single-member group that their personal reminders hang off of.
    private async Task<Group> GetOrCreatePersonalGroupAsync(CCBotDbContext db)
    {
        var userId = Context.User.Id;
        var group = await db.Groups
            .FirstOrDefaultAsync(g => g.Users.Length == 1 && g.Users[0] == userId);

        if (group is null)
        {
            group = new Group { Name = Context.User.Username, Users = [userId] };
            db.Groups.Add(group);
        }

        return group;
    }

    private Task ReplyErrorAsync(string message) =>
        RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content = message,
            Flags = MessageFlags.Ephemeral,
        }));

    [GeneratedRegex(@"(\d+)\s*([dhm])")]
    private static partial Regex DurationRegex();

    private static TimeSpan? ParseDuration(string input)
    {
        var matches = DurationRegex().Matches(input.ToLowerInvariant());
        if (matches.Count == 0)
            return null;

        var total = TimeSpan.Zero;
        foreach (Match m in matches)
        {
            if (!int.TryParse(m.Groups[1].Value, out var n) || n > 10_000)
                return null;

            total += m.Groups[2].Value switch
            {
                "d" => TimeSpan.FromDays(n),
                "h" => TimeSpan.FromHours(n),
                _   => TimeSpan.FromMinutes(n),
            };
        }
        return total;
    }
}