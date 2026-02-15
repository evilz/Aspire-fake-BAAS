var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL with persistent data
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var postgresDb = postgres.AddDatabase("postgresdb");

// Add API service
var api = builder.AddProject("api", "../AspireFakeBAAS.Api/AspireFakeBAAS.Api.csproj")
    .WithReference(postgresDb)
    .WaitFor(postgresDb);

// Add AuditTrail service
var auditTrail = builder.AddProject("audittrail", "../AspireFakeBAAS.AuditTrail/AspireFakeBAAS.AuditTrail.csproj")
    .WithReference(postgresDb)
    .WaitFor(postgresDb);

builder.Build().Run();

