namespace SymplrCli.Platform;

public record ProductResponse(Guid? Id, string Name, string Description, bool IsDisabled);
