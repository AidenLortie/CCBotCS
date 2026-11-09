using CCBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Rest;

namespace CCBot;

public class ReminderService(
    IDbContextFactory<CCBotDbContext> dbFactory,
    GatewayClient client,
    ILogger<ReminderService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                await SendDueRemindersAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Reminder check failed");
            }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    private async Task SendDueRemindersAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var due = await db.Reminders
            .Where(r => !r.Occurred && r.RemindAt <= now) // matches IX_Reminders_Occurred_RemindAt
            .Include(r => r.Event)
            .ThenInclude(e => e.Group)
            .ToListAsync(ct);

        foreach (var reminder in due)
        {
            var ev = reminder.Event;
            var users = ev.Group.Users;
            var mentions = string.Join(' ', users.Select(u => $"<@{u}>"));

            // A reminder can fire ahead of the event, so only say "now" once it has actually started.
            var when = ev.OccursAt <= now.AddMinutes(1)
                ? "is happening now!"
                : $"starts <t:{ev.OccursAt.ToUnixTimeSeconds()}:R>.";

            var message = new MessageProperties
            {
                Content = $"⏰ {mentions} **{ev.Name}** {when}",
                // Only ping the group's members, even if the name contains @everyone or role mentions
                AllowedMentions = new AllowedMentionsProperties
                {
                    Everyone = false,
                    AllowedRoles = [],
                    AllowedUsers = users,
                },
            };

            try
            {
                await client.Rest.SendMessageAsync(ev.ChannelId, message, cancellationToken: ct);
            }
            catch (RestException)
            {
                // Can't post in that channel (no permission, bot not in server, etc.), so DM each member
                foreach (var userId in users)
                {
                    try
                    {
                        var dm = await client.Rest.GetDMChannelAsync(userId, cancellationToken: ct);
                        await dm.SendMessageAsync(message, cancellationToken: ct);
                    }
                    catch (RestException ex)
                    {
                        logger.LogWarning(ex, "Couldn't deliver reminder {Id} to user {UserId}", reminder.Id, userId);
                    }
                }
            }

            reminder.Occurred = true; // mark even on failure so it doesn't retry forever
        }

        await db.SaveChangesAsync(ct);
    }
}
