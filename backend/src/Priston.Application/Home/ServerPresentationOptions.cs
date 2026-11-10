namespace Priston.Application.Home;

/// <summary>Textos e taxas exibidos no portal. Editáveis pelo admin na Fase 10.</summary>
public sealed class ServerPresentationOptions
{
    public const string SectionName = "ServerPresentation";

    public string Name { get; set; } = "NexusPK";

    public string Tagline { get; set; } = string.Empty;

    /// <summary>Taxas exibidas, ex.: { "EXP": "10x" }. Vazio esconde o bloco.</summary>
    public Dictionary<string, string> Rates { get; set; } = [];

    /// <summary>Links de comunidade (Discord, WhatsApp, Instagram…). Vazio esconde o bloco.</summary>
    public List<CommunityLink> CommunityLinks { get; set; } = [];
}

public sealed class CommunityLink
{
    public string Label { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
}
