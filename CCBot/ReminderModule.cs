using System.Text.RegularExpressions;
using CCBot.Data;
using CCBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace CCBot;

public partial class ReminderModule(IDbContextFactory<CCBotDbContext> dbFactory)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("schedule", "Schedule an event to be reminded of")]
    public async Task ScheduleAsync(
        [SlashCommandParameter(Description = "What to remind you about", MaxLength = 200)] string name,
        [SlashCommandParameter(Name = "in", Description = "When, e.g. 30m, 2h, 1d 4h")] string when)
    {
        if (ParseDuration(when) is not TimeSpan delay || delay < TimeSpan.FromMinutes(1))
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Content = "Couldn't read that time. Try something like `30min`, `2h`,  or `1d 4h",
                Flags = MessageFlags.Ephemeral,
            }));
            return;
        }

        await RespondAsync(InteractionCallback.DeferredMessage());
        
        var remindAt = DateTimeOffset.UtcNow + delay;

        await using var db = await dbFactory.CreateDbContextAsync();
        db.Reminders.Add(new Reminder
        {
            UserId = Context.User.Id,
            ChannelId = Context.Channel.Id,
            Name = name,
            RemindAt = remindAt,
        });
        await db.SaveChangesAsync();

        var unix = remindAt.ToUnixTimeSeconds();
        await ModifyResponseAsync(m =>
            m.Content = $"Got it! I'll remind you about **{name}** <t:{unix}:R> (<t:{unix}:f>).");
    }

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