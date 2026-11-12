using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Priston.Application.Admin;
using Priston.Infrastructure.Accounts;
using Priston.Infrastructure.Persistence;

namespace Priston.Infrastructure.Admin;

internal abstract class PortalStore(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options)
{
    protected Task<SqlConnection> OpenAsync(CancellationToken ct) => db.OpenAsync(ct);

    protected CommandDefinition Cmd(string sql, object? args, CancellationToken ct) =>
        new(sql, args, commandTimeout: options.Value.CommandTimeoutSeconds, cancellationToken: ct);

    protected CommandDefinition Cmd(string sql, object? args, SqlTransaction tx, CancellationToken ct) =>
        new(sql, args, tx, commandTimeout: options.Value.CommandTimeoutSeconds, cancellationToken: ct);

    protected static DbString Ansi(string? value, int length) => new() { Value = value, IsAnsi = true, Length = length };

    protected static bool IsUniqueViolation(SqlException ex) => ex.Number is 2601 or 2627;
}

internal sealed class StaffRepository(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : PortalStore(db, options), IStaffRepository
{
    private const string Columns = "Id, Username, DisplayName, PasswordHash, Role, Active, MustChangePassword, FailedLogins, LockedUntilUtc, CreatedUtc, LastLoginUtc";

    public async Task<StaffUser?> FindByUsernameAsync(string username, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var r = await conn.QueryFirstOrDefaultAsync<Row>(Cmd($"SELECT {Columns} FROM dbo.StaffUser WHERE Username = @username", new { username = Ansi(username, 32) }, ct)).ConfigureAwait(false);
        return r?.ToModel();
    }

    public async Task<StaffUser?> FindByIdAsync(int id, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var r = await conn.QueryFirstOrDefaultAsync<Row>(Cmd($"SELECT {Columns} FROM dbo.StaffUser WHERE Id = @id", new { id }, ct)).ConfigureAwait(false);
        return r?.ToModel();
    }

    public async Task<IReadOnlyList<StaffUser>> ListAsync(CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<Row>(Cmd($"SELECT {Columns} FROM dbo.StaffUser ORDER BY Active DESC, Username", null, ct)).ConfigureAwait(false);
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<int?> CreateAsync(string username, string displayName, string passwordHash, string role, string? createdBy, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        try
        {
            return await conn.ExecuteScalarAsync<int>(Cmd(
                """
                INSERT INTO dbo.StaffUser (Username, DisplayName, PasswordHash, Role, CreatedBy, MustChangePassword)
                OUTPUT INSERTED.Id
                VALUES (@username, @displayName, @passwordHash, @role, @createdBy, 1)
                """,
                new { username = Ansi(username, 32), displayName, passwordHash = Ansi(passwordHash, 200), role = Ansi(role, 16), createdBy = Ansi(createdBy, 32) }, ct)).ConfigureAwait(false);
        }
        catch (SqlException ex) when (IsUniqueViolation(ex))
        {
            return null;
        }
    }

    public async Task UpdateAsync(int id, string displayName, string role, bool active, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd("UPDATE dbo.StaffUser SET DisplayName = @displayName, Role = @role, Active = @active WHERE Id = @id",
            new { id, displayName, role = Ansi(role, 16), active }, ct)).ConfigureAwait(false);
    }

    public async Task SetPasswordAsync(int id, string passwordHash, bool mustChange, DateTimeOffset at, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd(
            """
            UPDATE dbo.StaffUser
               SET PasswordHash = @passwordHash, MustChangePassword = @mustChange, PasswordChangedUtc = @at, FailedLogins = 0, LockedUntilUtc = NULL
             WHERE Id = @id
            """,
            new { id, passwordHash = Ansi(passwordHash, 200), mustChange, at = Utc.From(at) }, ct)).ConfigureAwait(false);
    }

    public async Task RecordLoginAsync(int id, DateTimeOffset at, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd("UPDATE dbo.StaffUser SET LastLoginUtc = @at, FailedLogins = 0, LockedUntilUtc = NULL WHERE Id = @id",
            new { id, at = Utc.From(at) }, ct)).ConfigureAwait(false);
    }

    public async Task RecordFailureAsync(int id, DateTimeOffset? lockUntil, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd(
            """
            UPDATE dbo.StaffUser
               SET FailedLogins = CASE WHEN @lockUntil IS NULL THEN FailedLogins + 1 ELSE 0 END,
                   LockedUntilUtc = COALESCE(@lockUntil, LockedUntilUtc)
             WHERE Id = @id
            """,
            new { id, lockUntil = lockUntil?.UtcDateTime }, ct)).ConfigureAwait(false);
    }

    public async Task<int> CountActiveSuperAdminsAsync(CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<int>(Cmd("SELECT COUNT(*) FROM dbo.StaffUser WHERE Role = 'superadmin' AND Active = 1", null, ct)).ConfigureAwait(false);
    }

    private sealed class Row
    {
        public int Id { get; init; }
        public string Username { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string PasswordHash { get; init; } = "";
        public string Role { get; init; } = "";
        public bool Active { get; init; }
        public bool MustChangePassword { get; init; }
        public int FailedLogins { get; init; }
        public DateTime? LockedUntilUtc { get; init; }
        public DateTime CreatedUtc { get; init; }
        public DateTime? LastLoginUtc { get; init; }

        public StaffUser ToModel() => new(Id, Username, DisplayName, PasswordHash, Role, Active, MustChangePassword, FailedLogins,
            Utc.To(LockedUntilUtc), Utc.To(CreatedUtc), Utc.To(LastLoginUtc));
    }
}

