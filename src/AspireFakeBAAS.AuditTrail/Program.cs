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
    
    // Configure projections - using Inline (projections are built by API service)
    opts.Projections.Add<AccountProjection>(ProjectionLifecycle.Inline);
})
.UseLightweightSessions();

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql");

var app = builder.Build();

app.MapDefaultEndpoints();

// Query endpoints
app.MapGet("/audit/accounts/{accountId:guid}/events", async (Guid accountId, IDocumentStore store) =>
{
    using var session = store.LightweightSession();
    var events = await session.Events.FetchStreamAsync(accountId);
    
    return Results.Ok(events.Select(e => new
    {
        EventId = e.Id,
        EventType = e.EventType.Name,
        Version = e.Version,
        Timestamp = e.Timestamp,
        Data = e.Data
    }));
});

app.MapGet("/audit/accounts/{accountId:guid}", async (Guid accountId, IQuerySession session) =>
{
    var account = await session.LoadAsync<AccountSummary>(accountId);
    return account is not null ? Results.Ok(account) : Results.NotFound();
});

app.MapGet("/audit/events", async (IQuerySession session, int? skip = 0, int? take = 100) =>
{
    var events = await session.Events.QueryAllRawEvents()
        .OrderByDescending(e => e.Timestamp)
        .Skip(skip ?? 0)
        .Take(Math.Min(take ?? 100, 100))
        .ToListAsync();
    
    return Results.Ok(events.Select(e => new
    {
        StreamId = e.StreamId,
        EventId = e.Id,
        EventType = e.EventType.Name,
        Version = e.Version,
        Timestamp = e.Timestamp,
        Data = e.Data
    }));
});

app.Run();

// Projection for account summary
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

