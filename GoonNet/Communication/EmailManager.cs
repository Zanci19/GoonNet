using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Xml.Serialization;

namespace GoonNet;

public class EmailManager : IDisposable
{
    private System.Threading.Timer? _checkTimer;
    private bool _disposed;

    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
    public int CheckIntervalMinutes { get; set; } = 5;
    public string FromAddress { get; set; } = string.Empty;

    public event EventHandler<EmailEventArgs>? EmailReceived;
    public event EventHandler<EmailEventArgs>? EmailSent;
    public event EventHandler<string>? EmailError;

    public void SendEmail(string to, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(SmtpHost)) { EmailError?.Invoke(this, "SMTP host not configured"); return; }
        try
        {
            using var client = new SmtpClient(SmtpHost, SmtpPort)
            {
                EnableSsl = UseSsl,
                Credentials = new NetworkCredential(Username, Password)
            };
            var message = new MailMessage(FromAddress ?? Username, to, subject, body);
            client.Send(message);
            EmailSent?.Invoke(this, new EmailEventArgs(to, subject, body));
        }
        catch (Exception ex)
        {
            EmailError?.Invoke(this, ex.Message);
        }
    }

    public List<string> CheckForNewEmails()
    {
        // Placeholder - real POP3/IMAP would require an external library
        var placeholder = new List<string> { "Email checking not implemented - requires POP3/IMAP library" };
        EmailReceived?.Invoke(this, new EmailEventArgs("", "Info", placeholder[0]));
        return placeholder;
    }

    public void StartPeriodicCheck()
    {
        if (CheckIntervalMinutes <= 0) return;
        _checkTimer = new System.Threading.Timer(_ => CheckForNewEmails(),
            null, TimeSpan.Zero, TimeSpan.FromMinutes(CheckIntervalMinutes));
    }

    public void StopPeriodicCheck()
    {
        _checkTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _checkTimer?.Dispose();
        _checkTimer = null;
    }

    public void SaveSettings(string filePath)
    {
        var settings = new EmailSettings
        {
            SmtpHost = SmtpHost, SmtpPort = SmtpPort, Username = Username,
            UseSsl = UseSsl, CheckIntervalMinutes = CheckIntervalMinutes, FromAddress = FromAddress
        };
        var serializer = new XmlSerializer(typeof(EmailSettings));
        using var stream = File.Create(filePath);
        serializer.Serialize(stream, settings);
    }

    public void LoadSettings(string filePath)
    {
        if (!File.Exists(filePath)) return;
        try
        {
            var serializer = new XmlSerializer(typeof(EmailSettings));
            using var stream = File.OpenRead(filePath);
            if (serializer.Deserialize(stream) is EmailSettings s)
            {
                SmtpHost = s.SmtpHost; SmtpPort = s.SmtpPort; Username = s.Username;
                UseSsl = s.UseSsl; CheckIntervalMinutes = s.CheckIntervalMinutes; FromAddress = s.FromAddress;
            }
        }
        catch { }
    }

    public void Dispose()
    {
        if (!_disposed) { _disposed = true; StopPeriodicCheck(); }
    }
}

public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
    public int CheckIntervalMinutes { get; set; } = 5;
    public string FromAddress { get; set; } = string.Empty;
}

public class EmailEventArgs : EventArgs
{
    public string To { get; }
    public string Subject { get; }
    public string Body { get; }
    public EmailEventArgs(string to, string subject, string body) { To = to; Subject = subject; Body = body; }
}