internal sealed class StaffSessionStore(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : PortalStore(db, options), IStaffSessionStore
{
    public async Task CreateAsync(byte[] tokenHash, int staffId, DateTimeOffset expiresUtc, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd(
            "INSERT INTO dbo.StaffSession (TokenHash, StaffId, ExpiresUtc, IpAddress, UserAgent) VALUES (@tokenHash, @staffId, @expires, @ipAddress, @userAgent)",
            new { tokenHash, staffId, expires = Utc.From(expiresUtc), ipAddress, userAgent }, ct)).ConfigureAwait(false);
    }

    public async Task<(int StaffId, DateTimeOffset ExpiresUtc)?> FindActiveAsync(byte[] tokenHash, DateTimeOffset now, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var row = await conn.QueryFirstOrDefaultAsync<(int StaffId, DateTime ExpiresUtc)?>(Cmd(
            "SELECT StaffId, ExpiresUtc FROM dbo.StaffSession WHERE TokenHash = @tokenHash AND RevokedUtc IS NULL AND ExpiresUtc > @now",
            new { tokenHash, now = Utc.From(now) }, ct)).ConfigureAwait(false);
        return row is { } r ? (r.StaffId, Utc.To(r.ExpiresUtc)) : null;
    }

    public async Task RevokeAsync(byte[] tokenHash, DateTimeOffset now, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd("UPDATE dbo.StaffSession SET RevokedUtc = @now WHERE TokenHash = @tokenHash AND RevokedUtc IS NULL",
            new { tokenHash, now = Utc.From(now) }, ct)).ConfigureAwait(false);
    }

    public async Task RevokeAllAsync(int staffId, byte[]? exceptTokenHash, DateTimeOffset now, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd(
            "UPDATE dbo.StaffSession SET RevokedUtc = @now WHERE StaffId = @staffId AND RevokedUtc IS NULL AND (@except IS NULL OR TokenHash <> @except)",
            new { staffId, except = exceptTokenHash, now = Utc.From(now) }, ct)).ConfigureAwait(false);
    }
}

