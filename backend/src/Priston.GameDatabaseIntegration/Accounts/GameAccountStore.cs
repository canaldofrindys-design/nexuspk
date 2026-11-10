using System.Data;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.Options;
using Priston.Application.Accounts;
using Priston.GameDatabaseIntegration.Connection;

namespace Priston.GameDatabaseIntegration.Accounts;

/// <summary>
/// Leitura e escrita das contas em [accountdb].[dbo].[&lt;letra&gt;GameUser].
/// Formato de gravação igual ao do site antigo (único método de cadastro identificado; não há procedure).
/// A coluna Passwd guarda a senha como texto: é o formato que o servidor do jogo compara.
/// </summary>
internal sealed class GameAccountStore(IGameDbConnectionFactory factory, IOptions<GameDatabaseOptions> options) : IGameAccountStore
{
    // Valores gravados pelo site antigo em todo cadastro.
    private const string DefaultGpCode = "PTP-RUD001";
    private const string DefaultDisuseDay = "20301212";

    private GameDatabaseOptions O => options.Value;

    public bool WritesEnabled => factory.CanWrite;

    public async Task<bool> ExistsAsync(string login, CancellationToken ct)
    {
        var table = GameAccountTable.QualifiedNameFor(login, O.AccountDatabase);
        await using var conn = await factory.OpenReadAsync(ct).ConfigureAwait(false);
        var found = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
            $"SELECT TOP (1) 1 FROM {table} WHERE userid = @login",
            new { login = Ansi(login) },
            commandTimeout: O.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
        return found.HasValue;
    }

    public async Task<GameCredentialCheck> CheckPasswordAsync(string login, string password, CancellationToken ct)
    {
        if (!GameAccountTable.IsValidLogin(login))
        {
            return new(GameCredentialResult.NotFound, null);
        }

        var table = GameAccountTable.QualifiedNameFor(login, O.AccountDatabase);
        await using var conn = await factory.OpenReadAsync(ct).ConfigureAwait(false);
        var row = await conn.QueryFirstOrDefaultAsync<AccountRow>(new CommandDefinition(
            $"SELECT TOP (1) userid AS UserId, Passwd AS Password, BlockChk AS BlockChk, DelChk AS DelChk FROM {table} WHERE userid = @login",
            new { login = Ansi(login) },
            commandTimeout: O.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);

        if (row is null)
        {
            // Mesma quantidade de trabalho de uma comparação para reduzir diferença de tempo.
            FixedTimeEquals(password, new string('\0', 32));
            return new(GameCredentialResult.NotFound, null);
        }

        if (!FixedTimeEquals(password, row.Password ?? string.Empty))
        {
            return new(GameCredentialResult.Invalid, row.UserId);
        }

        if (IsSet(row.DelChk))
        {
            return new(GameCredentialResult.Deleted, row.UserId);
        }

        return IsSet(row.BlockChk)
            ? new(GameCredentialResult.Blocked, row.UserId)
            : new(GameCredentialResult.Valid, row.UserId);
    }

    public async Task<GameAccountCreateResult> CreateAsync(string login, string password, string? ipAddress, CancellationToken ct)
    {
        var table = GameAccountTable.QualifiedNameFor(login, O.AccountDatabase);
        var sql = $"""
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            IF EXISTS (SELECT 1 FROM {table} WITH (UPDLOCK, HOLDLOCK) WHERE userid = @login)
            BEGIN
                ROLLBACK TRANSACTION;
                SELECT CAST(0 AS int);
            END
            ELSE
            BEGIN
                INSERT INTO {table}
                    (userid, Passwd, GPCode, RegistDay, DisuseDay, inuse, Grade,
                     EventChk, SelectChk, BlockChk, SpecialChk, Credit, DelChk, Channel)
                VALUES
                    (@login, @password, @gpCode, GETDATE(), @disuseDay, '0', 'U',
                     '0', '0', '0', '0', '0', '0', @channel);
                COMMIT TRANSACTION;
                SELECT CAST(1 AS int);
            END
            """;

        await using var conn = await factory.OpenWriteAsync(ct).ConfigureAwait(false);
        var created = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new
            {
                login = Ansi(login),
                password = Ansi(password),
                gpCode = new DbString { Value = DefaultGpCode, IsAnsi = true, Length = 10 },
                disuseDay = DefaultDisuseDay,
                channel = new DbString { Value = ipAddress is { Length: > 50 } ? ipAddress[..50] : ipAddress, IsAnsi = true, Length = 50 },
            },
            commandTimeout: O.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
        return created == 1 ? GameAccountCreateResult.Created : GameAccountCreateResult.AlreadyExists;
    }

    public async Task<bool> ChangePasswordAsync(string login, string newPassword, CancellationToken ct)
    {
        var table = GameAccountTable.QualifiedNameFor(login, O.AccountDatabase);
        await using var conn = await factory.OpenWriteAsync(ct).ConfigureAwait(false);
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            $"UPDATE {table} SET Passwd = @password WHERE userid = @login",
            new { login = Ansi(login), password = Ansi(newPassword) },
            commandTimeout: O.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
        return rows > 0;
    }

    private static DbString Ansi(string value) => new() { Value = value, IsAnsi = true, Length = GameAccountTable.MaxLoginLength };

    // BlockChk e DelChk são char(1); o cadastro grava '0'. Qualquer outro valor conta como marcado.
    internal static bool IsSet(string? flag) => !string.IsNullOrWhiteSpace(flag) && flag.Trim() != "0";

    internal static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.Latin1.GetBytes(a)),
            SHA256.HashData(Encoding.Latin1.GetBytes(b)));

    private sealed record AccountRow(string UserId, string? Password, string? BlockChk, string? DelChk);
}
