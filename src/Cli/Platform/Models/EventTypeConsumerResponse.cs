namespace SymplrCli.Platform;

public record EventTypeConsumerResponse(
    Guid Id,
    Guid ConsumerId,
    string? ConsumerName,
    string? ConsumerDescription,
    string? ConsumerEndpoint,
    string? ConsumerAuthorizationType,
    Guid EventTypeId,
    string? EventTypeName,
    Guid TenantProductEnvironmentId,
    Guid TenantId,
    string? TenantName,
    Guid ProductId,
    string? ProductName,
    string? ProductString,
    string? EnvironmentName,
    bool IsDisabled,
    string? CreatedBy,
    DateTimeOffset Created,
    string? LastModifiedBy,
    DateTimeOffset LastModified);