internal sealed class ContentAdminStore(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : PortalStore(db, options), IContentAdminStore
{
    private const string NewsColumns = "Id, Slug, Category, Title, Summary, Body, ImageUrl, Pinned, Status, PublishedUtc, CreatedUtc, UpdatedUtc, CreatedBy, UpdatedBy";
    private const string EventColumns = "Id, Slug, Category, Name, Summary, Description, Rules, Prizes, Location, ImageUrl, StartsUtc, EndsUtc, Status, CreatedUtc, UpdatedUtc, CreatedBy, UpdatedBy";

    public async Task<(IReadOnlyList<NewsRecord> Items, int Total)> ListNewsAsync(string? status, int skip, int take, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        using var grid = await conn.QueryMultipleAsync(Cmd(
            $"""
            SELECT COUNT(*) FROM dbo.News WHERE (@status IS NULL OR Status = @status);
            SELECT {NewsColumns} FROM dbo.News WHERE (@status IS NULL OR Status = @status)
            ORDER BY COALESCE(PublishedUtc, UpdatedUtc) DESC, Id DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;
            """,
            new { status = Ansi(status, 16), skip, take }, ct)).ConfigureAwait(false);
        var total = await grid.ReadSingleAsync<int>().ConfigureAwait(false);
        var rows = await grid.ReadAsync<NewsRow>().ConfigureAwait(false);
        return (rows.Select(r => r.ToModel()).ToList(), total);
    }

    public async Task<NewsRecord?> GetNewsAsync(int id, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var r = await conn.QueryFirstOrDefaultAsync<NewsRow>(Cmd($"SELECT {NewsColumns} FROM dbo.News WHERE Id = @id", new { id }, ct)).ConfigureAwait(false);
        return r?.ToModel();
    }

    public async Task<int?> CreateNewsAsync(NewsData d, string by, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        try
        {
            return await conn.ExecuteScalarAsync<int>(Cmd(
                """
                INSERT INTO dbo.News (Slug, Category, Title, Summary, Body, ImageUrl, Pinned, Status, PublishedUtc, CreatedBy, UpdatedBy)
                OUTPUT INSERTED.Id
                VALUES (@Slug, @Category, @Title, @Summary, @Body, @ImageUrl, @Pinned, @Status, @PublishedUtc, @by, @by)
                """,
                NewsArgs(d, by), ct)).ConfigureAwait(false);
        }
        catch (SqlException ex) when (IsUniqueViolation(ex))
        {
            return null;
        }
    }

    public async Task<bool> UpdateNewsAsync(int id, NewsData d, string by, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        try
        {
            var args = new DynamicParameters(NewsArgs(d, by));
            args.Add("id", id);
            await conn.ExecuteAsync(Cmd(
                """
                UPDATE dbo.News
                   SET Slug = @Slug, Category = @Category, Title = @Title, Summary = @Summary, Body = @Body, ImageUrl = @ImageUrl,
                       Pinned = @Pinned, Status = @Status, PublishedUtc = @PublishedUtc, UpdatedBy = @by, UpdatedUtc = SYSUTCDATETIME()
                 WHERE Id = @id
                """,
                args, ct)).ConfigureAwait(false);
            return true;
        }
        catch (SqlException ex) when (IsUniqueViolation(ex))
        {
            return false;
        }
    }

    public async Task<(IReadOnlyList<EventRecord> Items, int Total)> ListEventsAsync(string? status, int skip, int take, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        using var grid = await conn.QueryMultipleAsync(Cmd(
            $"""
            SELECT COUNT(*) FROM dbo.GameEvent WHERE (@status IS NULL OR Status = @status);
            SELECT {EventColumns} FROM dbo.GameEvent WHERE (@status IS NULL OR Status = @status)
            ORDER BY StartsUtc DESC, Id DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;
            """,
            new { status = Ansi(status, 16), skip, take }, ct)).ConfigureAwait(false);
        var total = await grid.ReadSingleAsync<int>().ConfigureAwait(false);
        var rows = await grid.ReadAsync<EventRow>().ConfigureAwait(false);
        return (rows.Select(r => r.ToModel()).ToList(), total);
    }

    public async Task<EventRecord?> GetEventAsync(int id, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var r = await conn.QueryFirstOrDefaultAsync<EventRow>(Cmd($"SELECT {EventColumns} FROM dbo.GameEvent WHERE Id = @id", new { id }, ct)).ConfigureAwait(false);
        return r?.ToModel();
    }

    public async Task<int?> CreateEventAsync(EventData d, string by, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        try
        {
            return await conn.ExecuteScalarAsync<int>(Cmd(
                """
                INSERT INTO dbo.GameEvent (Slug, Category, Name, Summary, Description, Rules, Prizes, Location, ImageUrl, StartsUtc, EndsUtc, Status, CreatedBy, UpdatedBy)
                OUTPUT INSERTED.Id
                VALUES (@Slug, @Category, @Name, @Summary, @Description, @Rules, @Prizes, @Location, @ImageUrl, @StartsUtc, @EndsUtc, @Status, @by, @by)
                """,
                EventArgs(d, by), ct)).ConfigureAwait(false);
        }
        catch (SqlException ex) when (IsUniqueViolation(ex))
        {
            return null;
        }
    }

    public async Task<bool> UpdateEventAsync(int id, EventData d, string by, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        try
        {
            var args = new DynamicParameters(EventArgs(d, by));
            args.Add("id", id);
            await conn.ExecuteAsync(Cmd(
                """
                UPDATE dbo.GameEvent
                   SET Slug = @Slug, Category = @Category, Name = @Name, Summary = @Summary, Description = @Description, Rules = @Rules,
                       Prizes = @Prizes, Location = @Location, ImageUrl = @ImageUrl, StartsUtc = @StartsUtc, EndsUtc = @EndsUtc, Status = @Status,
                       UpdatedBy = @by, UpdatedUtc = SYSUTCDATETIME()
                 WHERE Id = @id;
                -- Horário mudou: o lembrete volta a valer para o novo horário.
                UPDATE dbo.GameEventReminder SET SentUtc = NULL WHERE EventId = @id AND SentUtc IS NOT NULL
                   AND EXISTS (SELECT 1 FROM dbo.GameEvent WHERE Id = @id AND StartsUtc > SYSUTCDATETIME());
                """,
                args, ct)).ConfigureAwait(false);
            return true;
        }
        catch (SqlException ex) when (IsUniqueViolation(ex))
        {
            return false;
        }
    }

    public async Task<bool> DeleteNewsAsync(int id, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteAsync(Cmd("DELETE FROM dbo.News WHERE Id = @id", new { id }, ct)).ConfigureAwait(false) > 0;
    }

    public async Task<bool> DeleteEventAsync(int id, CancellationToken ct)
    {
        // Os lembretes saem junto (ON DELETE CASCADE).
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteAsync(Cmd("DELETE FROM dbo.GameEvent WHERE Id = @id", new { id }, ct)).ConfigureAwait(false) > 0;
    }

    public async Task EndEventAsync(int id, DateTimeOffset endsUtc, string by, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd(
            "UPDATE dbo.GameEvent SET EndsUtc = @ends, UpdatedBy = @by, UpdatedUtc = SYSUTCDATETIME() WHERE Id = @id AND StartsUtc < @ends",
            new { id, ends = Utc.From(endsUtc), by = Ansi(by, 32) }, ct)).ConfigureAwait(false);
    }

    private static object NewsArgs(NewsData d, string by) => new
    {
        Slug = Ansi(d.Slug, 120),
        Category = Ansi(d.Category, 16),
        d.Title,
        d.Summary,
        d.Body,
        d.ImageUrl,
        d.Pinned,
        Status = Ansi(d.Status, 16),
        PublishedUtc = d.PublishedUtc?.UtcDateTime,
        by = Ansi(by, 32),
    };

    private static object EventArgs(EventData d, string by) => new
    {
        Slug = Ansi(d.Slug, 120),
        Category = Ansi(d.Category, 16),
        d.Name,
        d.Summary,
        d.Description,
        d.Rules,
        d.Prizes,
        d.Location,
        d.ImageUrl,
        StartsUtc = d.StartsUtc.UtcDateTime,
        EndsUtc = d.EndsUtc?.UtcDateTime,
        Status = Ansi(d.Status, 16),
        by = Ansi(by, 32),
    };

    private sealed class NewsRow
    {
        public int Id { get; init; }
        public string Slug { get; init; } = "";
        public string Category { get; init; } = "";
        public string Title { get; init; } = "";
        public string Summary { get; init; } = "";
        public string Body { get; init; } = "";
        public string? ImageUrl { get; init; }
        public bool Pinned { get; init; }
        public string Status { get; init; } = "";
        public DateTime? PublishedUtc { get; init; }
        public DateTime CreatedUtc { get; init; }
        public DateTime UpdatedUtc { get; init; }
        public string? CreatedBy { get; init; }
        public string? UpdatedBy { get; init; }

        public NewsRecord ToModel() => new(Id, Slug, Category, Title, Summary, Body, ImageUrl, Pinned, Status, Utc.To(PublishedUtc), Utc.To(CreatedUtc), Utc.To(UpdatedUtc), CreatedBy, UpdatedBy);
    }

    private sealed class EventRow
    {
        public int Id { get; init; }
        public string Slug { get; init; } = "";
        public string Category { get; init; } = "";
        public string Name { get; init; } = "";
        public string Summary { get; init; } = "";
        public string Description { get; init; } = "";
        public string? Rules { get; init; }
        public string? Prizes { get; init; }
        public string? Location { get; init; }
        public string? ImageUrl { get; init; }
        public DateTime StartsUtc { get; init; }
        public DateTime? EndsUtc { get; init; }
        public string Status { get; init; } = "";
        public DateTime CreatedUtc { get; init; }
        public DateTime UpdatedUtc { get; init; }
        public string? CreatedBy { get; init; }
        public string? UpdatedBy { get; init; }

        public EventRecord ToModel() => new(Id, Slug, Category, Name, Summary, Description, Rules, Prizes, Location, ImageUrl, Utc.To(StartsUtc), Utc.To(EndsUtc),
            Status, Utc.To(CreatedUtc), Utc.To(UpdatedUtc), CreatedBy, UpdatedBy);
    }
}

