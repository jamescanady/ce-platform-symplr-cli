namespace SymplrCli.Platform;

public record TenantByProductResponse(
    Guid Id,
    string? Name,
    string? Description,
    string? TenantGlobalId,
    string? TenantShortCode,
    string? TenantProductCode,
    Guid? NamespaceId,
    string? NameSpace,
    Guid ProductId,
    string? ProductName,
    string? EnvironmentName);
