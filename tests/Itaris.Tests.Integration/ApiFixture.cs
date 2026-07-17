using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Itaris.Tests.Integration;

/// <summary>
/// Spins up a real PostgreSQL 16 container (Testcontainers) and hosts the API in-process
/// against it. The Development environment applies migrations on startup, so each test
/// class gets a fully migrated identity schema.
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("itaris")
        .WithUsername("itaris")
        .WithPassword("itaris")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // Environment variables are layered after appsettings.json by WebApplication.CreateBuilder,
        // so this reliably beats the checked-in localhost connection string. In-memory config via
        // ConfigureAppConfiguration is applied as host config (earlier layer) for minimal-hosting
        // apps and loses to appsettings.json — a real local Postgres on 5432 would be hit instead.
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _postgres.GetConnectionString());
    }
}
