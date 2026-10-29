namespace ACI.Application.DTOs;

public record WebhookInfoDto(
    string WebhookUrl,
    string? ApiKey,
    DateTime? ApiKeyCreatedAt,
    bool HasApiKey,
    bool UsesDefaultWebhookPassword,
    string PasswordWebhookUrl,
    // Where Cadence posts lead and deal events. The secret itself is never sent back.
    string? OutboundWebhookUrl = null,
    bool HasOutboundWebhookSecret = false,
    // Whether an MCP key exists. The key itself is shown once, when it is generated.
    bool HasMcpApiKey = false,
    DateTime? McpApiKeyCreatedAt = null
);
