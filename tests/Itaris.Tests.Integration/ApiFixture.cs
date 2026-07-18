using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Itaris.Tests.Integration;

/// <summary>
/// Spins up a real PostgreSQL 16 container (Testcontainers) and hosts the API in-process
/// against it. Startup in the Development environment applies migrations and seeds
/// roles/permissions and the platform admin.
///
/// Shared by every integration test class via <see cref="ApiCollection"/> — see that type for
/// why exactly one instance must exist.
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("itaris")
        .WithUsername("itaris")
        .WithPassword("itaris")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Environment variables are layered after appsettings.json by WebApplication.CreateBuilder,
        // so this reliably beats the checked-in localhost connection string. In-memory config via
        // ConfigureAppConfiguration is applied as host config (an earlier layer) for minimal-hosting
        // apps and loses to appsettings.json — a real local Postgres on 5432 would be hit instead.
        // Set here (not in ConfigureWebHost) so it is in place before any host is built.
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _postgres.GetConnectionString());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Development");
}
