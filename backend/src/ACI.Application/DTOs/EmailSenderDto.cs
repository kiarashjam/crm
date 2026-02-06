namespace ACI.Application.DTOs;

public class SendEmailRequest
{
    public required string To { get; set; }
    public string? ToName { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public bool IsHtml { get; set; } = false;
    public string? ReplyTo { get; set; }
    public List<string>? Cc { get; set; }
    public List<string>? Bcc { get; set; }
    
    // Optional tracking
    public string? CopyHistoryId { get; set; }
    public string? CopyTypeId { get; set; }
}

public class SendEmailResult
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public string? Error { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public class SmtpSettings
{
    public string? Id { get; set; }
    public required string Host { get; set; }
    public int Port { get; set; } = 587;
    public required string Username { get; set; }
    public required string Password { get; set; }
    public bool UseSsl { get; set; } = true;
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
    public DateTime? LastTestedAt { get; set; }
    public bool IsValid { get; set; } = false;
}

public class TestSmtpRequest
{
    public required string Host { get; set; }
    public int Port { get; set; } = 587;
    public required string Username { get; set; }
    public required string Password { get; set; }
    public bool UseSsl { get; set; } = true;
    public string? FromEmail { get; set; }
}

public class TestSmtpResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int LatencyMs { get; set; }
}
