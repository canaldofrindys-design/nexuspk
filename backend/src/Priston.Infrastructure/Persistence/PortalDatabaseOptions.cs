namespace Priston.Infrastructure.Persistence;

/// <summary>Banco complementar do portal (notícias, eventos, tickets, snapshots, auditoria).</summary>
public sealed class PortalDatabaseOptions
{
    public const string SectionName = "PortalDatabase";

    /// <summary>Login de aplicação: leitura e escrita de dados, sem DDL.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public int CommandTimeoutSeconds { get; set; } = 30;
}
