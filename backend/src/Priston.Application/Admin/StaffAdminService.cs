using System.Globalization;
using System.Text.Json;
using Priston.Application.Accounts;

namespace Priston.Application.Admin;

public sealed record CreateStaffRequest(string? Username, string? DisplayName, string? Role, string? TemporaryPassword);

public sealed record UpdateStaffRequest(string? DisplayName, string? Role, bool? Active);

public sealed record ResetStaffPasswordRequest(string? TemporaryPassword);

/// <summary>
/// Gestão da equipe (só superadministrador). A senha inicial é temporária: o membro troca no primeiro acesso.
/// Sempre sobra pelo menos um superadministrador ativo.
/// </summary>
public sealed class StaffAdminService(IStaffRepository staff, IStaffSessionStore sessions, IAuditLog audit, TimeProvider clock)
{
    public async Task<IReadOnlyList<StaffListItem>> ListAsync(CancellationToken ct) =>
        (await staff.ListAsync(ct).ConfigureAwait(false))
            .Select(u => new StaffListItem(u.Id, u.Username, u.DisplayName, u.Role, u.Active, u.MustChangePassword, u.CreatedUtc, u.LastLoginUtc))
            .ToList();

    public async Task<StaffResult> CreateAsync(StaffPrincipal me, CreateStaffRequest r, ClientInfo client, CancellationToken ct)
    {
        var errors = new Dictionary<string, string>();
        var username = r.Username?.Trim() ?? string.Empty;
        var display = StaffText.Normalize(r.DisplayName ?? string.Empty);
        if (!StaffAuthService.UsernamePattern().IsMatch(username))
        {
            errors["username"] = "Use de 3 a 32 letras, números, ponto, hífen ou sublinhado.";
        }

        if (display.Length is 0 or > 64)
        {
            errors["displayName"] = "Informe um nome de exibição de até 64 caracteres.";
        }

        if (!StaffRoles.IsValid(r.Role))
        {
            errors["role"] = "Escolha uma função.";
        }

        if (StaffPassword.Validate(r.TemporaryPassword, username) is { } pwError)
        {
            errors["temporaryPassword"] = pwError;
        }

        if (errors.Count > 0)
        {
            return new(StaffAuthStatus.ValidationFailed, errors);
        }

        var id = await staff.CreateAsync(username, display, StaffPassword.Hash(r.TemporaryPassword!), r.Role!, me.Username, ct).ConfigureAwait(false);
        if (id is null)
        {
            return StaffResult.Invalid("username", "Este usuário já existe.");
        }

        await audit.WriteAsync("staff", me.Username, "staff.created", "staff", Id(id.Value), client.IpAddress,
            JsonSerializer.Serialize(new { username, displayName = display, role = r.Role }), ct).ConfigureAwait(false);
        return StaffResult.Ok;
    }

    public async Task<StaffResult> UpdateAsync(StaffPrincipal me, int id, UpdateStaffRequest r, ClientInfo client, CancellationToken ct)
    {
        var user = await staff.FindByIdAsync(id, ct).ConfigureAwait(false);
        if (user is null)
        {
            return StaffResult.Of(StaffAuthStatus.NotFound);
        }

        var display = StaffText.Normalize(r.DisplayName ?? user.DisplayName);
        var role = r.Role ?? user.Role;
        var active = r.Active ?? user.Active;
        if (display.Length is 0 or > 64)
        {
            return StaffResult.Invalid("displayName", "Informe um nome de exibição de até 64 caracteres.");
        }

        if (!StaffRoles.IsValid(role))
        {
            return StaffResult.Invalid("role", "Escolha uma função.");
        }

        if (id == me.Id && (!active || role != user.Role))
        {
            return StaffResult.Invalid("role", "Você não pode desativar nem mudar a própria função.");
        }

        var losesSuper = user.Role == StaffRoles.SuperAdmin && user.Active && (role != StaffRoles.SuperAdmin || !active);
        if (losesSuper && await staff.CountActiveSuperAdminsAsync(ct).ConfigureAwait(false) <= 1)
        {
            return StaffResult.Invalid("role", "É preciso manter pelo menos um superadministrador ativo.");
        }

        await staff.UpdateAsync(id, display, role, active, ct).ConfigureAwait(false);
        if (!active || role != user.Role)
        {
            // Mudança de acesso vale na hora: as sessões abertas são encerradas.
            await sessions.RevokeAllAsync(id, null, clock.GetUtcNow(), ct).ConfigureAwait(false);
        }

        await audit.WriteAsync("staff", me.Username, "staff.updated", "staff", Id(id), client.IpAddress,
            JsonSerializer.Serialize(new { before = new { user.DisplayName, user.Role, user.Active }, after = new { DisplayName = display, Role = role, Active = active } }), ct).ConfigureAwait(false);
        return StaffResult.Ok;
    }

    public async Task<StaffResult> ResetPasswordAsync(StaffPrincipal me, int id, ResetStaffPasswordRequest r, ClientInfo client, CancellationToken ct)
    {
        var user = await staff.FindByIdAsync(id, ct).ConfigureAwait(false);
        if (user is null)
        {
            return StaffResult.Of(StaffAuthStatus.NotFound);
        }

        if (id == me.Id)
        {
            return StaffResult.Invalid("temporaryPassword", "Para a sua própria senha, use \"Trocar senha\".");
        }

        if (StaffPassword.Validate(r.TemporaryPassword, user.Username) is { } error)
        {
            return StaffResult.Invalid("temporaryPassword", error);
        }

        var now = clock.GetUtcNow();
        await staff.SetPasswordAsync(id, StaffPassword.Hash(r.TemporaryPassword!), true, now, ct).ConfigureAwait(false);
        await sessions.RevokeAllAsync(id, null, now, ct).ConfigureAwait(false);
        await audit.WriteAsync("staff", me.Username, "staff.password-reset", "staff", Id(id), client.IpAddress, null, ct).ConfigureAwait(false);
        return StaffResult.Ok;
    }

    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);
}
