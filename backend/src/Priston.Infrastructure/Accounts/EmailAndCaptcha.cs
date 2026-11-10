using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Priston.Application.Accounts;

namespace Priston.Infrastructure.Accounts;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string UserName { get; set; } = string.Empty;

    /// <summary>Vem de variável de ambiente (Smtp__Password). Nunca do appsettings versionado.</summary>
    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "NexusPK";
}

internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.Host) && !string.IsNullOrWhiteSpace(options.Value.FromAddress);

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var o = options.Value;
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Envio de e-mail não configurado (Smtp:Host e Smtp:FromAddress).");
        }

        using var mail = new MailMessage(new MailAddress(o.FromAddress, o.FromName), new MailAddress(message.To))
        {
            Subject = message.Subject,
            Body = message.TextBody,
            IsBodyHtml = false,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8,
        };
        using var client = new SmtpClient(o.Host, o.Port) { EnableSsl = o.EnableSsl };
        if (!string.IsNullOrEmpty(o.UserName))
        {
            client.Credentials = new NetworkCredential(o.UserName, o.Password);
        }

        await client.SendMailAsync(mail, ct).ConfigureAwait(false);
    }
}

public sealed class CaptchaOptions
{
    public const string SectionName = "Captcha";

    /// <summary>Chave secreta do Cloudflare Turnstile. Vazia desliga o captcha.</summary>
    public string TurnstileSecret { get; set; } = string.Empty;
}

internal sealed class TurnstileCaptchaVerifier(HttpClient http, IOptions<CaptchaOptions> options) : ICaptchaVerifier
{
    private static readonly Uri VerifyUri = new("https://challenges.cloudflare.com/turnstile/v0/siteverify");

    public bool IsEnabled => !string.IsNullOrWhiteSpace(options.Value.TurnstileSecret);

    public async Task<bool> VerifyAsync(string? token, string? ipAddress, CancellationToken ct)
    {
        if (!IsEnabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048)
        {
            return false;
        }

        var form = new Dictionary<string, string> { ["secret"] = options.Value.TurnstileSecret, ["response"] = token };
        if (!string.IsNullOrEmpty(ipAddress))
        {
            form["remoteip"] = ipAddress;
        }

        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await http.PostAsync(VerifyUri, content, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>(ct).ConfigureAwait(false);
            return result?.Success == true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private sealed record TurnstileResponse([property: JsonPropertyName("success")] bool Success);
}
