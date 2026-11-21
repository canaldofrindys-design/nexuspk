using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Priston.Application.GameAdmin;
using Priston.GameDatabaseIntegration.Connection;

namespace Priston.GameDatabaseIntegration.Admin;

/// <summary>
/// Pacotes de Box Donate em NexusPKAdmin, gravados igual às procedures do jogo (NexusCoinShopPurchase e
/// NexusDailyLoginPackage): o pacote PENDENTE, a linha de controle BI354 (Ordinal 0) e os itens (Ordinal 1..64).
/// DistributorNotifiedAt fica vazio para o servidor avisar o jogador (operação NOTICES do NexusDonateDistributor).
/// </summary>
internal sealed class DonateBoxStore(IGameDbConnectionFactory factory, IOptions<GameDatabaseOptions> options) : IDonateBoxStore
{
    private GameDatabaseOptions O => options.Value;

    public bool Enabled => factory.CanAdmin;

    private string Db => SqlIdentifier.Quote(O.DonateDatabase);

    private CommandDefinition Cmd(string sql, object? args, CancellationToken ct, IDbTransaction? tx = null) =>
        new(sql, args, tx, commandTimeout: O.CommandTimeoutSeconds, cancellationToken: ct);

    private static DbString Ansi(string? value, int length) => new() { Value = value, IsAnsi = true, Length = length };

    public async Task<int> CreateAsync(DonateBoxPackage package, CancellationToken ct)
    {
        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);

        // Os textos livres são cortados no tamanho das colunas, que não estão no mapeamento.
        var sizes = (await conn.QueryAsync<(string Name, int Chars)>(Cmd(
            $"""
            SELECT CONVERT(nvarchar(128), c.name) AS Name, CAST(CASE WHEN c.max_length = -1 THEN 4000 WHEN t.name IN ('nvarchar', 'nchar') THEN c.max_length / 2 ELSE c.max_length END AS int) AS Chars
            FROM {Db}.sys.columns c JOIN {Db}.sys.types t ON t.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID(@table) AND c.name IN ('CreatedBy', 'Reason')
            """, new { table = $"{O.DonateDatabase}.dbo.NexusDonatePackages" }, ct)).ConfigureAwait(false))
            .ToDictionary(r => r.Name, r => r.Chars, StringComparer.OrdinalIgnoreCase);
        string Fit(string column, string value) => sizes.TryGetValue(column, out var n) && n > 0 && value.Length > n ? value[..n] : value;

        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        var id = await conn.ExecuteScalarAsync<int>(Cmd(
            $"""
            INSERT {Db}.dbo.NexusDonatePackages (AccountID, CharacterName, Spec, RequestKey, CreatedBy, Reason, Status, DistributorNotifiedAt)
            VALUES (@login, @character, @spec, @key, @createdBy, @reason, 'PENDENTE', NULL);
            SELECT CONVERT(int, SCOPE_IDENTITY());
            """,
            new
            {
                login = Ansi(package.Login, 31),
                character = Ansi(package.Character, 31),
                spec = package.Spec,
                key = Ansi(package.RequestKey, 64),
                createdBy = Fit("CreatedBy", package.CreatedBy),
                reason = Fit("Reason", package.Reason),
            }, ct, tx)).ConfigureAwait(false);

        var rows = new List<object> { new { id, ordinal = 0, code = Ansi(DonateBoxService.BoxCode, 7), aging = 0, mix = 0 } };
        rows.AddRange(package.Lines.Select((l, i) => (object)new { id, ordinal = i + 1, code = Ansi(l.Code, 7), aging = l.Aging, mix = l.Mix ? 1 : 0 }));
        await conn.ExecuteAsync(Cmd(
            $"INSERT {Db}.dbo.NexusDonateItems (PackageID, Ordinal, ItemCode, Aging, Mix) VALUES (@id, @ordinal, @code, @aging, @mix)",
            rows, ct, tx)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return id;
    }

    public async Task<IReadOnlyList<DonateBoxSent>> ListRecentAsync(int take, CancellationToken ct)
    {
        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<SentRaw>(Cmd(
            $"""
            SELECT TOP (@take) CAST(p.ID AS int) AS Id, p.AccountID AS Login, p.CharacterName AS Character, CAST(p.Spec AS int) AS Spec, CONVERT(varchar(20), p.Status) AS Status, CONVERT(nvarchar(100), p.CreatedBy) AS CreatedBy,
                   CONVERT(nvarchar(300), p.Reason) AS Reason, CAST(p.CreatedAt AS datetime2) AS CreatedAt, CAST(p.CompletedAt AS datetime2) AS CompletedAt,
                   CAST(CASE WHEN p.DistributorNotifiedAt IS NULL THEN 0 ELSE 1 END AS bit) AS Notified,
                   (SELECT COUNT(*) FROM {Db}.dbo.NexusDonateItems i WHERE i.PackageID = p.ID AND i.Ordinal > 0) AS Items,
                   (SELECT TOP (1) i.ItemCode FROM {Db}.dbo.NexusDonateItems i WHERE i.PackageID = p.ID AND i.Ordinal > 0 ORDER BY i.Ordinal) AS FirstItem
            FROM {Db}.dbo.NexusDonatePackages p
            WHERE p.RequestKey LIKE 'PORTAL-%'
            ORDER BY p.ID DESC
            """, new { take }, ct)).ConfigureAwait(false);
        return rows.Select(r => new DonateBoxSent(r.Id, r.Login.Trim(), r.Character?.Trim(), r.Spec, r.Status.Trim(), r.CreatedBy?.Trim(), r.Reason?.Trim(),
            r.CreatedAt, r.CompletedAt, r.Notified, r.Items, r.FirstItem?.Trim())).ToList();
    }

    public async Task<DonateRuntime?> RuntimeAsync(CancellationToken ct)
    {
        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        var row = await conn.QueryFirstOrDefaultAsync<RuntimeRaw>(Cmd(
            $"""
            IF OBJECT_ID(@table) IS NOT NULL
                SELECT TOP (1) CAST(Ready AS bit) AS Ready, CAST(LastSeen AS datetime2) AS LastSeen, CAST(ProtocolVersion AS int) AS ProtocolVersion FROM {Db}.dbo.NexusDonateRuntime ORDER BY LastSeen DESC;
            ELSE
                SELECT CAST(0 AS bit) AS Ready, CAST(NULL AS datetime2) AS LastSeen, 0 AS ProtocolVersion WHERE 1 = 0;
            """, new { table = $"{O.DonateDatabase}.dbo.NexusDonateRuntime" }, ct)).ConfigureAwait(false);
        return row is null ? null : new DonateRuntime(row.Ready, row.LastSeen, row.ProtocolVersion);
    }

    private sealed record SentRaw(int Id, string Login, string? Character, int Spec, string Status, string? CreatedBy, string? Reason,
        DateTime? CreatedAt, DateTime? CompletedAt, bool Notified, int Items, string? FirstItem);

    private sealed record RuntimeRaw(bool Ready, DateTime? LastSeen, int ProtocolVersion);
}
