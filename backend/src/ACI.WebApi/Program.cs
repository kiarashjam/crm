using System.Reflection;
using System.Text;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Infrastructure;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Middleware;
using ACI.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;

// Configure Serilog early for startup logging
var logConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");

// Only add file logging in Development (Azure App Service doesn't allow local file writes)
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
if (environment == "Development")
{
    logConfig.WriteTo.File(
        path: "logs/aci-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");
}

Log.Logger = logConfig.CreateLogger();

try
{
    Log.Information("Starting ACI CRM API");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for all logging
    builder.Host.UseSerilog();

    // CORS Configuration
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(builder.Configuration["Cors:AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? ["http://localhost:5173", "http://localhost:5174", "http://localhost:3000"])
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddProblemDetails();

    // Global Exception Handler
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // Enhanced Swagger Configuration
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "ACI CRM API",
            Version = "v1",
            Description = "API for the ACI Customer Relationship Management system. Provides endpoints for managing contacts, companies, leads, deals, tasks, activities, and more.",
            Contact = new OpenApiContact
            {
                Name = "ACI Team",
                Email = "support@aci-crm.com"
            },
            License = new OpenApiLicense
            {
                Name = "Proprietary"
            }
        });

        // Include XML comments for better documentation
        var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }

        // JWT Bearer Authentication
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.\n\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        // Organization Header
        options.AddSecurityDefinition("OrganizationId", new OpenApiSecurityScheme
        {
            Description = "Organization ID header for multi-tenant operations",
            Name = "X-Organization-Id",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey
        });
    });

    // Infrastructure services (repositories, EF, etc.)
    builder.Services.AddInfrastructure(builder.Configuration);

    // Application services
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IContactService, ContactService>();
    builder.Services.AddScoped<IDealService, DealService>();
    builder.Services.AddScoped<ILeadService, LeadService>();
    builder.Services.AddScoped<ICompanyService, CompanyService>();
    builder.Services.AddScoped<ITaskService, TaskService>();
    builder.Services.AddScoped<IActivityService, ActivityService>();
    builder.Services.AddScoped<ITemplateService, TemplateService>();
    builder.Services.AddScoped<ICopyHistoryService, CopyHistoryService>();
    builder.Services.AddScoped<ISettingsService, SettingsService>();
    builder.Services.AddScoped<IOrganizationService, ACI.Application.Services.OrganizationService>();
    builder.Services.AddScoped<IInviteService, ACI.Application.Services.InviteService>();
    builder.Services.AddScoped<IJoinRequestService, ACI.Application.Services.JoinRequestService>();
    builder.Services.AddScoped<ICopyGeneratorApplicationService, CopyGeneratorService>();
    builder.Services.AddScoped<ISendToCrmService, SendToCrmService>();
    builder.Services.AddScoped<IPipelineService, PipelineService>();
    builder.Services.AddScoped<IDealStageService, DealStageService>();
    builder.Services.AddScoped<ILeadStatusService, LeadStatusService>();
    builder.Services.AddScoped<ILeadSourceService, LeadSourceService>();
    builder.Services.AddScoped<IGlobalSearchService, GlobalSearchService>();

    // HTTP Context accessor for current user service
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

    // JWT Authentication
    var jwtSecret = builder.Configuration["Jwt:SecretKey"] ?? "ACI-SuperSecretKey-ChangeInProduction-Min32Chars";
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ACI";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ACI";

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            };
        });
    builder.Services.AddAuthorization();

    // Health Checks
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddHealthChecks()
        .AddSqlServer(
            connectionString ?? "Server=(localdb)\\mssqllocaldb;Database=ACI;Trusted_Connection=True;",
            name: "database",
            tags: ["db", "sql"]);

    var app = builder.Build();

    // Configure middleware pipeline - Enable Swagger in all environments for API documentation
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ACI CRM API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "ACI CRM API Documentation";
    });

    // Global exception handler
    app.UseExceptionHandler();

    // Serilog request logging with enrichment
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("UserId", httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous");
            diagnosticContext.Set("OrganizationId", httpContext.Request.Headers["X-Organization-Id"].FirstOrDefault() ?? "none");
            diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        };
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });

    app.UseHttpsRedirection();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    
    // Health check endpoint
    app.MapHealthChecks("/health");
    
    // Simple ping endpoint (no DB)
    app.MapGet("/ping", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));
    
    app.MapControllers();

    // Database migration and seeding
    string? dbError = null;
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        
        try
        {
            var connStr = app.Configuration.GetConnectionString("DefaultConnection") ?? "(not configured)";
            var maskedConnStr = connStr.Length > 30 ? connStr.Substring(0, 30) + "..." : connStr;
            Log.Information("Database connection: {ConnectionString}", maskedConnStr);
            
            // First, create missing tables and fix schema
            Log.Information("Checking and fixing database schema...");
            
            // Create Organizations table if missing
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Organizations')
                BEGIN
                    CREATE TABLE [Organizations] (
                        [Id] uniqueidentifier NOT NULL,
                        [Name] nvarchar(256) NOT NULL,
                        [OwnerUserId] uniqueidentifier NOT NULL,
                        [CreatedAtUtc] datetime2 NOT NULL,
                        [WebhookApiKey] nvarchar(max) NULL,
                        [WebhookApiKeyCreatedAtUtc] datetime2 NULL,
                        CONSTRAINT [PK_Organizations] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Organizations_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) 
                            REFERENCES [Users] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_Organizations_OwnerUserId] ON [Organizations] ([OwnerUserId]);
                END;
            ");
            
            // Create OrganizationMembers table if missing
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OrganizationMembers')
                BEGIN
                    CREATE TABLE [OrganizationMembers] (
                        [OrganizationId] uniqueidentifier NOT NULL,
                        [UserId] uniqueidentifier NOT NULL,
                        [Role] nvarchar(32) NOT NULL,
                        [JoinedAtUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_OrganizationMembers] PRIMARY KEY ([OrganizationId], [UserId]),
                        CONSTRAINT [FK_OrganizationMembers_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) 
                            REFERENCES [Organizations] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_OrganizationMembers_Users_UserId] FOREIGN KEY ([UserId]) 
                            REFERENCES [Users] ([Id]) ON DELETE NO ACTION
                    );
                    CREATE INDEX [IX_OrganizationMembers_UserId] ON [OrganizationMembers] ([UserId]);
                END;
            ");
            
            // Create OrgSettings table if missing
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OrgSettings')
                BEGIN
                    CREATE TABLE [OrgSettings] (
                        [OrganizationId] uniqueidentifier NOT NULL,
                        [CompanyName] nvarchar(256) NOT NULL,
                        [BrandTone] nvarchar(32) NOT NULL,
                        [UpdatedAtUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_OrgSettings] PRIMARY KEY ([OrganizationId]),
                        CONSTRAINT [FK_OrgSettings_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) 
                            REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
                    );
                END;
            ");
            
            // Create Invites table if missing
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Invites')
                BEGIN
                    CREATE TABLE [Invites] (
                        [Id] uniqueidentifier NOT NULL,
                        [OrganizationId] uniqueidentifier NOT NULL,
                        [Email] nvarchar(256) NOT NULL,
                        [Token] nvarchar(128) NOT NULL,
                        [ExpiresAtUtc] datetime2 NOT NULL,
                        [CreatedAtUtc] datetime2 NOT NULL,
                        [AcceptedByUserId] uniqueidentifier NULL,
                        [AcceptedAtUtc] datetime2 NULL,
                        CONSTRAINT [PK_Invites] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Invites_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) 
                            REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX [IX_Invites_Token] ON [Invites] ([Token]);
                    CREATE INDEX [IX_Invites_OrganizationId_Email] ON [Invites] ([OrganizationId], [Email]);
                END;
            ");
            
            // Create JoinRequests table if missing
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'JoinRequests')
                BEGIN
                    CREATE TABLE [JoinRequests] (
                        [Id] uniqueidentifier NOT NULL,
                        [OrganizationId] uniqueidentifier NOT NULL,
                        [UserId] uniqueidentifier NOT NULL,
                        [Status] nvarchar(32) NOT NULL,
                        [CreatedAtUtc] datetime2 NOT NULL,
                        [RespondedByUserId] uniqueidentifier NULL,
                        [RespondedAtUtc] datetime2 NULL,
                        CONSTRAINT [PK_JoinRequests] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_JoinRequests_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) 
                            REFERENCES [Organizations] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_JoinRequests_Users_UserId] FOREIGN KEY ([UserId]) 
                            REFERENCES [Users] ([Id]) ON DELETE NO ACTION
                    );
                    CREATE INDEX [IX_JoinRequests_OrganizationId_UserId] ON [JoinRequests] ([OrganizationId], [UserId]);
                END;
            ");
            
            // Create Pipelines table if missing
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Pipelines')
                BEGIN
                    CREATE TABLE [Pipelines] (
                        [Id] uniqueidentifier NOT NULL,
                        [OrganizationId] uniqueidentifier NOT NULL,
                        [Name] nvarchar(256) NOT NULL,
                        [DisplayOrder] int NOT NULL,
                        CONSTRAINT [PK_Pipelines] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Pipelines_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) 
                            REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_Pipelines_OrganizationId] ON [Pipelines] ([OrganizationId]);
                END;
            ");
            
            // Create DealStages table if missing
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DealStages')
                BEGIN
                    CREATE TABLE [DealStages] (
                        [Id] uniqueidentifier NOT NULL,
                        [PipelineId] uniqueidentifier NOT NULL,
                        [Name] nvarchar(128) NOT NULL,
                        [DisplayOrder] int NOT NULL,
                        [Color] nvarchar(32) NULL,
                        CONSTRAINT [PK_DealStages] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_DealStages_Pipelines_PipelineId] FOREIGN KEY ([PipelineId]) 
                            REFERENCES [Pipelines] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_DealStages_PipelineId] ON [DealStages] ([PipelineId]);
                END;
            ");
            
            // Create LeadSources table if missing
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LeadSources')
                BEGIN
                    CREATE TABLE [LeadSources] (
                        [Id] uniqueidentifier NOT NULL,
                        [OrganizationId] uniqueidentifier NOT NULL,
                        [Name] nvarchar(128) NOT NULL,
                        [DisplayOrder] int NOT NULL,
                        CONSTRAINT [PK_LeadSources] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_LeadSources_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) 
                            REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_LeadSources_OrganizationId] ON [LeadSources] ([OrganizationId]);
                END;
            ");
            
            // Create LeadStatuses table if missing
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LeadStatuses')
                BEGIN
                    CREATE TABLE [LeadStatuses] (
                        [Id] uniqueidentifier NOT NULL,
                        [OrganizationId] uniqueidentifier NOT NULL,
                        [Name] nvarchar(128) NOT NULL,
                        [DisplayOrder] int NOT NULL,
                        CONSTRAINT [PK_LeadStatuses] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_LeadStatuses_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) 
                            REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_LeadStatuses_OrganizationId] ON [LeadStatuses] ([OrganizationId]);
                END;
            ");
            
            // Fix UserSettings table columns
            await db.Database.ExecuteSqlRawAsync(@"
                -- Add missing columns to UserSettings
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'JobTitle')
                    ALTER TABLE [UserSettings] ADD [JobTitle] nvarchar(128) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'AvatarUrl')
                    ALTER TABLE [UserSettings] ADD [AvatarUrl] nvarchar(512) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'Phone')
                    ALTER TABLE [UserSettings] ADD [Phone] nvarchar(32) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'Timezone')
                    ALTER TABLE [UserSettings] ADD [Timezone] nvarchar(64) NOT NULL DEFAULT 'UTC';
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'Language')
                    ALTER TABLE [UserSettings] ADD [Language] nvarchar(10) NOT NULL DEFAULT 'en';
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'Bio')
                    ALTER TABLE [UserSettings] ADD [Bio] nvarchar(500) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'EmailSignature')
                    ALTER TABLE [UserSettings] ADD [EmailSignature] nvarchar(2000) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'DefaultEmailSubjectPrefix')
                    ALTER TABLE [UserSettings] ADD [DefaultEmailSubjectPrefix] nvarchar(100) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'Theme')
                    ALTER TABLE [UserSettings] ADD [Theme] int NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'DataDensity')
                    ALTER TABLE [UserSettings] ADD [DataDensity] int NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'SidebarCollapsed')
                    ALTER TABLE [UserSettings] ADD [SidebarCollapsed] bit NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'ShowWelcomeBanner')
                    ALTER TABLE [UserSettings] ADD [ShowWelcomeBanner] bit NOT NULL DEFAULT 1;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'EmailNotificationsEnabled')
                    ALTER TABLE [UserSettings] ADD [EmailNotificationsEnabled] bit NOT NULL DEFAULT 1;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'EmailOnNewLead')
                    ALTER TABLE [UserSettings] ADD [EmailOnNewLead] bit NOT NULL DEFAULT 1;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'EmailOnDealUpdate')
                    ALTER TABLE [UserSettings] ADD [EmailOnDealUpdate] bit NOT NULL DEFAULT 1;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'EmailOnTaskDue')
                    ALTER TABLE [UserSettings] ADD [EmailOnTaskDue] bit NOT NULL DEFAULT 1;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'EmailOnTeamMention')
                    ALTER TABLE [UserSettings] ADD [EmailOnTeamMention] bit NOT NULL DEFAULT 1;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'EmailDigestFrequency')
                    ALTER TABLE [UserSettings] ADD [EmailDigestFrequency] int NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'InAppNotificationsEnabled')
                    ALTER TABLE [UserSettings] ADD [InAppNotificationsEnabled] bit NOT NULL DEFAULT 1;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'InAppSoundEnabled')
                    ALTER TABLE [UserSettings] ADD [InAppSoundEnabled] bit NOT NULL DEFAULT 1;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrowserNotificationsEnabled')
                    ALTER TABLE [UserSettings] ADD [BrowserNotificationsEnabled] bit NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'DefaultPipelineId')
                    ALTER TABLE [UserSettings] ADD [DefaultPipelineId] nvarchar(64) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'DefaultLeadStatusId')
                    ALTER TABLE [UserSettings] ADD [DefaultLeadStatusId] nvarchar(64) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'DefaultLeadSourceId')
                    ALTER TABLE [UserSettings] ADD [DefaultLeadSourceId] nvarchar(64) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'DefaultFollowUpDays')
                    ALTER TABLE [UserSettings] ADD [DefaultFollowUpDays] int NOT NULL DEFAULT 3;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'DefaultCurrency')
                    ALTER TABLE [UserSettings] ADD [DefaultCurrency] nvarchar(10) NULL DEFAULT 'USD';
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'ShowActivityStatus')
                    ALTER TABLE [UserSettings] ADD [ShowActivityStatus] bit NOT NULL DEFAULT 1;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'AllowAnalytics')
                    ALTER TABLE [UserSettings] ADD [AllowAnalytics] bit NOT NULL DEFAULT 1;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'CreatedAtUtc')
                    ALTER TABLE [UserSettings] ADD [CreatedAtUtc] datetime2 NOT NULL DEFAULT GETUTCDATE();
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'UpdatedAtUtc')
                    ALTER TABLE [UserSettings] ADD [UpdatedAtUtc] datetime2 NOT NULL DEFAULT GETUTCDATE();
            ");
            
            // Fix Contacts table columns
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'OrganizationId')
                BEGIN
                    ALTER TABLE [Contacts] ADD [OrganizationId] uniqueidentifier NULL;
                    CREATE INDEX [IX_Contacts_OrganizationId] ON [Contacts] ([OrganizationId]);
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'IsArchived')
                BEGIN
                    ALTER TABLE [Contacts] ADD [IsArchived] bit NOT NULL DEFAULT 0;
                    CREATE INDEX [IX_Contacts_IsArchived] ON [Contacts] ([IsArchived]);
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'ArchivedAtUtc')
                    ALTER TABLE [Contacts] ADD [ArchivedAtUtc] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'ArchivedByUserId')
                    ALTER TABLE [Contacts] ADD [ArchivedByUserId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'ConvertedFromLeadId')
                    ALTER TABLE [Contacts] ADD [ConvertedFromLeadId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'DoNotContact')
                BEGIN
                    ALTER TABLE [Contacts] ADD [DoNotContact] bit NOT NULL DEFAULT 0;
                    CREATE INDEX [IX_Contacts_DoNotContact] ON [Contacts] ([DoNotContact]);
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'DoNotContactReason')
                    ALTER TABLE [Contacts] ADD [DoNotContactReason] nvarchar(512) NULL;
            ");
            await db.Database.ExecuteSqlRawAsync(@"
                -- Add missing columns to Templates table if they don't exist
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Templates') AND name = 'IsSystemTemplate')
                BEGIN
                    ALTER TABLE [Templates] ADD [IsSystemTemplate] bit NOT NULL DEFAULT 0;
                    CREATE INDEX [IX_Templates_IsSystemTemplate] ON [Templates] ([IsSystemTemplate]);
                END;
                
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Templates') AND name = 'IsSharedWithOrganization')
                BEGIN
                    ALTER TABLE [Templates] ADD [IsSharedWithOrganization] bit NOT NULL DEFAULT 0;
                END;
                
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Templates') AND name = 'OrganizationId')
                BEGIN
                    ALTER TABLE [Templates] ADD [OrganizationId] uniqueidentifier NULL;
                    CREATE INDEX [IX_Templates_OrganizationId] ON [Templates] ([OrganizationId]);
                END;
                
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Templates') AND name = 'Content')
                BEGIN
                    ALTER TABLE [Templates] ADD [Content] nvarchar(4000) NULL;
                END;
                
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Templates') AND name = 'BrandTone')
                BEGIN
                    ALTER TABLE [Templates] ADD [BrandTone] nvarchar(64) NULL;
                END;
                
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Templates') AND name = 'Length')
                BEGIN
                    ALTER TABLE [Templates] ADD [Length] nvarchar(32) NULL;
                END;
                
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Templates') AND name = 'UpdatedAtUtc')
                BEGIN
                    ALTER TABLE [Templates] ADD [UpdatedAtUtc] datetime2 NULL;
                END;
            ");
            
            // Fix existing orphan templates to be system templates
            await db.Database.ExecuteSqlRawAsync(@"
                UPDATE [Templates] 
                SET [IsSystemTemplate] = 1 
                WHERE [UserId] IS NULL AND [IsSystemTemplate] = 0;
            ");
            Log.Information("Schema check complete");
            
            Log.Information("Applying database migrations...");
            try
            {
                await db.Database.MigrateAsync();
            }
            catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number == 2714) // Object already exists
            {
                Log.Warning("Tables already exist (error 2714), marking InitialCreate migration as applied...");
                
                // Ensure __EFMigrationsHistory table exists and mark InitialCreate as applied
                await db.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '__EFMigrationsHistory')
                    BEGIN
                        CREATE TABLE [__EFMigrationsHistory] (
                            [MigrationId] nvarchar(150) NOT NULL,
                            [ProductVersion] nvarchar(32) NOT NULL,
                            CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                        );
                    END;
                    
                    IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260206130556_InitialCreate')
                    BEGIN
                        INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) 
                        VALUES ('20260206130556_InitialCreate', '8.0.11');
                    END;
                ");
                
                Log.Information("InitialCreate marked as applied, migration history fixed");
            }
            
            Log.Information("Seeding database...");
            await SeedData.SeedAsync(db, passwordHasher);
            
            Log.Information("Database initialization complete");
        }
        catch (Exception dbEx)
        {
            dbError = dbEx.ToString();
            Log.Error(dbEx, "Database migration failed (app will start but DB operations will fail): {ErrorMessage}", dbEx.Message);
            // Don't throw - allow app to start for debugging
        }
    }
    
    // Add diagnostic endpoint that shows DB error if any
    app.MapGet("/db-status", () => dbError == null 
        ? Results.Ok(new { status = "ok", message = "Database initialized successfully" }) 
        : Results.Ok(new { status = "error", message = dbError }));
    
    // Test templates directly
    app.MapGet("/db-test-templates", async (AppDbContext db) =>
    {
        try
        {
            var count = await db.Templates.CountAsync();
            var sample = await db.Templates.Select(t => new { t.Id, t.Title, t.IsSystemTemplate }).Take(3).ToListAsync();
            return Results.Ok(new { status = "ok", count, sample });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.ToString() });
        }
    });
    
    // Test organizations
    app.MapGet("/db-test-orgs", async (AppDbContext db) =>
    {
        try
        {
            var count = await db.Organizations.CountAsync();
            return Results.Ok(new { status = "ok", count });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.Message, detail = ex.ToString() });
        }
    });
    
    // Test user settings
    app.MapGet("/db-test-settings", async (AppDbContext db) =>
    {
        try
        {
            var count = await db.UserSettings.CountAsync();
            return Results.Ok(new { status = "ok", count });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.Message, detail = ex.ToString() });
        }
    });
    
    // Test contacts
    app.MapGet("/db-test-contacts", async (AppDbContext db) =>
    {
        try
        {
            var count = await db.Contacts.CountAsync();
            return Results.Ok(new { status = "ok", count });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.Message, detail = ex.ToString() });
        }
    });
    
    // Test settings service with actual user
    app.MapGet("/db-test-settings-service", async (AppDbContext db, ISettingsService settingsService) =>
    {
        try
        {
            var firstUser = await db.Users.FirstOrDefaultAsync();
            if (firstUser == null) return Results.Ok(new { status = "no_users", message = "No users in database" });
            var settings = await settingsService.GetAsync(firstUser.Id);
            return Results.Ok(new { status = "ok", userId = firstUser.Id, settings });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.Message, detail = ex.ToString() });
        }
    });
    
    // Test contacts service with actual user
    app.MapGet("/db-test-contacts-service", async (AppDbContext db, IContactService contactService) =>
    {
        try
        {
            var firstUser = await db.Users.FirstOrDefaultAsync();
            if (firstUser == null) return Results.Ok(new { status = "no_users", message = "No users in database" });
            var contacts = await contactService.GetContactsPagedAsync(firstUser.Id, null, 1, 10);
            return Results.Ok(new { status = "ok", userId = firstUser.Id, totalCount = contacts.TotalCount });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.Message, detail = ex.ToString() });
        }
    });

    Log.Information("ACI CRM API started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Expose Program class for integration testing with WebApplicationFactory
public partial class Program { }
