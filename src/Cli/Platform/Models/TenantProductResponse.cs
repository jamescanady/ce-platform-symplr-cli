namespace SymplrCli.Platform;

public record TenantProductResponse(Guid? Id, string Name, string Description, bool IsDisabled);
