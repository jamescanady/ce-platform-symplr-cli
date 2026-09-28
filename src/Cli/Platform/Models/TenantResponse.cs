namespace SymplrCli.Platform;

public record TenantResponse(
    Guid? Id,
    string Name,
    string Description,
    string GlobalTenantCode,
    string TenantShortCode,
    bool IsDisabled,
    TenantProductResponse[]? Products);
