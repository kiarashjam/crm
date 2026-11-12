using ACI.Application.Configuration;
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

        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IDuplicateRepository, DuplicateRepository>();
        services.AddScoped<IContactRelationshipRepository, ContactRelationshipRepository>();
        services.AddScoped<IDealRepository, DealRepository>();
        services.AddScoped<ILeadRepository, LeadRepository>();
        services.AddScoped<ILeadOpsRepository, LeadOpsRepository>();
        services.AddScoped<ISequenceRepository, SequenceRepository>();
        services.AddScoped<IPipelineRepository, PipelineRepository>();
        services.AddScoped<IDealStageRepository, DealStageRepository>();
        services.AddScoped<ILeadStatusRepository, LeadStatusRepository>();
        services.AddScoped<ILeadSourceRepository, LeadSourceRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        services.AddScoped<IJoinRequestRepository, JoinRequestRepository>();
        services.AddScoped<IContractRepository, ContractRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<ICustomFieldRepository, CustomFieldRepository>();
        services.AddScoped<IWorkspaceRecords, WorkspaceRecords>();
        services.AddScoped<IDealLineItemRepository, DealLineItemRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IOnboardingRepository, OnboardingRepository>();
        services.AddScoped<IEmailImageRepository, EmailImageRepository>();
        services.AddScoped<IAutomationRepository, AutomationRepository>();
        services.AddScoped<IClubRepository, ClubRepository>();
        services.AddScoped<IClubDocumentRepository, ClubDocumentRepository>();
        services.AddScoped<IClubLedger, ClubLedgerService>();
        // Events and their guest lists, resources and their bookings; their money goes through the ledger.
        services.AddScoped<IClubEvents, ClubEventService>();
        services.AddScoped<IClubReservations, ClubReservationService>();
        services.AddSingleton<ILedgerReadObserver, NoLedgerReadObserver>();
        // The club's price catalogue, and the billing that runs on the ledger.
        services.AddScoped<IClubCatalog, ClubCatalogService>();
        services.AddScoped<IClubBilling, ClubBillingService>();
        // How people join (applications, the public form, approval) and who belongs together.
        services.AddScoped<IClubApplications, ClubApplicationService>();
        services.AddScoped<IClubHouseholds, ClubHouseholdService>();
        // Money from outside the club: payment settings, QR-bills, bank files, Stripe and the
        // accounting journal. Every payment they take goes through the ledger above.
        services.AddScoped<ACI.Infrastructure.Services.Payments.ClubPaymentSettingsService>();
        services.AddScoped<IClubPaymentSettings>(sp => sp.GetRequiredService<ACI.Infrastructure.Services.Payments.ClubPaymentSettingsService>());
        services.AddScoped<ACI.Infrastructure.Services.Payments.ClubQrBillService>();
        services.AddScoped<IClubQrBills>(sp => sp.GetRequiredService<ACI.Infrastructure.Services.Payments.ClubQrBillService>());
        services.AddScoped<IClubBankFiles, ACI.Infrastructure.Services.Payments.ClubBankFileService>();
        services.AddSingleton<ACI.Infrastructure.Services.Payments.PayLinkSigner>();
        services.AddScoped<IClubPayments, ACI.Infrastructure.Services.Payments.ClubPaymentsService>();
        services.AddScoped<IClubAccounting, ACI.Infrastructure.Services.Payments.ClubAccountingService>();
        services.AddHttpClient(ACI.Infrastructure.Services.Payments.StripePaymentProvider.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<IPaymentProvider, ACI.Infrastructure.Services.Payments.StripePaymentProvider>();
        // The POS (Lightspeed Restaurant K-Series): its closed tickets come in and are posted to
        // members as sales through the ledger above.
        services.AddScoped<IClubPos, ACI.Infrastructure.Services.Pos.ClubPosService>();
        services.AddSingleton<ACI.Infrastructure.Services.Pos.PosConnectState>();
        services.AddHttpClient(ACI.Infrastructure.Services.Pos.LightspeedKSeriesProvider.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(60));
        services.AddSingleton<IPosProvider, ACI.Infrastructure.Services.Pos.LightspeedKSeriesProvider>();
        services.AddScoped<IConsentRepository, ConsentRepository>();
        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<IImportRepository, ImportRepository>();
        services.AddSingleton<IAttachmentLinks, AttachmentLinkSigner>();
        services.AddSingleton<IUnsubscribeLinks, UnsubscribeLinkSigner>();
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<ICopyHistoryRepository, CopyHistoryRepository>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IDealBoardService, DealBoardService>();
        services.AddScoped<WeeklyDigestService>();
        services.AddScoped<IWebhookTransport, WebhookTransport>();
        
        // Application services
        services.AddScoped<ITemplateService, TemplateService>();

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
