using Dapper;
using Microsoft.Extensions.Options;
using Priston.Application.Site;
using Priston.Application.Support;
using Priston.Infrastructure.Accounts;
using Priston.Infrastructure.Admin;
using Priston.Infrastructure.Persistence;

namespace Priston.Infrastructure.Site;

/// <summary>Conteúdo do site, banners e imagens do painel (banco do portal, migração 0007).</summary>
internal sealed class SiteContentStore(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : PortalStore(db, options), ISiteContentStore
{
    private const string BannerColumns =
        "Id, Title, Subtitle, ImageUrl, LinkUrl, LinkLabel, SortOrder, Active, StartsUtc, EndsUtc, CreatedUtc, UpdatedUtc, CreatedBy, UpdatedBy";

    public async Task<SiteSettingRow?> GetSettingAsync(string key, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var row = await conn.QueryFirstOrDefaultAsync<SettingRow>(Cmd(
            "SELECT Value, UpdatedUtc, UpdatedBy FROM dbo.SiteSetting WHERE [Key] = @key", new { key = Ansi(key, 32) }, ct)).ConfigureAwait(false);
        return row is null ? null : new SiteSettingRow(row.Value, Utc.To(row.UpdatedUtc), row.UpdatedBy);
    }

    public async Task SaveSettingAsync(string key, string json, string by, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd(
            """
            UPDATE dbo.SiteSetting SET Value = @json, UpdatedBy = @by, UpdatedUtc = SYSUTCDATETIME() WHERE [Key] = @key;
            IF @@ROWCOUNT = 0
                INSERT INTO dbo.SiteSetting ([Key], Value, UpdatedBy) VALUES (@key, @json, @by);
            """,
            new { key = Ansi(key, 32), json, by = Ansi(by, 32) }, ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BannerRecord>> ListBannersAsync(CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<BannerRow>(Cmd($"SELECT {BannerColumns} FROM dbo.SiteBanner ORDER BY Active DESC, SortOrder, Id DESC", null, ct)).ConfigureAwait(false);
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<BannerRecord?> GetBannerAsync(int id, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var row = await conn.QueryFirstOrDefaultAsync<BannerRow>(Cmd($"SELECT {BannerColumns} FROM dbo.SiteBanner WHERE Id = @id", new { id }, ct)).ConfigureAwait(false);
        return row?.ToModel();
    }

    public async Task<int> CreateBannerAsync(BannerData d, string by, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<int>(Cmd(
            """
            INSERT INTO dbo.SiteBanner (Title, Subtitle, ImageUrl, LinkUrl, LinkLabel, SortOrder, Active, StartsUtc, EndsUtc, CreatedBy, UpdatedBy)
            OUTPUT INSERTED.Id
            VALUES (@Title, @Subtitle, @ImageUrl, @LinkUrl, @LinkLabel, @SortOrder, @Active, @StartsUtc, @EndsUtc, @by, @by)
            """,
            Args(d, by), ct)).ConfigureAwait(false);
    }

    public async Task UpdateBannerAsync(int id, BannerData d, string by, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var args = new DynamicParameters(Args(d, by));
        args.Add("id", id);
        await conn.ExecuteAsync(Cmd(
            """
            UPDATE dbo.SiteBanner
               SET Title = @Title, Subtitle = @Subtitle, ImageUrl = @ImageUrl, LinkUrl = @LinkUrl, LinkLabel = @LinkLabel, SortOrder = @SortOrder,
                   Active = @Active, StartsUtc = @StartsUtc, EndsUtc = @EndsUtc, UpdatedBy = @by, UpdatedUtc = SYSUTCDATETIME()
             WHERE Id = @id
            """,
            args, ct)).ConfigureAwait(false);
    }

    public async Task<bool> DeleteBannerAsync(int id, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteAsync(Cmd("DELETE FROM dbo.SiteBanner WHERE Id = @id", new { id }, ct)).ConfigureAwait(false) > 0;
    }

    public async Task SaveMediaAsync(Guid id, CleanAttachment file, string by, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd(
            "INSERT INTO dbo.MediaFile (Id, FileName, ContentType, SizeBytes, Data, CreatedBy) VALUES (@id, @fileName, @contentType, @size, @data, @by)",
            new { id, fileName = file.FileName, contentType = Ansi(file.ContentType, 32), size = file.Data.Length, data = file.Data, by = Ansi(by, 32) }, ct)).ConfigureAwait(false);
    }

    public async Task<AttachmentContent?> GetMediaAsync(Guid id, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        return await conn.QueryFirstOrDefaultAsync<AttachmentContent>(Cmd(
            "SELECT FileName, ContentType, Data FROM dbo.MediaFile WHERE Id = @id", new { id }, ct)).ConfigureAwait(false);
    }

    private static object Args(BannerData d, string by) => new
    {
        d.Title,
        d.Subtitle,
        d.ImageUrl,
        d.LinkUrl,
        d.LinkLabel,
        d.SortOrder,
        d.Active,
        StartsUtc = d.StartsUtc?.UtcDateTime,
        EndsUtc = d.EndsUtc?.UtcDateTime,
        by = Ansi(by, 32),
    };

    private sealed class SettingRow
    {
        public string Value { get; init; } = "";
        public DateTime UpdatedUtc { get; init; }
        public string? UpdatedBy { get; init; }
    }

    private sealed class BannerRow
    {
        public int Id { get; init; }
        public string Title { get; init; } = "";
        public string? Subtitle { get; init; }
        public string? ImageUrl { get; init; }
        public string? LinkUrl { get; init; }
        public string? LinkLabel { get; init; }
        public int SortOrder { get; init; }
        public bool Active { get; init; }
        public DateTime? StartsUtc { get; init; }
        public DateTime? EndsUtc { get; init; }
        public DateTime CreatedUtc { get; init; }
        public DateTime UpdatedUtc { get; init; }
        public string? CreatedBy { get; init; }
        public string? UpdatedBy { get; init; }

        public BannerRecord ToModel() => new(Id, Title, Subtitle, ImageUrl, LinkUrl, LinkLabel, SortOrder, Active, Utc.To(StartsUtc), Utc.To(EndsUtc),
            Utc.To(CreatedUtc), Utc.To(UpdatedUtc), CreatedBy, UpdatedBy);
    }
}
