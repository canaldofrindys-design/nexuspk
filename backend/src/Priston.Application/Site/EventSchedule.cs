using System.Globalization;
using System.Text.Json;
using Priston.Application.Accounts;
using Priston.Application.Admin;

namespace Priston.Application.Site;

/// <summary>
/// Evento fixo do servidor que se repete toda semana (Bless Castle, SoD, Hellsgate...), com contagem regressiva no banner da home.
/// <paramref name="Kind"/>: uma das chaves de <see cref="SiteContentService.ScheduleKinds"/> (escolhe arte e ícone).
/// <paramref name="Days"/>: dias da semana em que acontece (0 = domingo ... 6 = sábado).
/// <paramref name="Time"/>: primeiro horário de início em Brasília ("HH:mm"). <paramref name="DurationMinutes"/>: quanto tempo fica aberto.
/// <paramref name="Times"/>: todos os horários do dia, em ordem ("01:00", "13:00"...), para bosses que nascem várias vezes; null = só <paramref name="Time"/>.
/// <paramref name="Place"/>: mapa ou local (opcional), mostrado na página de bosses.
/// </summary>
public sealed record ScheduledEvent(
    string Name,
    string Kind,
    IReadOnlyList<int> Days,
    string Time,
    int DurationMinutes,
    string? Description,
    IReadOnlyList<string>? Times = null,
    string? Place = null);

public sealed record EventScheduleSettings(IReadOnlyList<ScheduledEvent> Items);

/// <summary><paramref name="Time"/>: um horário ("16:00") ou vários separados por vírgula ("01:00, 09:00, 17:00").</summary>
public sealed record ScheduledEventInput(string? Name, string? Kind, IReadOnlyList<int>? Days, string? Time, string? Duration, string? Description, string? Place = null);

public sealed record EventScheduleSettingsInput(IReadOnlyList<ScheduledEventInput>? Items);

public sealed partial class SiteContentService
{
    public const string ScheduleKey = "schedule";
    public const int MaxScheduled = 20;
    public const int MaxTimesPerEvent = 24;
    public const int MaxDurationMinutes = 24 * 60;

