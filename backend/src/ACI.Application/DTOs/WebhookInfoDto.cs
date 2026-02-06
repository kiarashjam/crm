namespace ACI.Application.DTOs;

public record WebhookInfoDto(
    string WebhookUrl,
    string? ApiKey,
    DateTime? ApiKeyCreatedAt,
    bool HasApiKey
);
