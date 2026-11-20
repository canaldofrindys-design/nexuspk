using Priston.Application.Accounts;
using Priston.Application.Admin;

namespace Priston.Application.GameAdmin;

/// <summary>
/// Confirmação com a senha do membro antes de ações perigosas (banir, apagar, zerar ranking, ligar/desligar o servidor).
/// Senha errada conta como falha de login: cinco seguidas bloqueiam a conta por 15 minutos e encerram as sessões.
/// </summary>
public sealed class StaffReauthService(IStaffRepository staff, IStaffSessionStore sessions, IAuditLog audit, TimeProvider clock)
{
    public async Task<bool> VerifyAsync(StaffPrincipal me, string? password, ClientInfo client, CancellationToken ct)
    {
        var user = await staff.FindByIdAsync(me.Id, ct).ConfigureAwait(false);
        var supplied = password ?? string.Empty;
        var ok = supplied.Length is > 0 and <= StaffPassword.MaxLength
                 & StaffPassword.Verify(supplied, user?.PasswordHash ?? StaffPassword.Dummy);
        if (user is null || !user.Active)
        {
            return false;
        }

        var now = clock.GetUtcNow();
        if (ok && !(user.LockedUntilUtc > now))
        {
            return true;
        }

        var locks = user.FailedLogins + 1 >= StaffAuthService.MaxFailures;
        await staff.RecordFailureAsync(user.Id, locks ? now + StaffAuthService.LockDuration : null, ct).ConfigureAwait(false);
        await audit.WriteAsync("staff", me.Username, locks ? "staff.reauth-locked" : "staff.reauth-failed", "staff", user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), client.IpAddress, null, ct).ConfigureAwait(false);
        if (locks)
        {
            await sessions.RevokeAllAsync(user.Id, null, now, ct).ConfigureAwait(false);
        }

        return false;
    }
}
