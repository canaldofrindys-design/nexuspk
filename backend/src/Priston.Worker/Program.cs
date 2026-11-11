using Priston.Application;
using Priston.GameDatabaseIntegration;
using Microsoft.Extensions.Hosting.WindowsServices;
using Priston.Infrastructure;
using Priston.Infrastructure.Hosting;
using Priston.Worker.Jobs;
using Quartz;
using Serilog;

// Roda como serviço Windows em produção; como console no desenvolvimento.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : null,
});
builder.Services.AddWindowsService(o => o.ServiceName = "NexusPortalWorker");
builder.Configuration.AddPortalConfigFile();
builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGameDatabaseIntegration(builder.Configuration);

builder.Services.AddQuartz(q =>
{
    var probe = new JobKey(nameof(IntegrationProbeJob));
    q.AddJob<IntegrationProbeJob>(probe);
    q.AddTrigger(t => t.ForJob(probe)
        .WithIdentity($"{nameof(IntegrationProbeJob)}-trigger")
        .StartNow()
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));

    // Foto diária de nível: 00:05 em Brasília, a cada 30 min como reforço (só grava se faltar a do dia)
    // e uma vez ao iniciar o serviço.
    var snapshot = new JobKey(nameof(LevelSnapshotJob));
    q.AddJob<LevelSnapshotJob>(snapshot);
    var brasilia = TimeZoneInfo.FindSystemTimeZoneById(Priston.Domain.Time.RankingPeriods.TimeZoneId);
    q.AddTrigger(t => t.ForJob(snapshot)
        .WithIdentity($"{nameof(LevelSnapshotJob)}-daily")
        .WithCronSchedule("0 5 0 * * ?", c => c.InTimeZone(brasilia)));
    q.AddTrigger(t => t.ForJob(snapshot)
        .WithIdentity($"{nameof(LevelSnapshotJob)}-retry")
        .StartNow()
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(30).RepeatForever()));

    var reminders = new JobKey(nameof(EventReminderJob));
    q.AddJob<EventReminderJob>(reminders);
    q.AddTrigger(t => t.ForJob(reminders)
        .WithIdentity($"{nameof(EventReminderJob)}-trigger")
        .StartNow()
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));

    var serverProbe = new JobKey(nameof(ServerProbeJob));
    q.AddJob<ServerProbeJob>(serverProbe);
    q.AddTrigger(t => t.ForJob(serverProbe)
        .WithIdentity($"{nameof(ServerProbeJob)}-trigger")
        .StartNow()
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(1).RepeatForever()));
});
builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

var host = builder.Build();
host.Run();