    /// <summary>Tipos de evento fixo: definem a arte do banner e o ícone.</summary>
    public static readonly IReadOnlySet<string> ScheduleKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "bless", "sod", "hellsgate", "boss", "pvp", "evento",
    };

    /// <summary>Sem nada salvo no painel: o Bless Castle de sempre (domingo, 16:00).</summary>
    public static readonly EventScheduleSettings DefaultSchedule = new(
    [
        new ScheduledEvent("Bless Castle", "bless", [0], "16:00", 60, "Guerra de clãs pela posse do castelo"),
    ]);

    public async Task<SettingView<EventScheduleSettings>> GetScheduleSettingsAsync(CancellationToken ct)
    {
        var row = await store.GetSettingAsync(ScheduleKey, ct).ConfigureAwait(false);
        return new(ParseSchedule(row?.Value) ?? DefaultSchedule, row?.UpdatedUtc, row?.UpdatedBy);
    }

    public async Task<SaveResult> SaveScheduleSettingsAsync(StaffPrincipal me, EventScheduleSettingsInput input, ClientInfo client, CancellationToken ct)
    {
        var (value, errors) = ValidateSchedule(input);
        if (value is null)
        {
            return new(StaffAuthStatus.ValidationFailed, null, errors);
        }

        await store.SaveSettingAsync(ScheduleKey, JsonSerializer.Serialize(value, Json), me.Username, ct).ConfigureAwait(false);
        await audit.WriteAsync("staff", me.Username, "site.schedule.updated", "site", ScheduleKey, client.IpAddress,
            JsonSerializer.Serialize(new { items = value.Items.Select(i => new { i.Name, i.Days, Times = i.Times ?? [i.Time] }) }), ct).ConfigureAwait(false);
        Invalidate();
        return new(StaffAuthStatus.Ok);
    }

    /// <summary>
    /// Nome, tipo, ao menos um dia, um ou mais horários "HH:mm" (separados por vírgula) e duração em minutos.
    /// Linha sem nome e sem horário é ignorada.
    /// </summary>
    public static (EventScheduleSettings? Value, Dictionary<string, string> Errors) ValidateSchedule(EventScheduleSettingsInput input)
    {
        var errors = new Dictionary<string, string>();
        var items = new List<ScheduledEvent>();
        foreach (var (r, i) in (input.Items ?? []).Select((r, i) => (r, i + 1)))
        {
            var name = r.Name?.Trim() ?? string.Empty;
            var timeText = r.Time?.Trim() ?? string.Empty;
            if (name.Length == 0 && timeText.Length == 0)
            {
                continue;
            }

            if (name.Length is 0 or > 40)
            {
                errors["items"] = $"Evento {i}: informe o nome (até 40 caracteres).";
                continue;
            }

            var kind = r.Kind?.Trim() ?? string.Empty;
            if (!ScheduleKinds.Contains(kind))
            {
                errors["items"] = $"Evento {i}: escolha o tipo do evento.";
                continue;
            }

            var days = (r.Days ?? []).Where(d => d is >= 0 and <= 6).Distinct().Order().ToList();
            if (days.Count == 0)
            {
                errors["items"] = $"Evento {i}: marque ao menos um dia da semana.";
                continue;
            }

            var times = ParseTimes(timeText);
            if (times is null)
            {
                errors["items"] = $"Evento {i}: o horário fica no formato 16:00 (vários horários separados por vírgula: 01:00, 13:00).";
                continue;
            }

            if (times.Count > MaxTimesPerEvent)
            {
                errors["items"] = $"Evento {i}: use no máximo {MaxTimesPerEvent} horários por dia.";
                continue;
            }

            var durationText = r.Duration?.Trim() ?? string.Empty;
            var duration = 60;
            if (durationText.Length > 0
                && (!int.TryParse(durationText, NumberStyles.None, CultureInfo.InvariantCulture, out duration) || duration is < 1 or > MaxDurationMinutes))
            {
                errors["items"] = $"Evento {i}: a duração é em minutos, de 1 a {MaxDurationMinutes}.";
                continue;
            }

            var description = Optional(r.Description);
            if (description is { Length: > 120 })
            {
                errors["items"] = $"Evento {i}: a descrição tem no máximo 120 caracteres.";
                continue;
            }

            var place = Optional(r.Place);
            if (place is { Length: > 40 })
            {
                errors["items"] = $"Evento {i}: o local tem no máximo 40 caracteres.";
                continue;
            }

            items.Add(new ScheduledEvent(name, kind, days, times[0], duration, description, times.Count > 1 ? times : null, place));
        }

        if (items.Count > MaxScheduled)
        {
            errors["items"] = $"Use no máximo {MaxScheduled} eventos fixos.";
        }

        return errors.Count > 0 ? (null, errors) : (new EventScheduleSettings(items), errors);
    }

    /// <summary>"9:30, 01:00, 9:30" vira ["01:00", "09:30"]. null quando vazio ou com algum horário inválido.</summary>
    public static IReadOnlyList<string>? ParseTimes(string? text)
    {
        var parts = (text ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var times = new SortedSet<TimeOnly>();
        foreach (var part in parts)
        {
            if (!TimeOnly.TryParseExact(part, ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                return null;
            }

            times.Add(time);
        }

        return times.Select(t => t.ToString("HH:mm", CultureInfo.InvariantCulture)).ToList();
    }

    private static bool ValidTime(string? time) => TimeOnly.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static EventScheduleSettings? ParseSchedule(string? json) => Parse<EventScheduleSettings>(json) is { } s
        ? s with
        {
            Items = (s.Items ?? [])
                .Where(i => i is not null && !string.IsNullOrEmpty(i.Name) && ScheduleKinds.Contains(i.Kind ?? string.Empty)
                    && i.Days is { Count: > 0 } && ValidTime(i.Time) && (i.Times is null || i.Times.All(ValidTime)))
                .ToList(),
        }
        : null;
}
