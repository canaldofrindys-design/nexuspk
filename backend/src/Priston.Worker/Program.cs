using Priston.Application;
using Priston.GameDatabaseIntegration;
using Priston.Infrastructure;
using Priston.Worker.Jobs;
using Quartz;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Roda como serviço Windows em produção; como console no desenvolvimento.
builder.Services.AddWindowsService(o => o.ServiceName = "NexusPK Portal Worker");
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
});
builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

var host = builder.Build();
host.Run();
