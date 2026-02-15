# Aspire-fake-BAAS

A .NET 10 microservices solution using .NET Aspire demonstrating a mini "Bank-as-a-Service" backend with event sourcing and audit trails.

## Architecture

This solution implements a microservices architecture with:

- **Aspire AppHost**: Orchestrates local resources and services
- **API Service**: Minimal API for account and transaction operations
- **AuditTrail Service**: Query service for event streams and projections
- **PostgreSQL**: Event store database (via container)
- **Marten**: Event sourcing framework with projections
- **OpenTelemetry**: Distributed tracing and metrics
- **Health Checks**: Service health monitoring

## Features

- ✅ .NET 10 with Aspire orchestration
- ✅ Event sourcing with Marten
- ✅ PostgreSQL provisioning
- ✅ Shared event contracts
- ✅ Minimal API endpoints
- ✅ Event projections for read models
- ✅ Health checks (/health and /alive endpoints)
- ✅ OpenTelemetry integration
- ✅ Fully runnable locally without Azure dependencies

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

## Running Locally

### Option 1: Using Individual Services

1. Start PostgreSQL:
   ```bash
   docker run -d --name postgres-aspire \
     -e POSTGRES_PASSWORD=postgres \
     -e POSTGRES_USER=postgres \
     -e POSTGRES_DB=aspiredb \
     -p 5432:5432 \
     postgres:16-alpine
   ```

2. Run the API service:
   ```bash
   cd src/AspireFakeBAAS.Api
   dotnet run
   # API runs on http://localhost:5251
   ```

3. Run the AuditTrail service (in another terminal):
   ```bash
   cd src/AspireFakeBAAS.AuditTrail
   dotnet run
   # AuditTrail runs on http://localhost:5131
   ```

### Option 2: Using Aspire AppHost (requires Aspire workload)

```bash
cd src/AspireFakeBAAS.AppHost
dotnet run
```

## API Endpoints

### API Service (port 5251)

- `POST /accounts` - Create a new account
  ```json
  {
    "customerName": "John Doe",
    "email": "john.doe@example.com"
  }
  ```

- `PUT /accounts/{accountId}` - Update account information
  ```json
  {
    "customerName": "John M. Doe",
    "email": "john.m.doe@example.com"
  }
  ```

- `POST /transactions` - Create a transaction
  ```json
  {
    "accountId": "guid",
    "amount": 100.50,
    "transactionType": "deposit",
    "description": "Initial deposit"
  }
  ```

### AuditTrail Service (port 5131)

- `GET /audit/accounts/{accountId}/events` - Get all events for an account
- `GET /audit/accounts/{accountId}` - Get account summary projection
- `GET /audit/events?skip=0&take=100` - Get all events (paginated)

### Health Endpoints (both services)

- `GET /health` - Overall health status
- `GET /alive` - Liveness probe

## Event Contracts

The solution uses the following events:

- `AccountCreatedEvent` - When a new account is created
- `AccountUpdatedEvent` - When account information is updated
- `TransactionCreatedEvent` - When a transaction is recorded

All events are stored in PostgreSQL via Marten and can be queried through the AuditTrail service.

## Example Usage

```bash
# Create an account
ACCOUNT_ID=$(curl -s -X POST http://localhost:5251/accounts \
  -H "Content-Type: application/json" \
  -d '{"customerName":"John Doe","email":"john.doe@example.com"}' \
  | jq -r '.accountId')

# Create a transaction
curl -X POST http://localhost:5251/transactions \
  -H "Content-Type: application/json" \
  -d "{\"accountId\":\"$ACCOUNT_ID\",\"amount\":100.50,\"transactionType\":\"deposit\",\"description\":\"Initial deposit\"}"

# View audit events
curl "http://localhost:5131/audit/accounts/$ACCOUNT_ID/events" | jq .

# View all events
curl "http://localhost:5131/audit/events" | jq .
```

## Project Structure

```
Aspire-fake-BAAS/
├── src/
│   ├── AspireFakeBAAS.AppHost/          # Aspire orchestration host
│   ├── AspireFakeBAAS.ServiceDefaults/  # Shared service configuration
│   ├── AspireFakeBAAS.Contracts/        # Shared event contracts
│   ├── AspireFakeBAAS.Api/              # API service
│   └── AspireFakeBAAS.AuditTrail/       # Audit trail query service
└── AspireFakeBAAS.slnx                  # Solution file
```

## Technologies Used

- .NET 10
- Aspire (13.0.0)
- Marten (7.38.1) - Event Sourcing
- PostgreSQL (16)
- OpenTelemetry (1.11.0)
- ASP.NET Core Minimal APIs

## License

See LICENSE file for details.
