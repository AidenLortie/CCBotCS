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
        var due = await db.Reminders
            .Where(r => r.RemindAt <= DateTimeOffset.UtcNow)
            .ToListAsync(ct);

        foreach (var reminder in due)
        {
            var message = new MessageProperties
            {
                Content = $"⏰ <@{reminder.UserId}> **{reminder.Name}** is happening now!",
                // Only ping the user, even if the name contains @everyone or role mentions
                AllowedMentions = new AllowedMentionsProperties
                {
                    Everyone = false,
                    AllowedRoles = [],
                    AllowedUsers = [reminder.UserId],
                },
            };

            try
            {
                await client.Rest.SendMessageAsync(reminder.ChannelId, message, cancellationToken: ct);
            }
            catch (RestException)
            {
                // Can't post in that channel (no permission, bot not in server, etc.), so try a DM
                try
                {
                    var dm = await client.Rest.GetDMChannelAsync(reminder.UserId, cancellationToken: ct);
                    await dm.SendMessageAsync(message, cancellationToken: ct);
                }
                catch (RestException ex)
                {
                    logger.LogWarning(ex, "Couldn't deliver reminder {Id}", reminder.Id);
                }
            }

            db.Reminders.Remove(reminder); // remove even on failure so it doesn't retry forever
        }

        await db.SaveChangesAsync(ct);
    }
}