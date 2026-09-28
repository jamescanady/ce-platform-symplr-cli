namespace SymplrCli.Platform;

public record NamespaceResponse(
    Guid Id,
    string? Name,
    string? ShortCode,
    string? Description,
    bool IsDefault,
    bool IsDisabled,
    DateTimeOffset CreatedDate,
    string? CreatedBy,
    DateTimeOffset LastModified,
    string? LastModifiedBy);
