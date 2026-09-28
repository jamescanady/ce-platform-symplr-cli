namespace SymplrCli.Platform;

public record TenantNamespaceItem(
    Guid NamespaceId,
    string? Namespace,
    string? NamespaceDescription,
    NamespaceProductItem[]? Products);
