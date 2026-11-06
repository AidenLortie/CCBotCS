using CCBot;
using CCBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Rest;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddDbContextFactory<CCBotDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default")))
    .AddHostedService<ReminderService>();

builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

builder.Services
    .AddDiscordGateway(options => options.Intents = default) // reads Discord:Token
    .AddApplicationCommands();

var host = builder.Build();

await using (var db = await host.Services.GetRequiredService<IDbContextFactory<CCBotDbContext>>()
                 .CreateDbContextAsync())
{
    await db.Database.MigrateAsync();
}

host.AddModules(typeof(Program).Assembly);

await host.RunAsync();