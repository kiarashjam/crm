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

        // Signing in without a password: single sign-on with Google and Microsoft (off until
        // the Sso section names a client id and secret), and the sign-in steps both keep.
        services.Configure<SingleSignOnSettings>(configuration.GetSection(SingleSignOnSettings.SectionName));
        services.AddSingleton<ACI.Infrastructure.Services.SignIn.OidcProviderCatalog>();
        services.AddSingleton<ACI.Infrastructure.Services.SignIn.OidcMetadataSource>();
        services.AddHttpClient(ACI.Infrastructure.Services.SignIn.OidcMetadataSource.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<IAuthChallenges, ACI.Infrastructure.Services.SignIn.AuthChallengeStore>();
        services.AddScoped<SignInSessions>();
        services.AddScoped<ISingleSignOn, ACI.Infrastructure.Services.SignIn.SingleSignOnService>();
        services.AddScoped<IOrganizationSso, ACI.Infrastructure.Services.SignIn.OrganizationSsoService>();
        // Passkeys: on once the app's address is known (Passkeys, else Email:FrontendBaseUrl).
        services.Configure<PasskeySettings>(configuration.GetSection(PasskeySettings.SectionName));
        services.AddSingleton<ACI.Infrastructure.Services.SignIn.PasskeyChallengeSource>();
        services.AddScoped<IPasskeys, ACI.Infrastructure.Services.SignIn.PasskeyService>();

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
        // Which of the Sales and Club modules a workspace has on (InModuleAttribute asks it).
        services.AddScoped<IWorkspaceModules, WorkspaceModuleService>();
        services.AddScoped<IDealLineItemRepository, DealLineItemRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IOnboardingRepository, OnboardingRepository>();
        services.AddScoped<IEmailImageRepository, EmailImageRepository>();
        services.AddScoped<IAutomationRepository, AutomationRepository>();
        // An automation's runs and their logs, and the records they are about.
        services.AddScoped<IAutomationRunStore, AutomationRunStore>();
        services.AddScoped<IAutomationRecordReader, ACI.Infrastructure.Services.AutomationRecordReader>();
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
        // The club's figures (Club Reports, Today at the Club), summed by the database.
        services.AddScoped<IClubReports, ClubReportService>();
        // Keeping members and growing through them: the renewals board, each member's at-risk
        // score with the save actions it sets off, and referrals, sponsors and their bonus.
        services.AddScoped<IClubRenewals, ACI.Infrastructure.Services.Retention.ClubRenewalService>();
        services.AddScoped<ICorporateMemberships, CorporateMembershipService>();
        services.AddScoped<IClubRetention, ACI.Infrastructure.Services.Retention.ClubRetentionService>();
        services.AddScoped<IClubReferrals, ACI.Infrastructure.Services.Retention.ClubReferralService>();
        // Money from outside the club: payment settings, QR-bills, bank files, Stripe and the
        // accounting journal. Every payment they take goes through the ledger above.
        services.AddScoped<ACI.Infrastructure.Services.Payments.ClubPaymentSettingsService>();
        services.AddScoped<IClubPaymentSettings>(sp => sp.GetRequiredService<ACI.Infrastructure.Services.Payments.ClubPaymentSettingsService>());
        services.AddScoped<ACI.Infrastructure.Services.Payments.ClubQrBillService>();
        services.AddScoped<IClubQrBills>(sp => sp.GetRequiredService<ACI.Infrastructure.Services.Payments.ClubQrBillService>());
        services.AddScoped<IClubBankFiles, ACI.Infrastructure.Services.Payments.ClubBankFileService>();
        services.AddSingleton<ACI.Infrastructure.Services.Payments.PayLinkSigner>();
        services.AddScoped<IClubPayments, ACI.Infrastructure.Services.Payments.ClubPaymentsService>();
        // A membership contract's deposit: paid on the signing page or by QR-bill, and put on the
        // member's account once the club's approval has admitted them.
        services.AddScoped<IContractDeposits, ACI.Infrastructure.Services.Payments.ContractDepositService>();
        services.AddScoped<IClubAccounting, ACI.Infrastructure.Services.Payments.ClubAccountingService>();
        services.AddHttpClient(ACI.Infrastructure.Services.Payments.StripePaymentProvider.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<IPaymentProvider, ACI.Infrastructure.Services.Payments.StripePaymentProvider>();
        // The POS (Lightspeed Restaurant K-Series): its closed tickets come in and are posted to
        // members as sales through the ledger above.
        services.AddScoped<IClubPos, ACI.Infrastructure.Services.Pos.ClubPosService>();
        services.AddSingleton<ACI.Infrastructure.Services.Pos.PosConnectState>();
        services.AddHttpClient(ACI.Infrastructure.Services.Pos.LightspeedKSeriesProvider.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(60));
        services.AddSingleton<IPosProvider, ACI.Infrastructure.Services.Pos.LightspeedKSeriesProvider>();
        // A person's data rights (export, erasure) across every module.
        services.AddScoped<ACI.Infrastructure.Services.Privacy.PersonDataService>();
        services.AddScoped<IPersonData>(sp => sp.GetRequiredService<ACI.Infrastructure.Services.Privacy.PersonDataService>());
        // The workspace's privacy settings, the public privacy page, and the retention run.
        services.AddScoped<IPrivacySettings, ACI.Infrastructure.Services.Privacy.PrivacySettingsService>();
        services.AddScoped<IRetention, ACI.Infrastructure.Services.Privacy.RetentionService>();
        // The member portal: members sign in with emailed links into sessions of their own, and
        // see their own club account; staff turn it on, invite members and end sessions.
        services.AddScoped<ACI.Infrastructure.Services.Portal.MemberPortalAccess>();
        services.AddScoped<IMemberPortalSignIn>(sp => sp.GetRequiredService<ACI.Infrastructure.Services.Portal.MemberPortalAccess>());
        services.AddScoped<IMemberPortalAdmin>(sp => sp.GetRequiredService<ACI.Infrastructure.Services.Portal.MemberPortalAccess>());
        services.AddScoped<IMemberPortal, ACI.Infrastructure.Services.Portal.MemberPortalService>();
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

        // The assistant's language model, behind a provider-neutral interface. OpenAI first,
        // on the AI writer's key; a provider this server does not know leaves the assistant
        // "not configured", and the app keeps its built-in rules.
        services.Configure<AssistantOptions>(configuration.GetSection(AssistantOptions.SectionName));
        services.AddHttpClient(ACI.Infrastructure.Services.Assistant.OpenAIAssistantModel.HttpClientName, (sp, client) =>
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(sp.GetRequiredService<IOptions<AssistantOptions>>().Value.TimeoutSeconds, 5, 120)));
        services.AddSingleton<IAssistantModel>(sp =>
        {
            var provider = sp.GetRequiredService<IOptions<AssistantOptions>>().Value.Provider?.Trim() ?? "";
            return string.Equals(provider, "OpenAI", StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<ACI.Infrastructure.Services.Assistant.OpenAIAssistantModel>(sp)
                : new ACI.Infrastructure.Services.Assistant.UnconfiguredAssistantModel(provider);
        });
        // What calls to the model may cost: the month's allowance per workspace (or person,
        // outside any organization) and the short-term throttle per workspace; and the settings
        // a manager keeps them by.
        services.AddSingleton<ACI.Infrastructure.Services.Assistant.AiWorkspaceThrottle>();
        services.AddScoped<IAiBudget, ACI.Infrastructure.Services.Assistant.AiBudget>();
        services.AddScoped<IAssistantSettingsService, ACI.Infrastructure.Services.Assistant.AssistantSettingsService>();
        services.AddScoped<IClubAssistantReads, ACI.Infrastructure.Services.Assistant.ClubAssistantReads>();
        services.AddScoped<IMorningBrief, ACI.Infrastructure.Services.Assistant.MorningBriefService>();

        services.AddDataProtection().SetApplicationName("ACI");
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        var jwtSecret = configuration["Jwt:SecretKey"] ?? "ACI-SuperSecretKey-ChangeInProduction-Min32Chars";
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "ACI";
        var jwtAudience = configuration["Jwt:Audience"] ?? "ACI";
        var jwtExpiry = int.TryParse(configuration["Jwt:ExpiryMinutes"], out var exp) ? exp : 1440;
        services.AddSingleton<ITokenService>(_ => new JwtTokenService(jwtIssuer, jwtAudience, jwtSecret, jwtExpiry));
        // Member portal sessions: signed with a key of their own, derived from the same secret.
        services.AddSingleton(_ => new ACI.Infrastructure.Services.Portal.MemberPortalTokens(jwtIssuer, jwtSecret));

        return services;
    }
}
