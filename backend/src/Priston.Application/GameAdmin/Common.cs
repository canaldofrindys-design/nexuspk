using System.Text.Json;
using Priston.Application.Accounts;
using Priston.Application.Admin;

namespace Priston.Application.GameAdmin;

public enum GameAdminStatus
{
    Ok,
    ValidationFailed,
    NotFound,

    /// <summary>Função desligada: falta o login de administração do jogo ou a função não foi configurada.</summary>
    Disabled,

    /// <summary>O dado mudou ou não identifica uma linha só (tabela sem chave, linha duplicada).</summary>
    Conflict,

    /// <summary>A senha de confirmação está errada.</summary>
    ReauthFailed,

    /// <summary>A função do membro não permite a ação.</summary>
    Forbidden,
}

public sealed record GameAdminResult(GameAdminStatus Status, string? Message = null, IReadOnlyDictionary<string, string>? Errors = null, object? Data = null)
{
    public static GameAdminResult Ok(object? data = null, string? message = null) => new(GameAdminStatus.Ok, message, null, data);

    public static GameAdminResult Invalid(string field, string message) =>
        new(GameAdminStatus.ValidationFailed, "Confira os campos destacados.", new Dictionary<string, string> { [field] = message });

    public static GameAdminResult Invalid(IReadOnlyDictionary<string, string> errors) => new(GameAdminStatus.ValidationFailed, "Confira os campos destacados.", errors);

    public static GameAdminResult Disabled(string message) => new(GameAdminStatus.Disabled, message);

    public static GameAdminResult Conflict(string message) => new(GameAdminStatus.Conflict, message);

    public static GameAdminResult NotFound(string message = "Não encontrado.") => new(GameAdminStatus.NotFound, message);

    public static GameAdminResult WrongPassword { get; } = new(GameAdminStatus.ReauthFailed, "Senha incorreta. A ação não foi feita.",
        new Dictionary<string, string> { ["password"] = "Senha incorreta." });
}

/// <summary>Configuração do painel do jogo (appsettings / portal.json, seção GameAdmin).</summary>
public sealed class GameAdminOptions
{
    public const string SectionName = "GameAdmin";

    /// <summary>Nome do plano por número de IdVip (ServerDB.Vip). Ex.: { "1": "VIP Bronze" }. Sem nome, o painel mostra o número.</summary>
    public Dictionary<string, string> VipPlans { get; set; } = [];

    /// <summary>Maior mudança de coins numa operação para quem não é superadministrador.</summary>
    public int MaxCoinChange { get; set; } = 100_000;

    /// <summary>Maior duração de VIP (dias) numa operação para quem não é superadministrador.</summary>
    public int MaxVipDays { get; set; } = 90;
}

/// <summary>Grava auditoria das ações no jogo sempre como JSON e sempre com o motivo.</summary>
internal static class GameAudit
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public static Task WriteAsync(IAuditLog audit, StaffPrincipal me, string action, string targetType, string targetId, ClientInfo client, object details, CancellationToken ct) =>
        audit.WriteAsync("staff", me.Username, action, targetType, Cut(targetId, 100), client.IpAddress, JsonSerializer.Serialize(details, Json), ct);

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max];
}

internal static class Reason
{
    public const int MaxLength = 300;

    /// <summary>Motivo obrigatório em toda ação no jogo: fica na auditoria.</summary>
    public static string? Clean(string? reason, out string? error)
    {
        var r = (reason ?? string.Empty).Trim();
        if (r.Length < 3)
        {
            error = "Escreva o motivo (fica registrado na auditoria).";
            return null;
        }

        if (r.Length > MaxLength)
        {
            error = $"Use no máximo {MaxLength} caracteres.";
            return null;
        }

        error = null;
        return r;
    }
}

/// <summary>Datas do jogo ficam no horário de Brasília, sem fuso.</summary>
public static class GameTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(Priston.Domain.Time.RankingPeriods.TimeZoneId);

    public static DateTimeOffset? ToUtc(DateTime? local) =>
        local is null ? null : new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Value, DateTimeKind.Unspecified), Zone), TimeSpan.Zero);

    public static DateTime ToLocal(DateTimeOffset utc) => TimeZoneInfo.ConvertTimeFromUtc(utc.UtcDateTime, Zone);
}
