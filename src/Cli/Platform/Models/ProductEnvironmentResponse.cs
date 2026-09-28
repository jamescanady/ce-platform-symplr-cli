namespace SymplrCli.Platform;

public record ProductEnvironmentResponse(Guid? Id, Guid ProductId, string? Name, bool IsDisabled);
