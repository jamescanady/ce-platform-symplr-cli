namespace SymplrCli.Platform;

public record EventConsumerResponse(
    Guid? Id,
    Guid TenantId,
    string Name,
    string? Description,
    string Endpoint,
    string? AuthorizationType,
    object? AuthorizationParameters,
    bool IsDisabled,
    DateTimeOffset? LastSyncDate,
    string? LastSyncMessage,
    string? CreatedBy,
    DateTimeOffset Created,
    string? LastModifiedBy,
    DateTimeOffset LastModified,
    int Version);
