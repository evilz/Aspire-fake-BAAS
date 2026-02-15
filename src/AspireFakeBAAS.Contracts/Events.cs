namespace AspireFakeBAAS.Contracts;

public record AccountCreatedEvent(
    Guid AccountId,
    string AccountNumber,
    string CustomerName,
    string Email,
    DateTime CreatedAt);

public record AccountUpdatedEvent(
    Guid AccountId,
    string CustomerName,
    string Email,
    DateTime UpdatedAt);

public record TransactionCreatedEvent(
    Guid TransactionId,
    Guid AccountId,
    decimal Amount,
    string TransactionType,
    string Description,
    DateTime CreatedAt);