internal sealed class PortalStatsReader(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : PortalStore(db, options), IPortalStatsReader
{
    public async Task<PortalTotals> ReadAsync(DateTimeOffset now, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        return await conn.QuerySingleAsync<PortalTotals>(Cmd(
            """
            SELECT
              (SELECT COUNT(*) FROM dbo.AuditLog WHERE Action = 'account.register' AND OccurredUtc > DATEADD(day, -1, @now))  AS Registrations24h,
              (SELECT COUNT(*) FROM dbo.AuditLog WHERE Action = 'account.register' AND OccurredUtc > DATEADD(day, -7, @now))  AS Registrations7d,
              (SELECT COUNT(*) FROM dbo.AuditLog WHERE Action = 'account.register' AND OccurredUtc > DATEADD(day, -30, @now)) AS Registrations30d,
              (SELECT COUNT(*) FROM dbo.PlayerAccount WHERE EmailConfirmedUtc IS NOT NULL) AS AccountsWithConfirmedEmail,
              (SELECT COUNT(*) FROM dbo.News WHERE Status = 'published' AND PublishedUtc <= @now) AS NewsPublished,
              (SELECT COUNT(*) FROM dbo.News WHERE Status = 'draft') AS NewsDrafts,
              (SELECT COUNT(*) FROM dbo.GameEvent WHERE Status = 'published' AND StartsUtc <= @now
                  AND COALESCE(EndsUtc, DATEADD(hour, 1, StartsUtc)) > @now) AS EventsLive,
              (SELECT COUNT(*) FROM dbo.GameEvent WHERE Status = 'published' AND StartsUtc > @now) AS EventsUpcoming,
              (SELECT COUNT(*) FROM dbo.SupportTicket WHERE Status IN ('aberto','em_atendimento','aguardando')) AS TicketsActive,
              (SELECT COUNT(*) FROM dbo.SupportTicket WHERE Status IN ('aberto','em_atendimento') AND LastAuthor = 'jogador') AS TicketsWaitingStaff,
              (SELECT COUNT(*) FROM dbo.SupportTicket WHERE CreatedUtc > DATEADD(day, -1, @now)) AS Tickets24h
            """,
            new { now = now.UtcDateTime }, ct)).ConfigureAwait(false);
    }
}

internal sealed class AuditReader(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : PortalStore(db, options), IAuditReader
{
    public async Task<(IReadOnlyList<AuditEntry> Items, int Total)> ListAsync(AuditFilter f, int skip, int take, CancellationToken ct)
    {
        const string where = """
            WHERE (@actorType IS NULL OR ActorType = @actorType)
              AND (@actor IS NULL OR ActorId = @actor)
              AND (@prefix IS NULL OR Action LIKE @prefix + '%' ESCAPE '\')
            """;
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        using var grid = await conn.QueryMultipleAsync(Cmd(
            $"""
            SELECT COUNT(*) FROM dbo.AuditLog {where};
            SELECT Id, OccurredUtc, ActorType, ActorId, Action, TargetType, TargetId, IpAddress, AfterJson AS Details
            FROM dbo.AuditLog {where}
            ORDER BY OccurredUtc DESC, Id DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;
            """,
            new
            {
                actorType = Ansi(f.ActorType, 16),
                actor = f.Actor,
                prefix = Ansi(f.ActionPrefix?.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal), 100),
                skip,
                take,
            }, ct)).ConfigureAwait(false);
        var total = await grid.ReadSingleAsync<int>().ConfigureAwait(false);
        var rows = await grid.ReadAsync<Row>().ConfigureAwait(false);
        return (rows.Select(r => new AuditEntry(r.Id, Utc.To(r.OccurredUtc), r.ActorType, r.ActorId, r.Action, r.TargetType, r.TargetId, r.IpAddress, r.Details)).ToList(), total);
    }

    private sealed class Row
    {
        public long Id { get; init; }
        public DateTime OccurredUtc { get; init; }
        public string ActorType { get; init; } = "";
        public string? ActorId { get; init; }
        public string Action { get; init; } = "";
        public string? TargetType { get; init; }
        public string? TargetId { get; init; }
        public string? IpAddress { get; init; }
        public string? Details { get; init; }
    }
}
