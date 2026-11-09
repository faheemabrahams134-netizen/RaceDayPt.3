using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Api.Tests;

/// <summary>
/// Boots the real API in memory, backed by an EF Core in-memory database,
/// so tests exercise routing, authentication, authorisation and validation end to end.
/// </summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"RaceDayTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Registration:OrganiserCode", TestData.OrganiserCode);
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
            // Never talk to real Azure from unit tests: swap Blob Storage for an in-memory fake.
            services.RemoveAll<IBlobStorageService>();
            services.AddSingleton<IBlobStorageService, InMemoryBlobStorage>();
        });
    }
}
