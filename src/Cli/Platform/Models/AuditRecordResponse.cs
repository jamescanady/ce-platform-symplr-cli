namespace SymplrCli.Platform;

public record AuditRecordResponse(
    string?        TenantId,
    string?        ProductName,
    string?        Environment,
    string?        EventName,
    string?        ConsumerId,
    string?        CorrelationId,
    long           Counter,
    string?        Source,
    string?        Status,
    string?        ErrorCode,
    string?        ErrorMessage,
    DateTimeOffset Created);
