using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Priston.Application.GameAdmin;
using Priston.GameDatabaseIntegration.Accounts;
using Priston.GameDatabaseIntegration.Connection;

namespace Priston.GameDatabaseIntegration.Admin;

/// <summary>
/// Contas no painel da equipe, com o login de administração. Só tabelas e colunas do DATABASE_MAPPING.md:
/// &lt;letra&gt;GameUser, ServerDB.LevelTable e Vip, CoinShopDB.UserInfo, UserDB.GameMasters e BattlePoints, ClanDB.UL.
/// </summary>
internal sealed class GameAdminAccountStore(IGameDbConnectionFactory factory, IOptions<GameDatabaseOptions> options) : IGameAdminAccountStore
{
    private GameDatabaseOptions O => options.Value;

    public bool Enabled => factory.CanAdmin;

    private string Server => SqlIdentifier.Quote(O.ServerDatabase);

    private string User => SqlIdentifier.Quote(O.UserDatabase);

    private string Clan => SqlIdentifier.Quote(O.ClanDatabase);

    private string Coin => SqlIdentifier.Quote(O.CoinShopDatabase);

    private CommandDefinition Cmd(string sql, object? args, CancellationToken ct, IDbTransaction? tx = null) =>
        new(sql, args, tx, commandTimeout: O.CommandTimeoutSeconds, cancellationToken: ct);

    private static DbString Ansi(string? value, int length = 32) => new() { Value = value, IsAnsi = true, Length = length };

