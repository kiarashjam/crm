using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>Sets the shared password for the organization JSON webhook. Omit or send empty to use the app default.</summary>
public record UpdateWebhookPasswordRequest
{
    /// <summary>New password, or null/empty to clear and use the default.</summary>
    [StringLength(256, MinimumLength = 0)]
    public string? Password { get; init; }
}
