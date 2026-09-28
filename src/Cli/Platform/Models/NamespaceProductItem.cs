namespace SymplrCli.Platform;

public record NamespaceProductItem(
    Guid ProductId,
    string? ProductName,
    string? ProductDescription,
    string? EnvironmentName,
    Guid ProductEnvironmentId,
    Guid TenantProductEnvironmentId);
