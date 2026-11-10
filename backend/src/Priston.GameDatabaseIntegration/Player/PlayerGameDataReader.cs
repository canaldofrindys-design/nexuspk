using Dapper;
using Microsoft.Extensions.Options;
using Priston.Application.Player;
using Priston.GameDatabaseIntegration.Accounts;
using Priston.GameDatabaseIntegration.Connection;
using static Priston.GameDatabaseIntegration.SqlIdentifier;

namespace Priston.GameDatabaseIntegration.Player;

/// <summary>
/// Dados de uma conta nos bancos do jogo, só leitura. Tabelas e colunas confirmadas em DATABASE_MAPPING.md:
/// GameUser (RegistDay, BlockChk), ServerDB.LevelTable, ClanDB.UL, UserDB.BattlePoints, ServerDB.Vip, CoinShopDB.UserInfo.
/// Consultas separadas (sem JOIN entre bancos) porque as collations são diferentes.
/// </summary>
internal sealed class PlayerGameDataReader(IGameDbConnectionFactory factory, IOptions<GameDatabaseOptions> options) : IPlayerGameDataReader
{
    public async Task<RawPlayerGameData> ReadAsync(string login, CancellationToken ct)
    {
        var o = options.Value;
        var vc32 = new DbString { Value = login, IsAnsi = true, Length = 32 };
        var vc50 = new DbString { Value = login, IsAnsi = true, Length = 50 };
        var nvc100 = new DbString { Value = login, IsAnsi = false, Length = 100 };

        await using var conn = await factory.OpenReadAsync(ct).ConfigureAwait(false);
        CommandDefinition Cmd(string sql, object args) => new(sql, args, commandTimeout: o.CommandTimeoutSeconds, cancellationToken: ct);

        var account = await conn.QueryFirstOrDefaultAsync<(DateTime? RegistDay, string? BlockChk)?>(Cmd(
            $"SELECT TOP (1) RegistDay, BlockChk FROM {GameAccountTable.QualifiedNameFor(login, o.AccountDatabase)} WHERE userid = @login",
            new { login = vc32 })).ConfigureAwait(false);

        var characters = (await conn.QueryAsync<RawCharacter>(Cmd(
            $"""
            SELECT Nick AS Name, ISNULL(Class, 0) AS ClassCode, ISNULL(Level, 0) AS Level
            FROM {Quote(o.ServerDatabase)}.dbo.LevelTable
            WHERE szID = @login AND Nick IS NOT NULL AND LTRIM(RTRIM(Nick)) <> ''
            """,
            new { login = vc32 })).ConfigureAwait(false)).ToList();

        var clans = (await conn.QueryAsync<RawClanMember>(Cmd(
            $"""
            SELECT ChName AS CharacterName, ClanName
            FROM {Quote(o.ClanDatabase)}.dbo.UL
            WHERE userid = @login
            """,
            new { login = nvc100 })).ConfigureAwait(false)).ToList();

        var battlePoints = (await conn.QueryAsync<RawBattlePoints>(Cmd(
            $"""
            SELECT Nick, BPs AS BattlePoints, KillsPvP AS Kills, MortesPvP AS Deaths
            FROM {Quote(o.UserDatabase)}.dbo.BattlePoints
            WHERE Account = @login AND Nick IS NOT NULL
            """,
            new { login = vc50 })).ConfigureAwait(false)).ToList();

        var vips = (await conn.QueryAsync<RawVip>(Cmd(
            $"SELECT IdVip AS [Plan], DateStart AS StartLocal, DateEnd AS EndLocal FROM {Quote(o.ServerDatabase)}.dbo.Vip WHERE AccountID = @login",
            new { login = vc32 })).ConfigureAwait(false)).ToList();

        var coins = await conn.ExecuteScalarAsync<int?>(Cmd(
            $"SELECT TOP (1) UserCoin FROM {Quote(o.CoinShopDatabase)}.dbo.UserInfo WHERE UserID = @login",
            new { login = vc32 })).ConfigureAwait(false);

        return new RawPlayerGameData(
            account?.RegistDay,
            account is { } a && GameAccountStore.IsSet(a.BlockChk),
            characters,
            clans,
            battlePoints,
            vips,
            coins);
    }
}
