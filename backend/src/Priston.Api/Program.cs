using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using Priston.Api.Endpoints;
using Priston.Api.Health;
using Priston.Application;
using Priston.Api.Security;
using Priston.GameDatabaseIntegration;
using Priston.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddProblemDetails();
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));
builder.Services.AddGameDatabaseIntegration(builder.Configuration);

// Limite global por IP. Limites mais rígidos (login, cadastro) entram na Fase 5.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddHsts(o => o.MaxAge = TimeSpan.FromDays(365));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging();
app.UseRateLimiter();

app.MapPortalHealth();
app.MapHomeEndpoints();

app.MapGet("/api/v1/system/info", (TimeProvider clock) => Results.Ok(new
{
    name = "NexusPK Portal API",
    version = typeof(Program).Assembly.GetName().Version?.ToString(3),
    serverTimeUtc = clock.GetUtcNow(),
    timeZone = Priston.Domain.Time.RankingPeriods.TimeZoneId,
}));

app.Run();

public partial class Program;
