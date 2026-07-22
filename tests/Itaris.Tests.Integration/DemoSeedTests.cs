using Itaris.Api.Seeding;
using Itaris.Modules.Merchants.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Tests.Integration;

/// <summary>The demo seeder produces the mock world and is idempotent (a second run is a no-op).</summary>
[Collection(ApiCollection.Name)]
public class DemoSeedTests(ApiFixture fixture)
{
    [Fact]
    public async Task Seeder_creates_the_mock_world_once_and_is_idempotent()
    {
        using var scope = fixture.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();

        await seeder.SeedAsync();
        await seeder.SeedAsync(); // second run must not duplicate

        var db = scope.ServiceProvider.GetRequiredService<MerchantsDbContext>();
        Assert.Equal(1, await db.Merchants.CountAsync(m => m.Code == "WASHLEH"));
        Assert.Equal(4, await db.Merchants.CountAsync(m =>
            m.Code == "WASHLEH" || m.Code == "REEF" || m.Code == "WEIBDEH" || m.Code == "LAMSA"));
    }
}
