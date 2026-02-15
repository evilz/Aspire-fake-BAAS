using AspireFakeBAAS.Contracts;
using Marten;
using Marten.Events;
using Marten.Events.Projections;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Configure Marten
var connectionString = builder.Configuration.GetConnectionString("postgres") 
    ?? throw new InvalidOperationException("PostgreSQL connection string is required");

builder.Services.AddMarten(opts =>
{
    opts.Connection(connectionString);
    opts.Events.StreamIdentity = Marten.Events.StreamIdentity.AsGuid;
    
    // Configure event store
    opts.Events.AddEventType<AccountCreatedEvent>();
    opts.Events.AddEventType<AccountUpdatedEvent>();
    opts.Events.AddEventType<TransactionCreatedEvent>();
    
    // Configure inline projections so they're built when events are written
    opts.Projections.Add<AccountProjection>(ProjectionLifecycle.Inline);
})
.UseLightweightSessions()
.AddAsyncDaemon(Marten.Events.Daemon.Resiliency.DaemonMode.Solo);

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql");

var app = builder.Build();

app.MapDefaultEndpoints();

// API endpoints
app.MapPost("/accounts", async (CreateAccountRequest request, IDocumentStore store) =>
{
    using var session = store.LightweightSession();
    
    var accountId = Guid.NewGuid();
    var @event = new AccountCreatedEvent(
        accountId,
        Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
        request.CustomerName,
        request.Email,
        DateTime.UtcNow);
    
    session.Events.StartStream<Account>(accountId, @event);
    await session.SaveChangesAsync();
    
    return Results.Created($"/accounts/{accountId}", new { AccountId = accountId });
});

app.MapPut("/accounts/{accountId:guid}", async (Guid accountId, UpdateAccountRequest request, IDocumentStore store) =>
{
    using var session = store.LightweightSession();
    
    var @event = new AccountUpdatedEvent(
        accountId,
        request.CustomerName,
        request.Email,
        DateTime.UtcNow);
    
    session.Events.Append(accountId, @event);
    await session.SaveChangesAsync();
    
    return Results.Ok();
});

app.MapPost("/transactions", async (CreateTransactionRequest request, IDocumentStore store) =>
{
    using var session = store.LightweightSession();
    
    var transactionId = Guid.NewGuid();
    var @event = new TransactionCreatedEvent(
        transactionId,
        request.AccountId,
        request.Amount,
        request.TransactionType,
        request.Description,
        DateTime.UtcNow);
    
    session.Events.Append(request.AccountId, @event);
    await session.SaveChangesAsync();
    
    return Results.Created($"/transactions/{transactionId}", new { TransactionId = transactionId });
});

app.Run();

// Request/Response DTOs
public record CreateAccountRequest(string CustomerName, string Email);
public record UpdateAccountRequest(string CustomerName, string Email);
public record CreateTransactionRequest(Guid AccountId, decimal Amount, string TransactionType, string Description);

// Stream aggregates
public class Account
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}

// Projection for account summary (shared with AuditTrail service)
public class AccountProjection : MultiStreamProjection<AccountSummary, Guid>
{
    public AccountProjection()
    {
        Identity<AccountCreatedEvent>(e => e.AccountId);
        Identity<AccountUpdatedEvent>(e => e.AccountId);
        Identity<TransactionCreatedEvent>(e => e.AccountId);
    }

    public void Apply(AccountSummary summary, AccountCreatedEvent @event)
    {
        summary.Id = @event.AccountId;
        summary.AccountNumber = @event.AccountNumber;
        summary.CustomerName = @event.CustomerName;
        summary.Email = @event.Email;
        summary.CreatedAt = @event.CreatedAt;
        summary.LastUpdatedAt = @event.CreatedAt;
    }

    public void Apply(AccountSummary summary, AccountUpdatedEvent @event)
    {
        summary.CustomerName = @event.CustomerName;
        summary.Email = @event.Email;
        summary.LastUpdatedAt = @event.UpdatedAt;
    }

    public void Apply(AccountSummary summary, TransactionCreatedEvent @event)
    {
        summary.TransactionCount++;
        summary.TotalTransactionAmount += @event.Amount;
        summary.LastUpdatedAt = @event.CreatedAt;
    }
}

// Projection document
public class AccountSummary
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalTransactionAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}