    private static string LikePrefix(string term) =>
        term.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal).Replace("[", "\\[", StringComparison.Ordinal) + "%";

    public async Task<IReadOnlyList<AccountSearchHit>> SearchAsync(string term, int take, CancellationToken ct)
    {
        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        var hits = new Dictionary<string, AccountSearchHit>(StringComparer.OrdinalIgnoreCase);
        var like = LikePrefix(term);

        if (GameAccountTable.IsValidLogin(term))
        {
            var table = GameAccountTable.QualifiedNameFor(term, O.AccountDatabase);
            var rows = await conn.QueryAsync<AccountRow>(Cmd(
                $"SELECT TOP (@take) userid AS Login, RegistDay, BlockChk, DelChk FROM {table} WHERE userid LIKE @like ESCAPE '\\' ORDER BY userid",
                new { take, like = Ansi(like, 40) }, ct)).ConfigureAwait(false);
            foreach (var r in rows)
            {
                hits.TryAdd(r.Login.Trim(), new AccountSearchHit(r.Login.Trim(), null, IsSet(r.BlockChk), IsSet(r.DelChk), r.RegistDay));
            }
        }

        var chars = (await conn.QueryAsync<(string? Account, string? Nick)>(Cmd(
            $"SELECT TOP (@take) szID, Nick FROM {Server}.dbo.LevelTable WHERE Nick LIKE @like ESCAPE '\\' ORDER BY Nick",
            new { take, like = Ansi(like, 40) }, ct)).ConfigureAwait(false)).ToList();
        foreach (var (account, nick) in chars)
        {
            var login = account?.Trim();
            if (login is null || !GameAccountTable.IsValidLogin(login) || hits.ContainsKey(login) || hits.Count >= take)
            {
                continue;
            }

            var row = await conn.QueryFirstOrDefaultAsync<AccountRow>(Cmd(
                $"SELECT TOP (1) userid AS Login, RegistDay, BlockChk, DelChk FROM {GameAccountTable.QualifiedNameFor(login, O.AccountDatabase)} WHERE userid = @login",
                new { login = Ansi(login) }, ct)).ConfigureAwait(false);
            if (row is not null)
            {
                hits[login] = new AccountSearchHit(row.Login.Trim(), nick?.Trim(), IsSet(row.BlockChk), IsSet(row.DelChk), row.RegistDay);
            }
        }

        return hits.Values.Take(take).ToList();
    }

    public async Task<GameAccountSnapshot?> GetAsync(string login, CancellationToken ct)
    {
        if (!GameAccountTable.IsValidLogin(login))
        {
            return null;
        }

        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        var table = GameAccountTable.QualifiedNameFor(login, O.AccountDatabase);
        var accounts = (await conn.QueryAsync<GameAccountRowRaw>(Cmd(
            $"SELECT userid AS Login, RegistDay, LastDay, BlockChk, DelChk, Grade FROM {table} WHERE userid = @login",
            new { login = Ansi(login) }, ct)).ConfigureAwait(false)).ToList();
        if (accounts.Count == 0)
        {
            return null;
        }

        var canonical = accounts[0].Login.Trim();
        var args = new { login = Ansi(canonical), nlogin = canonical };
        using var grid = await conn.QueryMultipleAsync(Cmd(
            $"""
            SELECT Nick AS Name, Class, Level FROM {Server}.dbo.LevelTable WHERE szID = @login AND Nick IS NOT NULL ORDER BY Level DESC;
            SELECT PlayerName, IdVip AS [Plan], DateStart AS StartLocal, DateEnd AS EndLocal FROM {Server}.dbo.Vip WHERE AccountID = @login;
            SELECT UserCoin FROM {Coin}.dbo.UserInfo WHERE UserID = @login;
            SELECT MAX(GameMasterLevel) FROM {User}.dbo.GameMasters WHERE Account = @login;
            SELECT ClanName, ChName AS CharacterName, Permi AS Permission, DelActive FROM {Clan}.dbo.UL WHERE userid = @nlogin;
            SELECT Nick, BPs AS BattlePoints, BPsSemanal AS WeeklyBattlePoints, KillsPvP AS Kills, MortesPvP AS Deaths FROM {User}.dbo.BattlePoints WHERE Account = @login;
            """, args, ct)).ConfigureAwait(false);
        var characters = (await grid.ReadAsync<CharRaw>().ConfigureAwait(false)).Select(c => new GameCharacter(c.Name, c.Class, c.Level)).ToList();
        var vips = (await grid.ReadAsync<VipRaw>().ConfigureAwait(false)).Select(v => new GameVipRow(v.PlayerName, v.Plan, v.StartLocal, v.EndLocal)).ToList();
        var coins = (await grid.ReadAsync<int?>().ConfigureAwait(false)).ToList();
        var gm = await grid.ReadSingleOrDefaultAsync<int?>().ConfigureAwait(false);
        var clans = (await grid.ReadAsync<ClanRaw>().ConfigureAwait(false)).Select(c => new GameClanRow(c.ClanName, c.CharacterName, c.Permission, c.DelActive)).ToList();
        var pvp = (await grid.ReadAsync<PvpRaw>().ConfigureAwait(false))
            .Select(p => new GamePvpRow(p.Nick?.Trim(), p.BattlePoints, p.WeeklyBattlePoints, p.Kills, p.Deaths)).ToList();

        IReadOnlyList<string> shared = [];
        var names = characters.Select(c => c.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(200).ToList();
        if (names.Count > 0)
        {
            shared = (await conn.QueryAsync<string>(Cmd(
                $"SELECT DISTINCT Nick FROM {Server}.dbo.LevelTable WHERE Nick IN @names AND (szID IS NULL OR szID <> @login)",
                new { names, login = Ansi(canonical) }, ct)).ConfigureAwait(false))
                .Select(n => n.Trim()).ToList();
        }

        return new GameAccountSnapshot(
            accounts.Select(a => new GameAccountRow(a.Login, a.RegistDay, a.LastDay, a.BlockChk, a.DelChk, a.Grade)).ToList(),
            characters, vips, coins, gm, clans, pvp, shared);
    }

    public async Task<int> SetBlockedAsync(string login, bool blocked, CancellationToken ct)
    {
        var table = GameAccountTable.QualifiedNameFor(login, O.AccountDatabase);
        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteAsync(Cmd($"UPDATE {table} SET BlockChk = @flag WHERE userid = @login",
            new { login = Ansi(login), flag = Ansi(blocked ? "1" : "0", 1) }, ct)).ConfigureAwait(false);
    }

    public async Task<CoinChange> ChangeCoinsAsync(string login, Func<long, long?> compute, CancellationToken ct)
    {
        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
        var rows = (await conn.QueryAsync<int?>(Cmd($"SELECT UserCoin FROM {Coin}.dbo.UserInfo WITH (UPDLOCK, HOLDLOCK) WHERE UserID = @login",
            new { login = Ansi(login) }, ct, tx)).ConfigureAwait(false)).ToList();
        if (rows.Count > 1)
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            return new CoinChange(CoinOutcome.Duplicate, 0, 0, false);
        }

        long before = rows.Count == 0 ? 0 : rows[0] ?? 0;
        var after = compute(before);
        if (after is null)
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            return new CoinChange(CoinOutcome.Rejected, before, before, false);
        }

        var sql = rows.Count == 0
            ? $"INSERT INTO {Coin}.dbo.UserInfo (UserID, UserCoin, LastChange) VALUES (@login, @coins, GETDATE())"
            : $"UPDATE {Coin}.dbo.UserInfo SET UserCoin = @coins, LastChange = GETDATE() WHERE UserID = @login";
        await conn.ExecuteAsync(Cmd(sql, new { login = Ansi(login), coins = (int)after.Value }, ct, tx)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return new CoinChange(CoinOutcome.Ok, before, after.Value, rows.Count == 0);
    }

    public async Task AddVipAsync(string login, string? playerName, int plan, DateTime startLocal, DateTime endLocal, CancellationToken ct)
    {
        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd(
            $"INSERT INTO {Server}.dbo.Vip (PlayerName, IdVip, DateStart, DateEnd, AccountID) VALUES (@playerName, @plan, @start, @end, @login)",
            new { playerName = Ansi(playerName), plan, start = startLocal, end = endLocal, login = Ansi(login) }, ct)).ConfigureAwait(false);
    }

    public Task<int> UpdateVipAsync(string login, GameVipRow original, int plan, DateTime startLocal, DateTime endLocal, CancellationToken ct) =>
        ChangeOneVipAsync($"UPDATE {Server}.dbo.Vip SET IdVip = @plan, DateStart = @start, DateEnd = @end", login, original,
            new DynamicParameters(new { plan, start = startLocal, end = endLocal }), ct);

    public Task<int> DeleteVipAsync(string login, GameVipRow original, CancellationToken ct) =>
        ChangeOneVipAsync($"DELETE FROM {Server}.dbo.Vip", login, original, new DynamicParameters(), ct);

    /// <summary>A tabela Vip não tem chave: muda só se exatamente uma linha tiver todos os valores originais.</summary>
    private async Task<int> ChangeOneVipAsync(string statement, string login, GameVipRow o, DynamicParameters p, CancellationToken ct)
    {
        static string Eq(string col, string param, object? value) => value is null ? $"{col} IS NULL" : $"{col} = {param}";
        p.Add("login", Ansi(login));
        p.Add("oName", Ansi(o.PlayerName));
        p.Add("oPlan", o.Plan);
        p.Add("oStart", o.StartLocal, DbType.DateTime);
        p.Add("oEnd", o.EndLocal, DbType.DateTime);
        var where = $"WHERE AccountID = @login AND {Eq("PlayerName", "@oName", o.PlayerName)} AND {Eq("IdVip", "@oPlan", o.Plan)} AND {Eq("DateStart", "@oStart", o.StartLocal)} AND {Eq("DateEnd", "@oEnd", o.EndLocal)}";
        var sql = $"""
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            DECLARE @n int = (SELECT COUNT(*) FROM {Server}.dbo.Vip WITH (UPDLOCK, HOLDLOCK) {where});
            IF @n = 1 BEGIN {statement} {where}; COMMIT TRANSACTION; END
            ELSE ROLLBACK TRANSACTION;
            SELECT @n;
            """;
        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<int>(Cmd(sql, p, ct)).ConfigureAwait(false);
    }

    private static bool IsSet(string? flag) => GameAccountStore.IsSet(flag);

    private sealed record AccountRow(string Login, DateTime? RegistDay, string? BlockChk, string? DelChk);

    private sealed record GameAccountRowRaw(string Login, DateTime? RegistDay, DateTime? LastDay, string? BlockChk, string? DelChk, string? Grade);

    private sealed record CharRaw(string Name, int? Class, int? Level);

    private sealed record VipRaw(string? PlayerName, int? Plan, DateTime? StartLocal, DateTime? EndLocal);

    private sealed record ClanRaw(string? ClanName, string CharacterName, string? Permission, string? DelActive);

    private sealed record PvpRaw(string? Nick, int? BattlePoints, int WeeklyBattlePoints, int Kills, int Deaths);
}
