namespace SymplrCli.Platform;

public record TenantNamespaceResponse(
    Guid TenantId,
    string? TenantName,
    string? TenantDescription,
    string? TenantShortCode,
    TenantNamespaceItem[]? Namespaces);
