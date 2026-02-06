using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Infrastructure.Configuration;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Repositories;
using ACI.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        services.AddScoped<IPipelineRepository, PipelineRepository>();
        services.AddScoped<IDealStageRepository, DealStageRepository>();
        services.AddScoped<ILeadStatusRepository, LeadStatusRepository>();
        services.AddScoped<ILeadSourceRepository, LeadSourceRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        services.AddScoped<IJoinRequestRepository, JoinRequestRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<ICopyHistoryRepository, CopyHistoryRepository>();
        services.AddScoped<IEmailSequenceRepository, EmailSequenceRepository>();
        services.AddScoped<IReportingService, ReportingService>();
        
        // Application services
        services.AddScoped<ITemplateService, TemplateService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IEmailSequenceService, EmailSequenceService>();
        services.AddSingleton<ISpamCheckService, SpamCheckService>();
        services.AddSingleton<IABTestService, ABTestService>();
        services.AddSingleton<IEmailSenderService, EmailSenderService>();

        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        
        // Configure OpenAI settings
        services.Configure<OpenAISettings>(configuration.GetSection(OpenAISettings.SectionName));
        
        // Register Intelligent Sales Writer - use AI when configured, otherwise templates
        services.AddHttpClient<OpenAICopyGenerator>();
        services.AddSingleton<ICopyGenerator>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<OpenAISettings>>().Value;
            var logger = sp.GetRequiredService<ILogger<OpenAICopyGenerator>>();
            
            if (settings.IsConfigured)
            {
                logger.LogInformation("OpenAI API key detected - using AI-powered Intelligent Sales Writer (model: {Model})", settings.Model);
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(nameof(OpenAICopyGenerator));
                return new OpenAICopyGenerator(httpClient, sp.GetRequiredService<IOptions<OpenAISettings>>(), logger);
            }
            else
            {
                logger.LogInformation("OpenAI API key not configured - using template-based Intelligent Sales Writer");
                return new TemplateCopyGenerator();
            }
        });

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
