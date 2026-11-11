using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Priston.Application.Accounts;
using Priston.Application.Home;
using Priston.Domain.Time;

namespace Priston.Application.Content;

/// <summary>Envia o e-mail de lembrete para quem pediu, cerca de uma hora antes do início do evento.</summary>
public sealed partial class EventReminderService(
    IEventReminderStore reminders,
    IEmailSender email,
    TimeProvider clock,
    IOptions<ServerPresentationOptions> presentation,
    IOptions<AccountOptions> accountOptions,
    ILogger<EventReminderService> logger)
{
    public static readonly TimeSpan LeadTime = TimeSpan.FromMinutes(60);
    private const int BatchSize = 200;
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(RankingPeriods.TimeZoneId);
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public async Task<int> SendDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var due = await reminders.ListDueAsync(now, now + LeadTime, BatchSize, ct).ConfigureAwait(false);
        var sent = 0;
        foreach (var r in due)
        {
            try
            {
                await email.SendAsync(Build(r), ct).ConfigureAwait(false);
                await reminders.MarkSentAsync(r.EventId, r.GameLogin, clock.GetUtcNow(), ct).ConfigureAwait(false);
                sent++;
            }
#pragma warning disable CA1031 // Uma falha de envio não impede os demais; o próximo disparo tenta de novo.
            catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
            {
                LogSendFailed(logger, r.EventId, ex);
            }
        }

        if (sent > 0)
        {
            LogSent(logger, sent);
        }

        return sent;
    }

    public EmailMessage Build(DueReminder r)
    {
        var server = presentation.Value.Name;
        var local = TimeZoneInfo.ConvertTime(r.StartsUtc, Zone);
        var url = $"{accountOptions.Value.PublicBaseUrl.TrimEnd('/')}/eventos/{r.Slug}";
        var body = string.Join("\n",
            "Olá,",
            "",
            $"O evento \"{r.EventName}\" começa {local.ToString("dd/MM 'às' HH:mm", PtBr)} (horário de Brasília).",
            "",
            $"Detalhes e regulamento: {url}",
            "",
            "Você recebeu este aviso porque pediu um lembrete na página do evento. Para não receber mais, desative o lembrete na mesma página.",
            "",
            $"Equipe {server}");
        return new EmailMessage(r.Email, $"{server}: {r.EventName} começa às {local.ToString("HH:mm", PtBr)}", body);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Lembretes de evento enviados: {Count}")]
    private static partial void LogSent(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao enviar lembrete do evento {EventId}")]
    private static partial void LogSendFailed(ILogger logger, int eventId, Exception ex);
}
