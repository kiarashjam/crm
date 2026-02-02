using ACI.Application.Interfaces;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Repositories;
using ACI.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ACI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=ACI;Trusted_Connection=True;MultipleActiveResultSets=true";

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IDealRepository, DealRepository>();
        services.AddScoped<ILeadRepository, LeadRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<ICopyHistoryRepository, CopyHistoryRepository>();
        services.AddScoped<IReportingService, ReportingService>();

        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ICopyGenerator, TemplateCopyGenerator>();

        services.AddDataProtection().SetApplicationName("ACI");
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        var jwtSecret = configuration["Jwt:SecretKey"] ?? "ACI-SuperSecretKey-ChangeInProduction-Min32Chars";
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "ACI";
        var jwtAudience = configuration["Jwt:Audience"] ?? "ACI";
        var jwtExpiry = int.TryParse(configuration["Jwt:ExpiryMinutes"], out var exp) ? exp : 1440;
        services.AddSingleton<ITokenService>(_ => new JwtTokenService(jwtIssuer, jwtAudience, jwtSecret, jwtExpiry));

        return services;
    }
}
