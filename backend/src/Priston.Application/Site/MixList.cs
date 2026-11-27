using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using Priston.Application.Accounts;
using Priston.Application.Admin;

namespace Priston.Application.Site;

/// <summary>
/// Uma fórmula da Mix List: em qual equipamento vai, quais sheltoms leva e o que dá.
/// <paramref name="Group"/>: tipo de mix ("Mix comum", "Mix especial"...), como a equipe chama no jogo.
/// <paramref name="Target"/>: uma das chaves de <see cref="SiteContentService.MixTargets"/> (filtro da página).
/// <paramref name="Name"/>: nome do mix ("Devine", "Celesto"...). <paramref name="Sheltoms"/> e <paramref name="Effect"/>: texto livre.
/// </summary>
public sealed record MixRecipe(string Group, string Target, string Name, string Sheltoms, string Effect);

/// <summary>Mix List pública: texto de abertura e as fórmulas, na ordem em que a equipe cadastrou.</summary>
public sealed record MixSettings(string Intro, IReadOnlyList<MixRecipe> Items);

public sealed record MixRecipeInput(string? Group, string? Target, string? Name, string? Sheltoms, string? Effect);

public sealed record MixSettingsInput(string? Intro, IReadOnlyList<MixRecipeInput>? Items);

public sealed partial class SiteContentService
{
    public const string MixKey = "mix";
    public const int MaxMixRecipes = 200;

    /// <summary>Equipamentos que recebem mix (chave do filtro da página).</summary>
    public static readonly IReadOnlySet<string> MixTargets = new HashSet<string>(StringComparer.Ordinal)
    {
        "armas", "armaduras", "robes", "escudos", "orbs", "braceletes", "luvas", "botas", "amuletos", "aneis", "brincos", "cintos", "outros",
    };

    private static readonly MixSettings EmptyMix = new(string.Empty, []);

    /// <summary>Mix List para a página pública. Banco do portal fora do ar: lista vazia (a página diz que está indisponível).</summary>
    public async Task<SettingView<MixSettings>?> GetPublicMixAsync(CancellationToken ct)
    {
        try
        {
            return await cache.GetOrCreateAsync("site:mix", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheFor;
                entry.AddExpirationToken(new CancellationChangeToken(_generation.Token));
                return await GetMixSettingsAsync(ct).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogReadFailed(logger, ex);
            return null;
        }
    }

    public async Task<SettingView<MixSettings>> GetMixSettingsAsync(CancellationToken ct)
    {
        var row = await store.GetSettingAsync(MixKey, ct).ConfigureAwait(false);
        return new(ParseMix(row?.Value) ?? EmptyMix, row?.UpdatedUtc, row?.UpdatedBy);
    }

    public async Task<SaveResult> SaveMixSettingsAsync(StaffPrincipal me, MixSettingsInput input, ClientInfo client, CancellationToken ct)
    {
        var (value, errors) = ValidateMix(input);
        if (value is null)
        {
            return new(StaffAuthStatus.ValidationFailed, null, errors);
        }

        await store.SaveSettingAsync(MixKey, JsonSerializer.Serialize(value, Json), me.Username, ct).ConfigureAwait(false);
        await audit.WriteAsync("staff", me.Username, "site.mix.updated", "site", MixKey, client.IpAddress,
            JsonSerializer.Serialize(new { recipes = value.Items.Count, groups = value.Items.Select(i => i.Group).Distinct().ToList() }), ct).ConfigureAwait(false);
        Invalidate();
        return new(StaffAuthStatus.Ok);
    }

    /// <summary>Tipo de mix, equipamento, nome, sheltoms e efeito. Linha toda vazia é ignorada.</summary>
    public static (MixSettings? Value, Dictionary<string, string> Errors) ValidateMix(MixSettingsInput input)
    {
        var errors = new Dictionary<string, string>();
        var intro = ContentAdminService.Text(input.Intro, 2000, "intro", errors, required: false) ?? string.Empty;
        var items = new List<MixRecipe>();
        foreach (var (r, i) in (input.Items ?? []).Select((r, i) => (r, i + 1)))
        {
            var group = r.Group?.Trim() ?? string.Empty;
            var target = r.Target?.Trim() ?? string.Empty;
            var name = r.Name?.Trim() ?? string.Empty;
            var sheltoms = r.Sheltoms?.Trim() ?? string.Empty;
            var effect = r.Effect?.Trim() ?? string.Empty;
            if (group.Length == 0 && name.Length == 0 && sheltoms.Length == 0 && effect.Length == 0)
            {
                continue;
            }

            if (group.Length is 0 or > 30 || name.Length is 0 or > 40)
            {
                errors["items"] = $"Mix {i}: informe o tipo de mix (até 30 caracteres) e o nome (até 40).";
                continue;
            }

            if (!MixTargets.Contains(target))
            {
                errors["items"] = $"Mix {i}: escolha o equipamento.";
                continue;
            }

            if (sheltoms.Length is 0 or > 200 || effect.Length is 0 or > 300)
            {
                errors["items"] = $"Mix {i}: informe os sheltoms (até 200 caracteres) e o que o mix dá (até 300).";
                continue;
            }

            items.Add(new MixRecipe(group, target, name, sheltoms, effect));
        }

        if (items.Count > MaxMixRecipes)
        {
            errors["items"] = $"Use no máximo {MaxMixRecipes} fórmulas.";
        }

        return errors.Count > 0 ? (null, errors) : (new MixSettings(intro, items), errors);
    }

    private static MixSettings? ParseMix(string? json) => Parse<MixSettings>(json) is { } m
        ? m with
        {
            Intro = m.Intro ?? string.Empty,
            Items = (m.Items ?? [])
                .Where(i => i is not null && !string.IsNullOrEmpty(i.Name) && !string.IsNullOrEmpty(i.Group) && MixTargets.Contains(i.Target ?? string.Empty))
                .ToList(),
        }
        : null;
}
