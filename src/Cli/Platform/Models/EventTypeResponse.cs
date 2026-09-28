namespace SymplrCli.Platform;

public record EventTypeResponse(
    Guid Id,
    Guid ProductId,
    string? Name,
    string? Description,
    bool IsDisabled,
    string? CreatedBy,
    DateTimeOffset Created,
    string? LastModifiedBy,
    DateTimeOffset LastModified);
