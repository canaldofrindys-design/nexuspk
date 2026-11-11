using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using System.Text.Json.Serialization;
using Priston.Api.Endpoints;
using Priston.Api.Health;
using Priston.Application;
using Priston.Api.Security;
using Priston.GameDatabaseIntegration;
using Priston.Infrastructure;
using Priston.Infrastructure.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;

// Em produção roda como serviço Windows (o diretório atual é System32): a raiz é a pasta do programa.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : null,
});
builder.Services.AddWindowsService(o => o.ServiceName = "NexusPortalApi");
builder.Configuration.AddPortalConfigFile();

builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddProblemDetails();
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));
builder.Services.AddGameDatabaseIntegration(builder.Configuration);

// A API fica atrás do Next (mesma máquina). Só confia no X-Forwarded-For vindo de 127.0.0.1/::1.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    o.ForwardLimit = 1;
    o.KnownProxies.Clear();
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Add(IPAddress.Loopback);
    o.KnownProxies.Add(IPAddress.IPv6Loopback);
});

// Limite global por IP e um limite mais rígido para cadastro, login e recuperação.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.RateLimitPolicy, ctx =>
        IsInternalCall(ctx)
            ? RateLimitPartition.GetNoLimiter("internal")
            : RateLimitPartition.GetSlidingWindowLimiter(
                ctx.Connection.RemoteIpAddress!.ToString(),
                _ => new SlidingWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0 }));
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitByClient(ctx,
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddHsts(o => o.MaxAge = TimeSpan.FromDays(365));

var app = builder.Build();

app.UseForwardedHeaders();
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
app.MapAuthEndpoints();
app.MapPlayerEndpoints();
app.MapRankingEndpoints();
app.MapContentEndpoints();
app.MapAdminEndpoints();
app.MapSupportEndpoints();

app.MapGet("/api/v1/system/info", (TimeProvider clock) => Results.Ok(new
{
    name = "NexusPK Portal API",
    version = typeof(Program).Assembly.GetName().Version?.ToString(3),
    serverTimeUtc = clock.GetUtcNow(),
    timeZone = Priston.Domain.Time.RankingPeriods.TimeZoneId,
}));

app.Run();

// Chamada do próprio servidor do Next sem IP de jogador (renderização de páginas, cache):
// depois do UseForwardedHeaders o IP remoto continua sendo loopback. Não entra no limite.
static bool IsInternalCall(HttpContext ctx) =>
    ctx.Connection.RemoteIpAddress is null || IPAddress.IsLoopback(ctx.Connection.RemoteIpAddress);

static RateLimitPartition<string> RateLimitByClient(HttpContext ctx, Func<string, FixedWindowRateLimiterOptions> factory) =>
    IsInternalCall(ctx)
        ? RateLimitPartition.GetNoLimiter("internal")
        : RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress!.ToString(), factory);

public partial class Program;
