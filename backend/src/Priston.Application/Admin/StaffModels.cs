using System.Security.Cryptography;
using System.Text;

namespace Priston.Application.Admin;

public enum Capability
{
    ViewDashboard,
    ManageContent,
    ViewAudit,
    ManageStaff,
    ManageSupport,

    /// <summary>Textos institucionais, downloads e redes sociais do site.</summary>
    ManageSite,
}

/// <summary>Funções da equipe e o que cada uma pode fazer.</summary>
public static class StaffRoles
{
    public const string SuperAdmin = "superadmin";
    public const string Admin = "admin";
    public const string Moderator = "moderador";
    public const string Support = "suporte";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [SuperAdmin] = "Superadministrador",
        [Admin] = "Administrador",
        [Moderator] = "Moderador",
        [Support] = "Suporte",
    };

    private static readonly Dictionary<string, Capability[]> Grants = new(StringComparer.Ordinal)
    {
        [SuperAdmin] = [Capability.ViewDashboard, Capability.ManageContent, Capability.ViewAudit, Capability.ManageStaff, Capability.ManageSupport, Capability.ManageSite],
        [Admin] = [Capability.ViewDashboard, Capability.ManageContent, Capability.ViewAudit, Capability.ManageSupport, Capability.ManageSite],
        [Moderator] = [Capability.ViewDashboard, Capability.ManageContent, Capability.ManageSupport],
        [Support] = [Capability.ViewDashboard, Capability.ManageSupport],
    };

    public static bool IsValid(string? role) => role is not null && Grants.ContainsKey(role);

    public static bool Can(string role, Capability capability) =>
        Grants.TryGetValue(role, out var list) && list.Contains(capability);

    public static IReadOnlyList<Capability> CapabilitiesOf(string role) =>
        Grants.TryGetValue(role, out var list) ? list : [];
}

/// <summary>Senha da equipe: PBKDF2-SHA256, 600 mil iterações, sal de 16 bytes. Formato "pbkdf2-sha256$iter$sal$hash".</summary>
public static class StaffPassword
{
    public const int MinLength = 12;
    public const int MaxLength = 128;
    private const int Iterations = 600_000;
    private const string Scheme = "pbkdf2-sha256";

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Scheme}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Scheme || !int.TryParse(parts[1], out var iterations) || iterations < 100_000)
        {
            return false;
        }

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>Hash fixo para gastar o mesmo tempo quando o usuário não existe.</summary>
    public static readonly string Dummy = Hash("senha-de-referencia-inexistente");

    public static string? Validate(string? password, string? username)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            return $"Use pelo menos {MinLength} caracteres.";
        }

        if (password.Length > MaxLength)
        {
            return $"Use no máximo {MaxLength} caracteres.";
        }

        if (username is not null && password.Contains(username, StringComparison.OrdinalIgnoreCase))
        {
            return "A senha não pode conter o nome de usuário.";
        }

        return password.Distinct().Count() < 5 ? "A senha repete caracteres demais." : null;
    }
}

public sealed record StaffUser(
    int Id,
    string Username,
    string DisplayName,
    string PasswordHash,
    string Role,
    bool Active,
    bool MustChangePassword,
    int FailedLogins,
    DateTimeOffset? LockedUntilUtc,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? LastLoginUtc);

/// <summary>Membro da equipe autenticado na requisição.</summary>
public sealed record StaffPrincipal(int Id, string Username, string DisplayName, string Role, bool MustChangePassword, DateTimeOffset SessionExpiresUtc)
{
    public bool Can(Capability capability) => StaffRoles.Can(Role, capability);
}

public sealed record StaffListItem(int Id, string Username, string DisplayName, string Role, bool Active, bool MustChangePassword, DateTimeOffset CreatedUtc, DateTimeOffset? LastLoginUtc);

public interface IStaffRepository
{
    Task<StaffUser?> FindByUsernameAsync(string username, CancellationToken ct);

    Task<StaffUser?> FindByIdAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<StaffUser>> ListAsync(CancellationToken ct);

    /// <summary>Cria e devolve o Id. Devolve null se o usuário já existe.</summary>
    Task<int?> CreateAsync(string username, string displayName, string passwordHash, string role, string? createdBy, CancellationToken ct);

    Task UpdateAsync(int id, string displayName, string role, bool active, CancellationToken ct);

    Task SetPasswordAsync(int id, string passwordHash, bool mustChange, DateTimeOffset at, CancellationToken ct);

    Task RecordLoginAsync(int id, DateTimeOffset at, CancellationToken ct);

    /// <summary>Soma uma falha; com lockUntil, bloqueia e zera o contador.</summary>
    Task RecordFailureAsync(int id, DateTimeOffset? lockUntil, CancellationToken ct);

    Task<int> CountActiveSuperAdminsAsync(CancellationToken ct);
}

public interface IStaffSessionStore
{
    Task CreateAsync(byte[] tokenHash, int staffId, DateTimeOffset expiresUtc, string? ipAddress, string? userAgent, CancellationToken ct);

    Task<(int StaffId, DateTimeOffset ExpiresUtc)?> FindActiveAsync(byte[] tokenHash, DateTimeOffset now, CancellationToken ct);

    Task RevokeAsync(byte[] tokenHash, DateTimeOffset now, CancellationToken ct);

    Task RevokeAllAsync(int staffId, byte[]? exceptTokenHash, DateTimeOffset now, CancellationToken ct);
}

internal static class StaffText
{
    public static string Normalize(string value) => value.Trim().Normalize(NormalizationForm.FormC);
}
