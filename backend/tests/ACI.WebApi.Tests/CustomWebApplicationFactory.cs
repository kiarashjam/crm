using ACI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ACI.WebApi.Tests;

/// <summary>
/// Custom WebApplicationFactory for integration testing.
/// Uses in-memory database for isolated test runs.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }
            
            // Remove the existing AppDbContext registration
            services.RemoveAll(typeof(AppDbContext));

            // Add in-memory database for testing with a unique name per factory instance
            var dbName = $"TestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(dbName);
            });

            // Build service provider
            var sp = services.BuildServiceProvider();

            // Create database and seed data
            using var scope = sp.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<AppDbContext>();
            
            // Ensure database is created
            db.Database.EnsureCreated();
        });
    }
    
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Set test environment variables before host creation
        Environment.SetEnvironmentVariable("JWT__Secret", "TestSecretKeyForIntegrationTestsThatIsLongEnoughToBeValid12345");
        Environment.SetEnvironmentVariable("JWT__Issuer", "TestIssuer");
        Environment.SetEnvironmentVariable("JWT__Audience", "TestAudience");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Data Source=:memory:");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("OpenAI__ApiKey", "test-api-key");
        
        return base.CreateHost(builder);
    }
}
