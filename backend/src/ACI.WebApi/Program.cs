using System.Reflection;
using System.Text;
using ACI.Application.Interfaces;
using ACI.Domain.Common;
using ACI.Application.Services;
using ACI.Infrastructure;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Middleware;
using ACI.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
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
    var browserOrigins = builder.Configuration["Cors:AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries)
        ?? ["http://localhost:5173", "http://localhost:5174", "http://localhost:3000"];
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(browserOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                // The list exports say how many rows matched and how many the file holds.
                .WithExposedHeaders("X-Total-Count", "X-Exported-Count");
        });
        // Claude and other MCP hosts call /mcp from their own origins, including preflight OPTIONS.
        options.AddPolicy("Mcp", policy =>
        {
            policy.SetIsOriginAllowed(origin =>
                    ACI.WebApi.Mcp.McpController.IsAllowedOrigin(origin)
                    || browserOrigins.Any(allowed => string.Equals(allowed, origin, StringComparison.OrdinalIgnoreCase)))
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders("MCP-Protocol-Version", "Mcp-Session-Id");
        });
    });

    // An update body that sends a field as null clears it; a field left out is kept.
    builder.Services.AddControllers()
        .AddJsonOptions(options => ACI.WebApi.Extensions.MergePatchJson.TrackNullsSent(options.JsonSerializerOptions));

    // The public form and tracking endpoints are partitioned by form token and by
    // lead, not by IP: behind Azure's front end the connection address may be the
    // proxy's, and X-Forwarded-For is whatever the caller says it is. Sign-up has
    // nothing else to go by, so it alone uses the address (see "register").
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        // A bare 429 reaches people as "HTTP 429" in a toast. Say what happened, in the
        // same problem shape as every other refusal, and when to try again.
        options.OnRejected = async (context, ct) =>
        {
            var wait = context.Lease.TryGetMetadata(System.Threading.RateLimiting.MetadataName.RetryAfter, out var retryAfter)
                ? retryAfter
                : (TimeSpan?)null;
            if (wait is { } w)
                context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(w.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var minutes = wait is { } m ? Math.Max(1, (int)Math.Ceiling(m.TotalMinutes)) : (int?)null;
            await context.HttpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too Many Requests",
                Detail = minutes is { } n
                    ? $"Too many requests. Try again in {n} minute{(n == 1 ? "" : "s")}."
                    : "Too many requests. Wait a few minutes, then try again.",
            }, options: null, contentType: "application/problem+json", cancellationToken: ct);
        };
        options.AddPolicy("public-capture", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            "capture:" + (context.Request.RouteValues["token"]?.ToString() ?? ""),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                // Plenty for a club's website; a script posting the token in a loop
                // stops after twenty a minute instead of filling the lead list.
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
        options.AddPolicy("public-track", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            "track:" + (context.Request.RouteValues["leadId"]?.ToString() ?? ""),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
        // The public application form: a few applications a minute per form link is plenty for
        // a club; a script posting in a loop stops at ten instead of filling the list. Per link,
        // like the capture form, for the same reason (the caller's address can't be trusted).
        options.AddPolicy("public-apply", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            "apply:" + (context.Request.RouteValues["token"]?.ToString() ?? ""),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
        // The unsubscribe link: a person opens it once or twice. Per token, like the
        // two above; a forged token fails its signature check before any database work.
        options.AddPolicy("public-unsubscribe", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            "unsubscribe:" + (context.Request.RouteValues["token"]?.ToString() ?? ""),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
        // Email from the composer goes out under the club's own address, so one
        // account cannot turn it into a mass mailer: twenty a minute per person.
        // Per user, which is why UseRateLimiter runs after UseAuthentication.
        options.AddPolicy("composed-email", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            "email:" + (context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous"),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
        // Invitations go out from the club's sender with text the inviter chose (the
        // workspace name), so one account cannot send thousands. Per inviter, shared
        // by sending and re-sending.
        options.AddPolicy("invite-email", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            "invite:" + (context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous"),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            }));
        // Every draft is a paid call on the server's OpenAI key. Per person, a bucket of
        // twenty that refills one a minute: plenty for someone drafting and rewriting by
        // hand, and a script tops out near 1,400 a day instead of running up the bill.
        options.AddPolicy("ai-copy", context => System.Threading.RateLimiting.RateLimitPartition.GetTokenBucketLimiter(
            "ai:" + (context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous"),
            _ => new System.Threading.RateLimiting.TokenBucketRateLimiterOptions
            {
                TokenLimit = 20,
                TokensPerPeriod = 1,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                AutoReplenishment = true,
                QueueLimit = 0,
            }));
        // Sign-up is anonymous, so there is no account to count against: this one goes
        // by the connection's address. If Azure's front end hides the caller's address
        // behind its own, the limit becomes one shared by everyone signing up, which
        // ten every ten minutes still allows for a club; it stops a script making
        // accounts in bulk either way.
        options.AddPolicy("register", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            "register:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
            }));
    });
    builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
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
    builder.Services.AddSingleton<AuthAttemptLimiter>();
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ACI.WebApi.Services.SecurityStampCache>();
    builder.Services.AddSingleton<ISecurityStampCache>(sp => sp.GetRequiredService<ACI.WebApi.Services.SecurityStampCache>());
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IContactService, ContactService>();
    builder.Services.AddScoped<IContactRelationshipService, ContactRelationshipService>();
    builder.Services.AddScoped<IDealService, DealService>();
    builder.Services.AddScoped<ILeadService, LeadService>();
    builder.Services.AddScoped<ILeadAutomation, LeadAutomation>();
    builder.Services.AddScoped<ILeadStatusSync, LeadStatusSync>();
    builder.Services.AddScoped<ILeadOpsService, LeadOpsService>();
    builder.Services.AddScoped<ISequenceService, SequenceService>();
    builder.Services.AddScoped<IOutreachMail, OutreachMail>();
    builder.Services.AddScoped<IUnsubscribeService, UnsubscribeService>();
    builder.Services.AddScoped<IComposedEmailService, ComposedEmailService>();
    // One outbox for every email and webhook a workspace sends (see OutboxService).
    builder.Services.AddScoped<OutboxService>();
    builder.Services.AddScoped<IOutbox>(sp => sp.GetRequiredService<OutboxService>());
    builder.Services.AddScoped<IOutboundWebhookPublisher>(sp => sp.GetRequiredService<OutboxService>());
    builder.Services.AddSingleton<OutboxSignal>();
    builder.Services.AddSingleton<IOutboxSignal>(sp => sp.GetRequiredService<OutboxSignal>());
    builder.Services.AddSingleton<OutboxDispatcher>();
    builder.Services.AddScoped<IInboundEmailService, InboundEmailService>();
    builder.Services.AddScoped<IEmailSendingService, EmailSendingService>();
    builder.Services.AddScoped<IContractService, ContractService>();
    builder.Services.AddScoped<ICompanyService, CompanyService>();
    builder.Services.AddScoped<IDuplicateService, DuplicateService>();
    builder.Services.AddScoped<IProductService, ProductService>();
    builder.Services.AddScoped<IAutomationService, AutomationService>();
    builder.Services.AddScoped<IAutomationEngine, AutomationEngine>();
    builder.Services.AddScoped<IClubService, ClubService>();
    builder.Services.AddScoped<IClubDocumentService, ClubDocumentService>();
    // What the club's settings and price catalogue say the server applies (points, dues, billing).
    builder.Services.AddScoped<IClubRules, ClubRules>();
    // Member consent, which the outbox reads before marketing goes to a member.
    builder.Services.AddScoped<ConsentService>();
    builder.Services.AddScoped<IConsentService>(sp => sp.GetRequiredService<ConsentService>());
    builder.Services.AddScoped<IMemberMailRules>(sp => sp.GetRequiredService<ConsentService>());
    // Campaigns and the communications log, delivered through the outbox.
    builder.Services.AddScoped<ICampaignService, CampaignService>();
    builder.Services.AddScoped<IMemberMessageService, MemberMessageService>();
    builder.Services.AddScoped<ITaskService, TaskService>();
    builder.Services.AddScoped<IActivityService, ActivityService>();
    builder.Services.AddScoped<INotificationService, NotificationService>();
    builder.Services.AddScoped<ICollaborationNotifier, CollaborationNotifier>();
    builder.Services.AddScoped<IWorkQueueService, WorkQueueService>();
    builder.Services.AddScoped<ICalendarService, CalendarService>();
    builder.Services.AddScoped<IAuditService, AuditService>();
    builder.Services.AddScoped<IAttachmentService, AttachmentService>();
    builder.Services.AddScoped<ICustomFieldService, CustomFieldService>();
    builder.Services.AddScoped<IDealLineItemService, DealLineItemService>();
    builder.Services.AddScoped<ITemplateService, TemplateService>();
    builder.Services.AddScoped<ICopyHistoryService, CopyHistoryService>();
    builder.Services.AddScoped<ISettingsService, SettingsService>();
    builder.Services.AddScoped<IOrganizationService, ACI.Application.Services.OrganizationService>();
    // What the caller may do in the request's organization (MemberPermissions), read once per request.
    builder.Services.AddScoped<ACI.Application.Common.IPermissionService, ACI.Application.Common.PermissionService>();
    builder.Services.AddScoped<IInviteService, ACI.Application.Services.InviteService>();
    builder.Services.AddScoped<IJoinRequestService, ACI.Application.Services.JoinRequestService>();
    builder.Services.AddScoped<ICopyGeneratorApplicationService, CopyGeneratorService>();
    builder.Services.AddScoped<ISendToCrmService, SendToCrmService>();
    builder.Services.AddScoped<IPipelineService, PipelineService>();
    builder.Services.AddScoped<IDealStageService, DealStageService>();
    builder.Services.AddScoped<ILeadStatusService, LeadStatusService>();
    builder.Services.AddScoped<ILeadSourceService, LeadSourceService>();
    builder.Services.AddScoped<IGlobalSearchService, GlobalSearchService>();
    builder.Services.AddScoped<IImportService, ImportService>();
    builder.Services.AddScoped<ACI.WebApi.Mcp.CrmMcpTools>();

    // Background services
    builder.Services.AddHostedService<ACI.WebApi.Services.TaskReminderBackgroundService>();
    builder.Services.AddHostedService<ACI.WebApi.Services.ContractReminderBackgroundService>();
    // Posted by the outbox, not while a lead or deal is saved, so a receiver gets a little
    // longer to answer than a save could wait; the handler dials public addresses only.
    builder.Services.AddHttpClient(ACI.Infrastructure.Services.WebhookTransport.ClientName,
            client => client.Timeout = TimeSpan.FromSeconds(10))
        .ConfigurePrimaryHttpMessageHandler(ACI.Infrastructure.Services.WebhookTransport.PublicOnlyHandler);
    builder.Services.AddHostedService<ACI.WebApi.Services.OutboxDispatcherBackgroundService>();
    builder.Services.AddHostedService<ACI.WebApi.Services.LeadFollowUpBackgroundService>();
    builder.Services.AddHostedService<ACI.WebApi.Services.WeeklyDigestBackgroundService>();
    builder.Services.AddHostedService<ACI.WebApi.Services.PersonContactBackfillBackgroundService>();
    builder.Services.AddHostedService<ACI.WebApi.Services.GiftCardExpiryBackgroundService>();
    builder.Services.AddHostedService<ACI.WebApi.Services.CampaignSchedulerBackgroundService>();
    builder.Services.AddHostedService<ACI.WebApi.Services.ClubBillingBackgroundService>();

    // HTTP Context accessor for current user service
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
    // The workspace every read is filtered to (AppDbContext's query filters): the
    // request's, or whichever one a key, a token or a job says it acts for.
    builder.Services.AddScoped<ITenantContext, ACI.WebApi.Services.TenantContext>();

    // JWT Authentication
    // The default below is in the repository, so anyone can sign a token with it for
    // any user id. Outside development and tests the server refuses to start on it:
    // set Jwt__SecretKey (32+ random characters) in the host's configuration.
    const string publicJwtSecret = "ACI-SuperSecretKey-ChangeInProduction-Min32Chars";
    var jwtSecret = builder.Configuration["Jwt:SecretKey"] ?? publicJwtSecret;
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing")
        && (jwtSecret == publicJwtSecret || jwtSecret.Length < 32))
    {
        throw new InvalidOperationException(
            "Jwt:SecretKey is missing, too short, or the public default from the repository. " +
            "Set Jwt__SecretKey to a random value of at least 32 characters.");
    }
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
            options.Events = new JwtBearerEvents
            {
                // Claude sends the organization API key as Authorization: Bearer aci_….
                // That is not a JWT; skip bearer validation so the MCP action can read the key.
                OnMessageReceived = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/mcp"))
                        context.NoResult();
                    return Task.CompletedTask;
                },
                // The 2FA step token is signed with the same key, so it validates like a
                // session token. It proves only the password: accepting it here would let
                // anyone who knows the password skip the code entirely. Its one use is the
                // body of POST /api/auth/login/2fa, which reads it itself.
                //
                // A token lives a day, and a stolen one used to keep working through a
                // password reset. Each token carries the user's security stamp from when
                // it was issued; a reset or a 2FA change replaces the stamp, so older
                // tokens are refused here (401). A deleted user's tokens are refused too.
                OnTokenValidated = async context =>
                {
                    if (context.Principal?.FindFirst("typ")?.Value == "2fa")
                    {
                        context.Fail("A two-factor step token is not a session token.");
                        return;
                    }

                    var userIdClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (!Guid.TryParse(userIdClaim, out var userId))
                    {
                        context.Fail("The token names no user.");
                        return;
                    }

                    var services = context.HttpContext.RequestServices;
                    var carried = context.Principal?.FindFirst(ACI.Infrastructure.Services.JwtTokenService.SecurityStampClaim)?.Value ?? "";
                    var live = await services.GetRequiredService<ACI.WebApi.Services.SecurityStampCache>()
                        .AcceptsAsync(userId, carried, services.GetRequiredService<IUserRepository>(), context.HttpContext.RequestAborted);
                    if (!live)
                        context.Fail("This session was signed out.");
                },
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

    // Immediately after the request logger, so it sits INSIDE it: this rewrites the
    // path on the way back out, after routing has used it and before Serilog reads
    // it. Without this the raw contract signing token — the entire authorisation to
    // sign that contract — is written to the log in plaintext on every request.
    app.UseMiddleware<ACI.WebApi.Middleware.SecretPathRedactionMiddleware>();

    app.UseResponseCompression();
    app.UseHttpsRedirection();
    app.UseCors();
    app.UseAuthentication();
    // After authentication, so a policy can partition by who is calling.
    app.UseRateLimiter();
    app.UseAuthorization();
    app.UseMiddleware<ACI.WebApi.Middleware.OrganizationAccessMiddleware>();
    // Must follow OrganizationAccessMiddleware: the organization header is validated
    // (and stripped when forged) before the read-only role is resolved from it.
    app.UseMiddleware<ACI.WebApi.Middleware.ReadOnlyMemberMiddleware>();

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
            
            // The schema comes from the EF migrations (ACI.Infrastructure/Migrations), the
            // first of which, the baseline, makes the whole schema as it stood when they
            // became its only source. A new database is made by them, here. One made before
            // them, by the first migrations and the start-up DDL below, has the baseline
            // recorded without running it, further down (see SchemaBaseline), and from then
            // on both get every migration after the baseline from the same MigrateAsync.
            //
            // A missing database is only created in development (docker-compose's first
            // start). Anywhere else the database is provisioned first, so a missing one
            // means a mistyped name in the connection string: creating it would start
            // the app on a new, empty database (on Azure, one at the default, paid tier)
            // while the real one sat unused, and people would sign up into it.
            var databaseCreator = db.Database.GetService<IRelationalDatabaseCreator>();
            var databaseExists = await databaseCreator.ExistsAsync();
            if (!databaseExists && !app.Environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "The database named in the connection string does not exist. Outside development it is not "
                    + "created at start-up: create it first (the scripts in scripts/ do), or correct the name.");
            }
            if (!databaseExists)
            {
                Log.Information("No database yet: creating it (development only)...");
                await databaseCreator.CreateAsync();
            }

            // One instance at a time from here to the end of the schema work and seeding
            // (see SchemaLock): two starting together both ran the same migrations.
            await using var schemaLock = await ACI.WebApi.SchemaLock.AcquireAsync(
                db.Database.GetConnectionString()!, TimeSpan.FromMinutes(10));

            // Empty but for, at most, the history table a cut-off first start left behind
            // (see SchemaBaseline.IsEmptyAsync).
            if (await ACI.WebApi.SchemaBaseline.IsEmptyAsync(db))
            {
                Log.Information("Empty database: creating it from the migrations...");
                await db.Database.MigrateAsync();
            }

            // SAFETY NET, FOR ONE RELEASE: the start-up DDL that made the schema before the
            // baseline migration, left to run as it always has so that a database made
            // before the baseline is brought fully up to date before the baseline is recorded
            // on it. Every statement is guarded, so on a database the migrations made it
            // changes nothing but the AppliedDataFixes ledger (SchemaMigrationTests compares
            // the two).
            //
            // - Add nothing to it. A schema change is a migration now, from backend/:
            //   dotnet ef migrations add <Name> -p src/ACI.Infrastructure -s src/ACI.Infrastructure -o Migrations
            // - While it is here, a migration must not drop or rename anything it adds, or
            //   it would put that back on the next start.
            // - Once every database has started on this release (its __EFMigrationsHistory
            //   holds the baseline), delete everything from here to "Schema check complete",
            //   the one-time data fixes in it and the lead status alignment further down
            //   included (each is in AppliedDataFixes by then). Keep the ContactEmailIndexes
            //   call: where active contacts in one workspace share an email it has not yet
            //   been able to build the two unique indexes, and it builds them once the
            //   duplicates are merged (until then each start logs that they are missing).
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

            await db.Database.ExecuteSqlRawAsync(@"
                IF COL_LENGTH(N'dbo.Organizations', N'WebhookPassword') IS NULL
                BEGIN
                    ALTER TABLE [Organizations] ADD [WebhookPassword] nvarchar(256) NULL;
                END
            ");

            // The MCP key, separate from the lead webhook key (stored as a hash).
            await db.Database.ExecuteSqlRawAsync(@"
                IF COL_LENGTH(N'dbo.Organizations', N'McpApiKeyHash') IS NULL
                    ALTER TABLE [Organizations] ADD [McpApiKeyHash] nvarchar(64) NULL;
                IF COL_LENGTH(N'dbo.Organizations', N'McpApiKeyCreatedAtUtc') IS NULL
                    ALTER TABLE [Organizations] ADD [McpApiKeyCreatedAtUtc] datetime2 NULL;
            ");

            // The lead webhook key, stored as a hash like the MCP key. Keys saved in plain
            // text before are hashed in place, so senders using them carry on, and the
            // plain copy is cleared. Its own batch: it names the column added just above.
            await db.Database.ExecuteSqlRawAsync(@"
                IF COL_LENGTH(N'dbo.Organizations', N'WebhookApiKeyHash') IS NULL
                    ALTER TABLE [Organizations] ADD [WebhookApiKeyHash] nvarchar(64) NULL;
            ");
            await db.Database.ExecuteSqlRawAsync(LegacyWebhookKeys.HashInPlaceSql);
            
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

            // What each member may do (MemberPermissions): the preset they start from, and
            // their own list when the owner changed it. Null in both for every member from
            // before, who keeps exactly what their role allowed.
            await db.Database.ExecuteSqlRawAsync(@"
                IF COL_LENGTH(N'dbo.OrganizationMembers', N'PermissionPreset') IS NULL
                    ALTER TABLE [OrganizationMembers] ADD [PermissionPreset] nvarchar(40) NULL;
                IF COL_LENGTH(N'dbo.OrganizationMembers', N'Permissions') IS NULL
                    ALTER TABLE [OrganizationMembers] ADD [Permissions] nvarchar(400) NULL;
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
            
            // Fix DealStages table columns (IsWon/IsLost missing from original schema fix)
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DealStages') AND name = 'IsWon')
                    ALTER TABLE [DealStages] ADD [IsWon] bit NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DealStages') AND name = 'IsLost')
                    ALTER TABLE [DealStages] ADD [IsLost] bit NOT NULL DEFAULT 0;
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
                -- Rename CompanyName to BrandName if column exists
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'CompanyName')
                AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
                    EXEC sp_rename 'UserSettings.CompanyName', 'BrandName', 'COLUMN';
                
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
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'InAppNotificationsEnabled')
                    ALTER TABLE [UserSettings] ADD [InAppNotificationsEnabled] bit NOT NULL DEFAULT 1;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'InAppSoundEnabled')
                    ALTER TABLE [UserSettings] ADD [InAppSoundEnabled] bit NOT NULL DEFAULT 1;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrowserNotificationsEnabled')
                    ALTER TABLE [UserSettings] ADD [BrowserNotificationsEnabled] bit NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'WeeklyDigestEnabled')
                    ALTER TABLE [UserSettings] ADD [WeeklyDigestEnabled] bit NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'WeeklyDigestSentAtUtc')
                    ALTER TABLE [UserSettings] ADD [WeeklyDigestSentAtUtc] datetime2 NULL;
                -- The first migration made an EmailDigestFrequency column, NOT NULL with no
                -- default, that the model has not had since. On a database it created, the
                -- first settings save of anyone without settings yet failed on it, the
                -- weekly digest switch included. A default lets the insert through; the
                -- column and anything in it are left alone.
                IF COL_LENGTH('UserSettings', 'EmailDigestFrequency') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.default_constraints
                        WHERE parent_object_id = OBJECT_ID('UserSettings')
                          AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('UserSettings'), 'EmailDigestFrequency', 'ColumnId'))
                    ALTER TABLE [UserSettings] ADD CONSTRAINT [DF_UserSettings_EmailDigestFrequency] DEFAULT 0 FOR [EmailDigestFrequency];
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
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'AgentMemoryJson')
                    ALTER TABLE [UserSettings] ADD [AgentMemoryJson] nvarchar(4000) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'ViewPreferencesJson')
                    ALTER TABLE [UserSettings] ADD [ViewPreferencesJson] nvarchar(4000) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'CreatedAtUtc')
                    ALTER TABLE [UserSettings] ADD [CreatedAtUtc] datetime2 NOT NULL DEFAULT GETUTCDATE();
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'UpdatedAtUtc')
                    ALTER TABLE [UserSettings] ADD [UpdatedAtUtc] datetime2 NOT NULL DEFAULT GETUTCDATE();
            ");

            // Password reset columns on Users
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'PasswordResetTokenHash')
                    ALTER TABLE [Users] ADD [PasswordResetTokenHash] nvarchar(64) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'PasswordResetTokenExpiresAtUtc')
                    ALTER TABLE [Users] ADD [PasswordResetTokenExpiresAtUtc] datetime2 NULL;
            ");

            // Email verification. Accounts that exist when the column arrives were made
            // before verification existed, so they count as verified: nobody already in
            // a workspace is locked out of invitations by this deploy. The UPDATE is in
            // EXEC because the batch is compiled before the column exists.
            await db.Database.ExecuteSqlRawAsync(@"
                IF COL_LENGTH(N'dbo.Users', N'EmailVerifiedAtUtc') IS NULL
                BEGIN
                    ALTER TABLE [Users] ADD [EmailVerifiedAtUtc] datetime2 NULL;
                    EXEC(N'UPDATE [Users] SET [EmailVerifiedAtUtc] = SYSUTCDATETIME()');
                END
                IF COL_LENGTH(N'dbo.Users', N'EmailVerificationTokenHash') IS NULL
                    ALTER TABLE [Users] ADD [EmailVerificationTokenHash] nvarchar(64) NULL;
                IF COL_LENGTH(N'dbo.Users', N'EmailVerificationTokenExpiresAtUtc') IS NULL
                    ALTER TABLE [Users] ADD [EmailVerificationTokenExpiresAtUtc] datetime2 NULL;
            ");

            // Session revocation. Null for everyone at first, which matches the empty
            // stamp that tokens issued before this column existed are read as, so the
            // deploy itself signs nobody out.
            await db.Database.ExecuteSqlRawAsync(@"
                IF COL_LENGTH(N'dbo.Users', N'SecurityStamp') IS NULL
                    ALTER TABLE [Users] ADD [SecurityStamp] nvarchar(64) NULL;
            ");
            
            // Fix Leads table columns
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'OrganizationId')
                BEGIN
                    ALTER TABLE [Leads] ADD [OrganizationId] uniqueidentifier NULL;
                    CREATE INDEX [IX_Leads_OrganizationId] ON [Leads] ([OrganizationId]);
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'LeadSourceId')
                    ALTER TABLE [Leads] ADD [LeadSourceId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'LeadStatusId')
                    ALTER TABLE [Leads] ADD [LeadStatusId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'LeadScore')
                    ALTER TABLE [Leads] ADD [LeadScore] int NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'LastContactedAt')
                    ALTER TABLE [Leads] ADD [LastContactedAt] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'Description')
                    ALTER TABLE [Leads] ADD [Description] nvarchar(2000) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'LifecycleStage')
                    ALTER TABLE [Leads] ADD [LifecycleStage] nvarchar(64) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'IsConverted')
                    ALTER TABLE [Leads] ADD [IsConverted] bit NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'ConvertedAtUtc')
                    ALTER TABLE [Leads] ADD [ConvertedAtUtc] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'UpdatedAtUtc')
                    ALTER TABLE [Leads] ADD [UpdatedAtUtc] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'UpdatedByUserId')
                    ALTER TABLE [Leads] ADD [UpdatedByUserId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'ConvertedToContactId')
                    ALTER TABLE [Leads] ADD [ConvertedToContactId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'ConvertedToDealId')
                    ALTER TABLE [Leads] ADD [ConvertedToDealId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'ConvertedToCompanyId')
                    ALTER TABLE [Leads] ADD [ConvertedToCompanyId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'ReferredByContactId')
                BEGIN
                    ALTER TABLE [Leads] ADD [ReferredByContactId] uniqueidentifier NULL;
                    CREATE INDEX [IX_Leads_ReferredByContactId] ON [Leads] ([ReferredByContactId]);
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Leads_Contacts_ReferredByContactId')
                    ALTER TABLE [Leads] ADD CONSTRAINT [FK_Leads_Contacts_ReferredByContactId]
                        FOREIGN KEY ([ReferredByContactId]) REFERENCES [Contacts] ([Id]);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'AssignedToUserId')
                BEGIN
                    ALTER TABLE [Leads] ADD [AssignedToUserId] uniqueidentifier NULL;
                    CREATE INDEX [IX_Leads_AssignedToUserId] ON [Leads] ([AssignedToUserId]);
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'PipelineState')
                    ALTER TABLE [Leads] ADD [PipelineState] nvarchar(max) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'JobTitle')
                    ALTER TABLE [Leads] ADD [JobTitle] nvarchar(200) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'Country')
                    ALTER TABLE [Leads] ADD [Country] nvarchar(80) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'Industry')
                    ALTER TABLE [Leads] ADD [Industry] nvarchar(120) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'CompanySize')
                    ALTER TABLE [Leads] ADD [CompanySize] nvarchar(40) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'ScoreIsManual')
                    ALTER TABLE [Leads] ADD [ScoreIsManual] bit NOT NULL CONSTRAINT DF_Leads_ScoreIsManual DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'FirstResponseAtUtc')
                    ALTER TABLE [Leads] ADD [FirstResponseAtUtc] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'FirstResponseDueAtUtc')
                    ALTER TABLE [Leads] ADD [FirstResponseDueAtUtc] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'SlaBreached')
                    ALTER TABLE [Leads] ADD [SlaBreached] bit NOT NULL CONSTRAINT DF_Leads_SlaBreached DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'SlaNotifiedAtUtc')
                    ALTER TABLE [Leads] ADD [SlaNotifiedAtUtc] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'QueueId')
                    ALTER TABLE [Leads] ADD [QueueId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'TerritoryId')
                    ALTER TABLE [Leads] ADD [TerritoryId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'EmailOpenCount')
                    ALTER TABLE [Leads] ADD [EmailOpenCount] int NOT NULL CONSTRAINT DF_Leads_EmailOpenCount DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'EnrichedDomain')
                    ALTER TABLE [Leads] ADD [EnrichedDomain] nvarchar(200) NULL;
                -- When someone deleted the lead's person contact, so it is not made again (see Lead).
                IF COL_LENGTH(N'dbo.Leads', N'PersonContactRemovedAtUtc') IS NULL
                    ALTER TABLE [Leads] ADD [PersonContactRemovedAtUtc] datetime2 NULL;
            ");

            // One-time data fixes (see DataFixes). A failure is logged and retried on
            // the next start; it must not stop the schema work after it.
            try
            {
                await db.Database.ExecuteSqlRawAsync(ACI.WebApi.DataFixes.CreateTableSql);
                await db.Database.ExecuteSqlRawAsync(ACI.WebApi.DataFixes.UnfreezeLeadScoresSql);
            }
            catch (Exception fixEx)
            {
                Log.Warning(fixEx, "A one-time data fix failed; it will be retried on the next start");
            }

            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LeadStatusEvents')
                CREATE TABLE [LeadStatusEvents] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [LeadId] uniqueidentifier NOT NULL,
                    [OrganizationId] uniqueidentifier NULL,
                    [FromStatus] nvarchar(64) NULL,
                    [ToStatus] nvarchar(64) NOT NULL,
                    [ChangedByUserId] uniqueidentifier NULL,
                    [ChangedAtUtc] datetime2 NOT NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AuditEvents')
                CREATE TABLE [AuditEvents] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [OwnerUserId] uniqueidentifier NOT NULL,
                    [EntityType] nvarchar(32) NOT NULL,
                    [EntityId] uniqueidentifier NULL,
                    [EntityLabel] nvarchar(200) NULL,
                    [Action] nvarchar(32) NOT NULL,
                    [Summary] nvarchar(500) NOT NULL,
                    [ActorUserId] uniqueidentifier NULL,
                    [ActorName] nvarchar(200) NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Attachments')
                CREATE TABLE [Attachments] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [OwnerUserId] uniqueidentifier NOT NULL,
                    [EntityType] nvarchar(16) NOT NULL,
                    [RecordId] uniqueidentifier NOT NULL,
                    [FileName] nvarchar(255) NOT NULL,
                    [ContentType] nvarchar(127) NOT NULL,
                    [Size] bigint NOT NULL,
                    [Content] varbinary(max) NOT NULL,
                    [UploadedByName] nvarchar(200) NOT NULL,
                    [UploadedAtUtc] datetime2 NOT NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CustomFieldDefinitions')
                CREATE TABLE [CustomFieldDefinitions] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [OwnerUserId] uniqueidentifier NOT NULL,
                    [EntityType] nvarchar(16) NOT NULL,
                    [Key] nvarchar(64) NOT NULL,
                    [Label] nvarchar(120) NOT NULL,
                    [Type] nvarchar(16) NOT NULL,
                    [OptionsJson] nvarchar(max) NULL,
                    [Required] bit NOT NULL,
                    [Order] int NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CustomFieldValues')
                CREATE TABLE [CustomFieldValues] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [OwnerUserId] uniqueidentifier NOT NULL,
                    [EntityType] nvarchar(16) NOT NULL,
                    [RecordId] uniqueidentifier NOT NULL,
                    [ValuesJson] nvarchar(max) NOT NULL,
                    [UpdatedAtUtc] datetime2 NOT NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ForecastSnapshots')
                CREATE TABLE [ForecastSnapshots] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [SavedByUserId] uniqueidentifier NOT NULL,
                    [MonthUtc] datetime2 NOT NULL,
                    [Currency] nvarchar(8) NOT NULL,
                    [AssigneeId] uniqueidentifier NULL,
                    [AssigneeName] nvarchar(200) NOT NULL,
                    [WeightedValue] decimal(18,2) NOT NULL,
                    [OpenValue] decimal(18,2) NOT NULL,
                    [Covered] int NOT NULL,
                    [Missing] int NOT NULL,
                    [SavedAtUtc] datetime2 NOT NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SentMessages')
                CREATE TABLE [SentMessages] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [SentByUserId] uniqueidentifier NOT NULL,
                    [To] nvarchar(2000) NOT NULL,
                    [Cc] nvarchar(2000) NULL,
                    [Bcc] nvarchar(2000) NULL,
                    [Subject] nvarchar(300) NOT NULL,
                    [Body] nvarchar(max) NOT NULL,
                    [LeadId] uniqueidentifier NULL,
                    [ContactId] uniqueidentifier NULL,
                    [DealId] uniqueidentifier NULL,
                    [SentAtUtc] datetime2 NOT NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AutomationRules')
                CREATE TABLE [AutomationRules] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [OwnerUserId] uniqueidentifier NOT NULL,
                    [Name] nvarchar(200) NOT NULL,
                    [Enabled] bit NOT NULL,
                    [Trigger] nvarchar(40) NOT NULL,
                    [TriggerConfigJson] nvarchar(max) NULL,
                    [ActionsJson] nvarchar(max) NOT NULL,
                    [RunCount] int NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AutomationFires')
                CREATE TABLE [AutomationFires] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [RuleId] uniqueidentifier NOT NULL,
                    [SourceKey] nvarchar(80) NOT NULL,
                    [FiredAtUtc] datetime2 NOT NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ClubMembers')
                CREATE TABLE [ClubMembers] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [OwnerUserId] uniqueidentifier NOT NULL,
                    [FirstName] nvarchar(100) NOT NULL,
                    [LastName] nvarchar(100) NOT NULL,
                    [Email] nvarchar(256) NOT NULL,
                    [Phone] nvarchar(64) NULL,
                    [Tier] nvarchar(32) NOT NULL,
                    [Status] nvarchar(32) NOT NULL,
                    [JoinedAtUtc] datetime2 NOT NULL,
                    [RenewsAtUtc] datetime2 NOT NULL,
                    [DuesAmount] decimal(18,2) NOT NULL,
                    [DuesFrequency] nvarchar(32) NOT NULL,
                    [HouseAccountBalance] decimal(18,2) NOT NULL,
                    [Notes] nvarchar(max) NULL,
                    [ManualBalance] decimal(18,2) NOT NULL CONSTRAINT DF_ClubMembers_ManualBalance DEFAULT 0
                );
                -- Added after the table shipped: the opening or hand-corrected part of a
                -- member's house balance, which recomputing from charges used to wipe
                -- (see ClubMember.ManualBalance). DataFixes fills it for existing members.
                IF COL_LENGTH(N'dbo.ClubMembers', N'ManualBalance') IS NULL
                    ALTER TABLE [ClubMembers] ADD [ManualBalance] decimal(18,2) NOT NULL CONSTRAINT DF_ClubMembers_ManualBalance DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ClubVisits')
                CREATE TABLE [ClubVisits] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [MemberId] uniqueidentifier NOT NULL,
                    [MemberName] nvarchar(200) NOT NULL,
                    [MemberTier] nvarchar(32) NOT NULL,
                    [Venue] nvarchar(32) NOT NULL,
                    [VisitedAtUtc] datetime2 NOT NULL,
                    [GuestCount] int NOT NULL,
                    [PointsAwarded] int NOT NULL,
                    [Notes] nvarchar(max) NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LoyaltyLedger')
                CREATE TABLE [LoyaltyLedger] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [MemberId] uniqueidentifier NOT NULL,
                    [MemberName] nvarchar(200) NOT NULL,
                    [Kind] nvarchar(32) NOT NULL,
                    [Reason] nvarchar(40) NOT NULL,
                    [Points] int NOT NULL,
                    [Note] nvarchar(max) NULL,
                    [CreatedAtUtc] datetime2 NOT NULL
                );
                IF COL_LENGTH('Deals', 'IsPrivate') IS NULL ALTER TABLE [Deals] ADD [IsPrivate] bit NOT NULL CONSTRAINT DF_Deals_IsPrivate DEFAULT 0;
                IF COL_LENGTH('Deals', 'TeamId') IS NULL ALTER TABLE [Deals] ADD [TeamId] uniqueidentifier NULL;
                IF COL_LENGTH('Deals', 'Amount') IS NULL ALTER TABLE [Deals] ADD [Amount] decimal(18,2) NULL;
                IF COL_LENGTH('TaskItems', 'Recurrence') IS NULL ALTER TABLE [TaskItems] ADD [Recurrence] nvarchar(16) NULL;
                IF COL_LENGTH('Organizations', 'OutboundWebhookUrl') IS NULL ALTER TABLE [Organizations] ADD [OutboundWebhookUrl] nvarchar(500) NULL;
                IF COL_LENGTH('Organizations', 'OutboundWebhookSecret') IS NULL ALTER TABLE [Organizations] ADD [OutboundWebhookSecret] nvarchar(200) NULL;
                -- The workspace's own sender address, used only once its domain is verified (see Organization.SendingAddress).
                IF COL_LENGTH('Organizations', 'SendingAddress') IS NULL ALTER TABLE [Organizations] ADD [SendingAddress] nvarchar(256) NULL;
                -- What a workspace works in (see RegionalSettings). The defaults, a Swiss
                -- club's, fill the organisations already there; until now their months
                -- began at UTC midnight and a deal with no currency counted as dollars.
                IF COL_LENGTH('Organizations', 'Currency') IS NULL ALTER TABLE [Organizations] ADD [Currency] nvarchar(3) NOT NULL CONSTRAINT DF_Organizations_Currency DEFAULT N'CHF';
                IF COL_LENGTH('Organizations', 'Locale') IS NULL ALTER TABLE [Organizations] ADD [Locale] nvarchar(35) NOT NULL CONSTRAINT DF_Organizations_Locale DEFAULT N'de-CH';
                IF COL_LENGTH('Organizations', 'TimeZone') IS NULL ALTER TABLE [Organizations] ADD [TimeZone] nvarchar(64) NOT NULL CONSTRAINT DF_Organizations_TimeZone DEFAULT N'Europe/Zurich';
                -- How the board judges a deal: after how many days without activity an
                -- open deal is stale (the workspace's, which the default fills in for the
                -- organisations already there, unless its pipeline has its own), and the
                -- probability a stage gives the deals in it that have none of their own.
                IF COL_LENGTH('Organizations', 'StaleDealDays') IS NULL ALTER TABLE [Organizations] ADD [StaleDealDays] int NOT NULL CONSTRAINT DF_Organizations_StaleDealDays DEFAULT 30;
                IF COL_LENGTH('Pipelines', 'StaleDealDays') IS NULL ALTER TABLE [Pipelines] ADD [StaleDealDays] int NULL;
                IF COL_LENGTH('DealStages', 'Probability') IS NULL ALTER TABLE [DealStages] ADD [Probability] int NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SalesTeams')
                CREATE TABLE [SalesTeams] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NOT NULL,
                    [Name] nvarchar(120) NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SalesTeamMembers')
                CREATE TABLE [SalesTeamMembers] (
                    [TeamId] uniqueidentifier NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    CONSTRAINT PK_SalesTeamMembers PRIMARY KEY ([TeamId], [UserId])
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ClubDocuments')
                CREATE TABLE [ClubDocuments] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [Kind] nvarchar(40) NOT NULL,
                    [Payload] nvarchar(max) NOT NULL,
                    [UpdatedAtUtc] datetime2 NOT NULL,
                    [Version] uniqueidentifier NOT NULL CONSTRAINT DF_ClubDocuments_Version DEFAULT NEWID()
                );
                -- Added after the table shipped. Without it two redeems of one gift card
                -- both spent its balance, and two closes of one tab both posted a charge
                -- (see ClubDocument.Version). Existing rows take a fresh value.
                IF COL_LENGTH(N'dbo.ClubDocuments', N'Version') IS NULL
                    ALTER TABLE [ClubDocuments] ADD [Version] uniqueidentifier NOT NULL CONSTRAINT DF_ClubDocuments_Version DEFAULT NEWID();
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Products')
                CREATE TABLE [Products] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [OwnerUserId] uniqueidentifier NOT NULL,
                    [Name] nvarchar(200) NOT NULL,
                    [Sku] nvarchar(64) NULL,
                    [UnitPrice] decimal(18,4) NOT NULL,
                    [Currency] nvarchar(8) NULL,
                    [CreatedAtUtc] datetime2 NOT NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DealLineItems')
                CREATE TABLE [DealLineItems] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [DealId] uniqueidentifier NOT NULL,
                    [ProductId] nvarchar(64) NULL,
                    [Name] nvarchar(200) NOT NULL,
                    [Quantity] decimal(18,4) NOT NULL,
                    [UnitPrice] decimal(18,4) NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LeadScoringRules')
                CREATE TABLE [LeadScoringRules] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [Field] nvarchar(40) NOT NULL,
                    [MatchValue] nvarchar(120) NULL,
                    [Points] int NOT NULL,
                    [IsActive] bit NOT NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LeadAssignmentRules')
                CREATE TABLE [LeadAssignmentRules] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [Name] nvarchar(120) NOT NULL,
                    [Kind] nvarchar(32) NOT NULL,
                    [Priority] int NOT NULL,
                    [SourceMatch] nvarchar(80) NULL,
                    [TerritoryId] uniqueidentifier NULL,
                    [QueueId] uniqueidentifier NULL,
                    [MemberUserIds] nvarchar(2000) NULL,
                    [LastIndex] int NOT NULL,
                    [IsActive] bit NOT NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LeadTerritories')
                CREATE TABLE [LeadTerritories] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [Name] nvarchar(120) NOT NULL,
                    [Countries] nvarchar(500) NULL,
                    [Industries] nvarchar(500) NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LeadQueues')
                CREATE TABLE [LeadQueues] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [Name] nvarchar(120) NOT NULL,
                    [MemberUserIds] nvarchar(2000) NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LeadCaptureForms')
                CREATE TABLE [LeadCaptureForms] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [CreatedByUserId] uniqueidentifier NOT NULL,
                    [Name] nvarchar(120) NOT NULL,
                    [PublicToken] nvarchar(64) NOT NULL,
                    [IsActive] bit NOT NULL,
                    [SuccessMessage] nvarchar(300) NULL,
                    [CreatedAtUtc] datetime2 NOT NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LeadOpsSettings')
                CREATE TABLE [LeadOpsSettings] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [SlaHours] int NOT NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OutreachSequences')
                CREATE TABLE [OutreachSequences] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [UserId] uniqueidentifier NOT NULL,
                    [OrganizationId] uniqueidentifier NULL,
                    [Name] nvarchar(160) NOT NULL,
                    [Description] nvarchar(500) NULL,
                    [Status] nvarchar(20) NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OutreachSteps')
                CREATE TABLE [OutreachSteps] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [SequenceId] uniqueidentifier NOT NULL,
                    [SortOrder] int NOT NULL,
                    [Type] nvarchar(20) NOT NULL,
                    [DayOffset] int NOT NULL,
                    [Subject] nvarchar(200) NULL,
                    [Body] nvarchar(4000) NULL,
                    [TaskTitle] nvarchar(200) NULL,
                    [Note] nvarchar(1000) NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OutreachEnrollments')
                CREATE TABLE [OutreachEnrollments] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [SequenceId] uniqueidentifier NOT NULL,
                    [OrganizationId] uniqueidentifier NULL,
                    [EnrolledByUserId] uniqueidentifier NOT NULL,
                    [TargetType] nvarchar(20) NOT NULL,
                    [TargetId] uniqueidentifier NOT NULL,
                    [TargetName] nvarchar(200) NOT NULL,
                    [TargetEmail] nvarchar(256) NULL,
                    [Status] nvarchar(20) NOT NULL,
                    [CurrentStep] int NOT NULL,
                    [EnrolledAtUtc] datetime2 NOT NULL,
                    [NextActionAtUtc] datetime2 NULL
                );
            ");

            // Saved forecasts are kept rather than replaced, each save's lines sharing a
            // SnapshotId. Before this, a month held one save at most (saving again deleted
            // the last), so the lines already there are grouped by when and by whom they
            // were saved, inside the same transaction that adds the column. The default
            // covers a row written by an instance still running the previous code during a
            // deploy. ByCloseDate marks lines counted by close date; the ones already there
            // counted every open deal, and get 0.
            await db.Database.ExecuteSqlRawAsync(@"
                IF COL_LENGTH('ForecastSnapshots', 'SnapshotId') IS NULL
                BEGIN
                BEGIN TRY
                    BEGIN TRANSACTION;
                    ALTER TABLE [ForecastSnapshots] ADD [SnapshotId] uniqueidentifier NULL;
                    EXEC(N'
                        DECLARE @saves TABLE ([OrganizationId] uniqueidentifier NULL, [SavedByUserId] uniqueidentifier NOT NULL,
                            [MonthUtc] datetime2 NOT NULL, [SavedAtUtc] datetime2 NOT NULL, [SnapshotId] uniqueidentifier NOT NULL);
                        INSERT INTO @saves SELECT [OrganizationId], [SavedByUserId], [MonthUtc], [SavedAtUtc], NEWID()
                            FROM [ForecastSnapshots] GROUP BY [OrganizationId], [SavedByUserId], [MonthUtc], [SavedAtUtc];
                        UPDATE f SET [SnapshotId] = s.[SnapshotId]
                            FROM [ForecastSnapshots] f JOIN @saves s
                              ON (f.[OrganizationId] = s.[OrganizationId] OR (f.[OrganizationId] IS NULL AND s.[OrganizationId] IS NULL))
                             AND f.[SavedByUserId] = s.[SavedByUserId] AND f.[MonthUtc] = s.[MonthUtc] AND f.[SavedAtUtc] = s.[SavedAtUtc];
                        ALTER TABLE [ForecastSnapshots] ALTER COLUMN [SnapshotId] uniqueidentifier NOT NULL;
                        ALTER TABLE [ForecastSnapshots] ADD CONSTRAINT [DF_ForecastSnapshots_SnapshotId] DEFAULT NEWID() FOR [SnapshotId];');
                    COMMIT TRANSACTION;
                END TRY
                BEGIN CATCH
                    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
                    THROW;
                END CATCH;
                END
                IF COL_LENGTH('ForecastSnapshots', 'ByCloseDate') IS NULL
                    ALTER TABLE [ForecastSnapshots] ADD [ByCloseDate] bit NOT NULL CONSTRAINT [DF_ForecastSnapshots_ByCloseDate] DEFAULT 0;
                IF COL_LENGTH('ForecastSnapshots', 'OverdueCount') IS NULL
                    ALTER TABLE [ForecastSnapshots] ADD [OverdueCount] int NOT NULL CONSTRAINT [DF_ForecastSnapshots_OverdueCount] DEFAULT 0;
                IF COL_LENGTH('ForecastSnapshots', 'OverdueValue') IS NULL
                    ALTER TABLE [ForecastSnapshots] ADD [OverdueValue] decimal(18,2) NOT NULL CONSTRAINT [DF_ForecastSnapshots_OverdueValue] DEFAULT 0;
                IF COL_LENGTH('ForecastSnapshots', 'NoCloseDateCount') IS NULL
                    ALTER TABLE [ForecastSnapshots] ADD [NoCloseDateCount] int NOT NULL CONSTRAINT [DF_ForecastSnapshots_NoCloseDateCount] DEFAULT 0;
                IF COL_LENGTH('ForecastSnapshots', 'UnreadableCount') IS NULL
                    ALTER TABLE [ForecastSnapshots] ADD [UnreadableCount] int NOT NULL CONSTRAINT [DF_ForecastSnapshots_UnreadableCount] DEFAULT 0;
            ");

            // Needs ClubMembers.ManualBalance, added just above (see DataFixes). Like the
            // fixes before it, a failure is logged and retried on the next start.
            try
            {
                await db.Database.ExecuteSqlRawAsync(ACI.WebApi.DataFixes.KeepClubOpeningBalancesSql);
            }
            catch (Exception fixEx)
            {
                Log.Warning(fixEx, "A one-time data fix failed; it will be retried on the next start");
            }
            // Needs Deals.Amount, added just above. On its own, so a failure of the club
            // fix does not hold it back.
            try
            {
                await ACI.WebApi.DataFixes.FillDealAmountsAsync(db);
            }
            catch (Exception fixEx)
            {
                Log.Warning(fixEx, "A one-time data fix failed; it will be retried on the next start");
            }
            // Needs Organizations.Currency, added above, for the currency each deal gets.
            try
            {
                await db.Database.ExecuteSqlRawAsync(ACI.WebApi.DataFixes.FillDealCurrenciesSql);
            }
            catch (Exception fixEx)
            {
                Log.Warning(fixEx, "A one-time data fix failed; it will be retried on the next start");
            }

            // The indexes the model declares for the tables above. The DDL created the
            // tables without them, so the follow-up runner — once a minute, on every
            // instance — scanned OutreachEnrollments whole for due rows, and every
            // public form submission scanned LeadCaptureForms for its token. Each one
            // is guarded and caught on its own: this whole routine shares a single
            // catch, and one failure would otherwise skip every statement after it.
            await db.Database.ExecuteSqlRawAsync(@"
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OutreachEnrollments_Status_NextActionAtUtc')
                        CREATE INDEX [IX_OutreachEnrollments_Status_NextActionAtUtc] ON [OutreachEnrollments] ([Status], [NextActionAtUtc]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LeadCaptureForms_PublicToken')
                        CREATE UNIQUE INDEX [IX_LeadCaptureForms_PublicToken] ON [LeadCaptureForms] ([PublicToken]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LeadStatusEvents_LeadId')
                        CREATE INDEX [IX_LeadStatusEvents_LeadId] ON [LeadStatusEvents] ([LeadId]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditEvents_OrganizationId_CreatedAtUtc')
                        CREATE INDEX [IX_AuditEvents_OrganizationId_CreatedAtUtc] ON [AuditEvents] ([OrganizationId], [CreatedAtUtc]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditEvents_EntityType_EntityId')
                        CREATE INDEX [IX_AuditEvents_EntityType_EntityId] ON [AuditEvents] ([EntityType], [EntityId]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Attachments_EntityType_RecordId')
                        CREATE INDEX [IX_Attachments_EntityType_RecordId] ON [Attachments] ([EntityType], [RecordId]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CustomFieldDefinitions_OrganizationId_EntityType')
                        CREATE INDEX [IX_CustomFieldDefinitions_OrganizationId_EntityType] ON [CustomFieldDefinitions] ([OrganizationId], [EntityType]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CustomFieldValues_EntityType_RecordId')
                        CREATE UNIQUE INDEX [IX_CustomFieldValues_EntityType_RecordId] ON [CustomFieldValues] ([EntityType], [RecordId]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DealLineItems_DealId')
                        CREATE INDEX [IX_DealLineItems_DealId] ON [DealLineItems] ([DealId]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_OrganizationId')
                        CREATE INDEX [IX_Products_OrganizationId] ON [Products] ([OrganizationId]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ForecastSnapshots_OrganizationId_MonthUtc')
                        CREATE INDEX [IX_ForecastSnapshots_OrganizationId_MonthUtc] ON [ForecastSnapshots] ([OrganizationId], [MonthUtc]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SentMessages_OrganizationId_SentAtUtc')
                        CREATE INDEX [IX_SentMessages_OrganizationId_SentAtUtc] ON [SentMessages] ([OrganizationId], [SentAtUtc]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AutomationRules_OrganizationId_Trigger')
                        CREATE INDEX [IX_AutomationRules_OrganizationId_Trigger] ON [AutomationRules] ([OrganizationId], [Trigger]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AutomationFires_RuleId_SourceKey')
                        CREATE UNIQUE INDEX [IX_AutomationFires_RuleId_SourceKey] ON [AutomationFires] ([RuleId], [SourceKey]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ClubMembers_OrganizationId')
                        CREATE INDEX [IX_ClubMembers_OrganizationId] ON [ClubMembers] ([OrganizationId]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ClubVisits_MemberId')
                        CREATE INDEX [IX_ClubVisits_MemberId] ON [ClubVisits] ([MemberId]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LoyaltyLedger_MemberId')
                        CREATE INDEX [IX_LoyaltyLedger_MemberId] ON [LoyaltyLedger] ([MemberId]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ClubDocuments_OrganizationId_Kind')
                        CREATE INDEX [IX_ClubDocuments_OrganizationId_Kind] ON [ClubDocuments] ([OrganizationId], [Kind]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SalesTeams_OrganizationId')
                        CREATE INDEX [IX_SalesTeams_OrganizationId] ON [SalesTeams] ([OrganizationId]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LeadOpsSettings_OrganizationId')
                        CREATE INDEX [IX_LeadOpsSettings_OrganizationId] ON [LeadOpsSettings] ([OrganizationId]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OutreachSteps_SequenceId')
                        CREATE INDEX [IX_OutreachSteps_SequenceId] ON [OutreachSteps] ([SequenceId]);
                END TRY BEGIN CATCH END CATCH;

                -- Scoring rules saved with the retired 'qualified' status match nobody:
                -- no current status contains it. Point them at the agreed stage, which
                -- also counts every stage after it. Idempotent: a second run finds none.
                BEGIN TRY
                    UPDATE [LeadScoringRules]
                    SET [Field] = 'stageAtLeast', [MatchValue] = 'Contract Pending'
                    WHERE LOWER([Field]) = 'status' AND LOWER(LTRIM(RTRIM([MatchValue]))) = 'qualified';
                END TRY BEGIN CATCH END CATCH;
            ");

            // Contracts and their audit trail.
            //
            // Raw idempotent DDL rather than an EF migration, matching how every
            // other recent schema change in this file shipped — including
            // Leads.PipelineState directly above, which exists in no migration and
            // in no snapshot. Scaffolding a migration against that snapshot would
            // try to add PipelineState a second time. See ContractConfiguration.cs
            // for the model side; the two are kept deliberately in step.
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Contracts')
                BEGIN
                    CREATE TABLE [Contracts] (
                        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                        [OrganizationId] uniqueidentifier NOT NULL,
                        [LeadId] uniqueidentifier NULL,
                        [DealId] uniqueidentifier NULL,
                        [Status] nvarchar(32) NOT NULL,
                        [Title] nvarchar(300) NOT NULL,
                        [Body] nvarchar(max) NOT NULL,
                        [BodyHashAtSend] nvarchar(64) NULL,
                        [CounterpartyName] nvarchar(300) NOT NULL,
                        [CounterpartyEmail] nvarchar(320) NOT NULL,
                        [CreatedByUserId] uniqueidentifier NOT NULL,
                        [CreatedAtUtc] datetime2 NOT NULL,
                        [UpdatedAtUtc] datetime2 NOT NULL,
                        [SentAtUtc] datetime2 NULL,
                        [ReminderSentAtUtc] datetime2 NULL,
                        [SentByUserId] uniqueidentifier NULL,
                        [SigningTokenHash] nvarchar(64) NULL,
                        [SigningTokenExpiresAtUtc] datetime2 NULL,
                        [FirstViewedAtUtc] datetime2 NULL,
                        [ClientSignatureName] nvarchar(300) NULL,
                        [ClientSignedAtUtc] datetime2 NULL,
                        [ClientSignatureIp] nvarchar(64) NULL,
                        [ClientSignatureUserAgent] nvarchar(512) NULL,
                        [CounterSignatureName] nvarchar(300) NULL,
                        [CounterSignedAtUtc] datetime2 NULL,
                        [CounterSignedByUserId] uniqueidentifier NULL,
                        [CounterSignatureIp] nvarchar(64) NULL,
                        [ClosedReason] nvarchar(1000) NULL,
                        [ExecutedCopySentAtUtc] datetime2 NULL,
                        [RowVersion] rowversion NULL
                    );
                    CREATE INDEX [IX_Contracts_OrganizationId_LeadId]
                        ON [Contracts] ([OrganizationId], [LeadId]);
                    -- The public signing path looks up by this alone, so it has to be
                    -- indexed. Filtered unique: two live contracts must never share a
                    -- token, but the many NULLs (drafts, voided) have to coexist.
                    CREATE UNIQUE INDEX [IX_Contracts_SigningTokenHash]
                        ON [Contracts] ([SigningTokenHash])
                        WHERE [SigningTokenHash] IS NOT NULL;
                END;
                -- Added after the table first shipped. It lives here, after the CREATE, because
                -- on a new database the block above runs first; altering Contracts any earlier
                -- failed there and, sharing one catch, stopped the rest of the schema work.
                IF COL_LENGTH('Contracts', 'ReminderSentAtUtc') IS NULL
                    ALTER TABLE [Contracts] ADD [ReminderSentAtUtc] datetime2 NULL;

                -- The model declares these non-nullable and the service always writes
                -- at least "", but the original DDL allowed NULL — so EF would
                -- materialise a null into a non-nullable string and the next
                -- .Trim() on it would throw. Exactly the shape of the crash the
                -- Leads page had. Backfilled first so the ALTER cannot fail, and
                -- both statements are no-ops on a database already correct.
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'dbo.Contracts')
                      AND name IN (N'CounterpartyName', N'CounterpartyEmail')
                      AND is_nullable = 1)
                BEGIN
                    UPDATE [Contracts] SET [CounterpartyName] = '' WHERE [CounterpartyName] IS NULL;
                    UPDATE [Contracts] SET [CounterpartyEmail] = '' WHERE [CounterpartyEmail] IS NULL;
                    ALTER TABLE [Contracts] ALTER COLUMN [CounterpartyName] nvarchar(300) NOT NULL;
                    ALTER TABLE [Contracts] ALTER COLUMN [CounterpartyEmail] nvarchar(320) NOT NULL;
                END;

                -- Added after the table shipped. What makes a contract state change
                -- atomic instead of advisory: without it two concurrent signs both
                -- passed the state machine and both wrote, and a sign racing a void
                -- wrote the killed signing token back. SQL Server populates and
                -- maintains a rowversion itself, so existing rows need no backfill.
                IF COL_LENGTH(N'dbo.Contracts', N'RowVersion') IS NULL
                BEGIN
                    ALTER TABLE [Contracts] ADD [RowVersion] rowversion NULL;
                END;

                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ContractEvents')
                BEGIN
                    CREATE TABLE [ContractEvents] (
                        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                        [ContractId] uniqueidentifier NOT NULL,
                        [Type] nvarchar(32) NOT NULL,
                        [Detail] nvarchar(1000) NULL,
                        [ActorUserId] uniqueidentifier NULL,
                        [ActorLabel] nvarchar(300) NULL,
                        [Ip] nvarchar(64) NULL,
                        [UserAgent] nvarchar(512) NULL,
                        [AtUtc] datetime2 NOT NULL,
                        CONSTRAINT [FK_ContractEvents_Contracts] FOREIGN KEY ([ContractId])
                            REFERENCES [Contracts] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_ContractEvents_ContractId_AtUtc]
                        ON [ContractEvents] ([ContractId], [AtUtc]);
                END;
            ");
            
            // Fix Deals table columns
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Deals') AND name = 'OrganizationId')
                BEGIN
                    ALTER TABLE [Deals] ADD [OrganizationId] uniqueidentifier NULL;
                    CREATE INDEX [IX_Deals_OrganizationId] ON [Deals] ([OrganizationId]);
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Deals') AND name = 'Currency')
                    ALTER TABLE [Deals] ADD [Currency] nvarchar(8) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Deals') AND name = 'PipelineId')
                    ALTER TABLE [Deals] ADD [PipelineId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Deals') AND name = 'DealStageId')
                    ALTER TABLE [Deals] ADD [DealStageId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Deals') AND name = 'AssigneeId')
                    ALTER TABLE [Deals] ADD [AssigneeId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Deals') AND name = 'UpdatedAtUtc')
                    ALTER TABLE [Deals] ADD [UpdatedAtUtc] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Deals') AND name = 'UpdatedByUserId')
                    ALTER TABLE [Deals] ADD [UpdatedByUserId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Deals') AND name = 'IsWon')
                    ALTER TABLE [Deals] ADD [IsWon] bit NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Deals') AND name = 'Stage')
                    ALTER TABLE [Deals] ADD [Stage] nvarchar(128) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Deals') AND name = 'ContactId')
                    ALTER TABLE [Deals] ADD [ContactId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Deals') AND name = 'ExpectedCloseDateUtc')
                    ALTER TABLE [Deals] ADD [ExpectedCloseDateUtc] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Deals') AND name = 'Description')
                    ALTER TABLE [Deals] ADD [Description] nvarchar(2000) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Deals') AND name = 'Probability')
                    ALTER TABLE [Deals] ADD [Probability] int NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Deals') AND name = 'ClosedAtUtc')
                    ALTER TABLE [Deals] ADD [ClosedAtUtc] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Deals') AND name = 'ClosedReason')
                    ALTER TABLE [Deals] ADD [ClosedReason] nvarchar(500) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Deals') AND name = 'ClosedReasonCategory')
                    ALTER TABLE [Deals] ADD [ClosedReasonCategory] nvarchar(50) NULL;
            ");
            
            // Fix Companies table columns
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Companies') AND name = 'OrganizationId')
                BEGIN
                    ALTER TABLE [Companies] ADD [OrganizationId] uniqueidentifier NULL;
                    CREATE INDEX [IX_Companies_OrganizationId] ON [Companies] ([OrganizationId]);
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Companies') AND name = 'Domain')
                    ALTER TABLE [Companies] ADD [Domain] nvarchar(256) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Companies') AND name = 'Industry')
                    ALTER TABLE [Companies] ADD [Industry] nvarchar(128) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Companies') AND name = 'Size')
                    ALTER TABLE [Companies] ADD [Size] nvarchar(64) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Companies') AND name = 'UpdatedAtUtc')
                    ALTER TABLE [Companies] ADD [UpdatedAtUtc] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Companies') AND name = 'UpdatedByUserId')
                    ALTER TABLE [Companies] ADD [UpdatedByUserId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Companies') AND name = 'Description')
                    ALTER TABLE [Companies] ADD [Description] nvarchar(2000) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Companies') AND name = 'Location')
                    ALTER TABLE [Companies] ADD [Location] nvarchar(300) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Companies') AND name = 'Website')
                    ALTER TABLE [Companies] ADD [Website] nvarchar(500) NULL;
            ");
            
            // Fix Activities table columns
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'OrganizationId')
                BEGIN
                    ALTER TABLE [Activities] ADD [OrganizationId] uniqueidentifier NULL;
                    CREATE INDEX [IX_Activities_OrganizationId] ON [Activities] ([OrganizationId]);
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'LeadId')
                    ALTER TABLE [Activities] ADD [LeadId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'Participants')
                    ALTER TABLE [Activities] ADD [Participants] nvarchar(1024) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'UpdatedAtUtc')
                    ALTER TABLE [Activities] ADD [UpdatedAtUtc] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'UpdatedByUserId')
                    ALTER TABLE [Activities] ADD [UpdatedByUserId] uniqueidentifier NULL;
                -- How a call or meeting went, how long it took, and who reached out.
                IF COL_LENGTH('Activities', 'Outcome') IS NULL ALTER TABLE [Activities] ADD [Outcome] nvarchar(32) NULL;
                IF COL_LENGTH('Activities', 'DurationMinutes') IS NULL ALTER TABLE [Activities] ADD [DurationMinutes] int NULL;
                IF COL_LENGTH('Activities', 'Direction') IS NULL ALTER TABLE [Activities] ADD [Direction] nvarchar(16) NULL;
            ");

            // When an activity happened, as against when it was logged. The activities
            // already there were logged as they happened, as far as anyone recorded, so they
            // take their logged time, inside the transaction that adds the column. The
            // default covers a row written by an instance still running the previous code
            // during a deploy, which knows nothing of the column.
            // Filling it touches every activity in one transaction, which on the Basic
            // database tier can outlast a command's default 30 seconds. A timeout never
            // reaches the CATCH: the transaction stayed open on its pooled connection,
            // holding the table. So the batch has longer, and XACT_ABORT rolls back
            // whatever stops it.
            var commandTimeout = db.Database.GetCommandTimeout();
            db.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
            try
            {
                await db.Database.ExecuteSqlRawAsync(@"
                SET XACT_ABORT ON;
                IF COL_LENGTH('Activities', 'OccurredAtUtc') IS NULL
                BEGIN
                BEGIN TRY
                    BEGIN TRANSACTION;
                    ALTER TABLE [Activities] ADD [OccurredAtUtc] datetime2 NULL;
                    EXEC(N'
                        UPDATE [Activities] SET [OccurredAtUtc] = [CreatedAtUtc];
                        ALTER TABLE [Activities] ALTER COLUMN [OccurredAtUtc] datetime2 NOT NULL;
                        ALTER TABLE [Activities] ADD CONSTRAINT [DF_Activities_OccurredAtUtc] DEFAULT SYSUTCDATETIME() FOR [OccurredAtUtc];');
                    COMMIT TRANSACTION;
                END TRY
                BEGIN CATCH
                    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
                    THROW;
                END CATCH;
                END
            ");
            }
            finally
            {
                db.Database.SetCommandTimeout(commandTimeout);
            }
            // In a batch of its own, after the column it covers exists.
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Activities_OrganizationId_OccurredAtUtc' AND object_id = OBJECT_ID('Activities'))
                    CREATE INDEX [IX_Activities_OrganizationId_OccurredAtUtc] ON [Activities] ([OrganizationId], [OccurredAtUtc]);
            ");
            
            // Fix TaskItems table columns
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TaskItems') AND name = 'OrganizationId')
                BEGIN
                    ALTER TABLE [TaskItems] ADD [OrganizationId] uniqueidentifier NULL;
                    CREATE INDEX [IX_TaskItems_OrganizationId] ON [TaskItems] ([OrganizationId]);
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TaskItems') AND name = 'Description')
                    ALTER TABLE [TaskItems] ADD [Description] nvarchar(max) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TaskItems') AND name = 'DueDateUtc')
                    ALTER TABLE [TaskItems] ADD [DueDateUtc] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TaskItems') AND name = 'ReminderDateUtc')
                    ALTER TABLE [TaskItems] ADD [ReminderDateUtc] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TaskItems') AND name = 'Status')
                    ALTER TABLE [TaskItems] ADD [Status] nvarchar(32) NOT NULL DEFAULT 'Todo';
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TaskItems') AND name = 'Priority')
                    ALTER TABLE [TaskItems] ADD [Priority] nvarchar(32) NOT NULL DEFAULT 'None';
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TaskItems') AND name = 'Completed')
                    ALTER TABLE [TaskItems] ADD [Completed] bit NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TaskItems') AND name = 'AssigneeId')
                    ALTER TABLE [TaskItems] ADD [AssigneeId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TaskItems') AND name = 'LeadId')
                    ALTER TABLE [TaskItems] ADD [LeadId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TaskItems') AND name = 'DealId')
                    ALTER TABLE [TaskItems] ADD [DealId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TaskItems') AND name = 'ContactId')
                    ALTER TABLE [TaskItems] ADD [ContactId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TaskItems') AND name = 'Notes')
                    ALTER TABLE [TaskItems] ADD [Notes] nvarchar(max) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TaskItems') AND name = 'UpdatedAtUtc')
                    ALTER TABLE [TaskItems] ADD [UpdatedAtUtc] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TaskItems') AND name = 'CompletedAtUtc')
                    ALTER TABLE [TaskItems] ADD [CompletedAtUtc] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TaskItems') AND name = 'UpdatedByUserId')
                    ALTER TABLE [TaskItems] ADD [UpdatedByUserId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TaskItems') AND name = 'ReminderSentAtUtc')
                    ALTER TABLE [TaskItems] ADD [ReminderSentAtUtc] datetime2 NULL;
            ");
            
            // Fix CopyHistoryItems table columns
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CopyHistoryItems') AND name = 'OrganizationId')
                BEGIN
                    ALTER TABLE [CopyHistoryItems] ADD [OrganizationId] uniqueidentifier NULL;
                    CREATE INDEX [IX_CopyHistoryItems_OrganizationId] ON [CopyHistoryItems] ([OrganizationId]);
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CopyHistoryItems') AND name = 'Subject')
                    ALTER TABLE [CopyHistoryItems] ADD [Subject] nvarchar(max) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CopyHistoryItems') AND name = 'RecipientEmail')
                    ALTER TABLE [CopyHistoryItems] ADD [RecipientEmail] nvarchar(max) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CopyHistoryItems') AND name = 'CopyTypeId')
                    ALTER TABLE [CopyHistoryItems] ADD [CopyTypeId] int NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CopyHistoryItems') AND name = 'Goal')
                    ALTER TABLE [CopyHistoryItems] ADD [Goal] nvarchar(max) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CopyHistoryItems') AND name = 'BrandTone')
                    ALTER TABLE [CopyHistoryItems] ADD [BrandTone] nvarchar(max) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CopyHistoryItems') AND name = 'IsRewritten')
                    ALTER TABLE [CopyHistoryItems] ADD [IsRewritten] bit NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CopyHistoryItems') AND name = 'CopyCount')
                    ALTER TABLE [CopyHistoryItems] ADD [CopyCount] int NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CopyHistoryItems') AND name = 'SendCount')
                    ALTER TABLE [CopyHistoryItems] ADD [SendCount] int NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CopyHistoryItems') AND name = 'ResponseCount')
                    ALTER TABLE [CopyHistoryItems] ADD [ResponseCount] int NOT NULL DEFAULT 0;
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
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'ConvertedAtUtc')
                    ALTER TABLE [Contacts] ADD [ConvertedAtUtc] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'JobTitle')
                    ALTER TABLE [Contacts] ADD [JobTitle] nvarchar(128) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'PreferredContactMethod')
                    ALTER TABLE [Contacts] ADD [PreferredContactMethod] nvarchar(32) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'UpdatedAtUtc')
                    ALTER TABLE [Contacts] ADD [UpdatedAtUtc] datetime2 NULL;
                -- Fix: ensure UpdatedAtUtc is nullable (was previously added as NOT NULL in error)
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'UpdatedAtUtc' AND is_nullable = 0)
                    ALTER TABLE [Contacts] ALTER COLUMN [UpdatedAtUtc] datetime2 NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'UpdatedByUserId')
                    ALTER TABLE [Contacts] ADD [UpdatedByUserId] uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'Description')
                    ALTER TABLE [Contacts] ADD [Description] nvarchar(max) NULL;
            ");

            // Contact emails are unique per workspace: per organization, and with none,
            // per user (see ContactEmailIndexes). An index the existing rows do not meet
            // is left out rather than failing, and in its own catch, so it cannot stop
            // the schema work after it; the warning says which data needs merging.
            try
            {
                await db.Database.ExecuteSqlRawAsync(ACI.WebApi.ContactEmailIndexes.Sql);
                var emailIndexes = await db.Database.SqlQueryRaw<int>(ACI.WebApi.ContactEmailIndexes.CountSql).SingleAsync();
                if (emailIndexes < 2)
                    Log.Warning("Some active contacts in one workspace share an email, so the unique index on contact "
                        + "emails ({OrganizationIndex}, {PersonalIndex}) is not in place; merge the duplicates and restart",
                        ACI.WebApi.ContactEmailIndexes.OrganizationIndex, ACI.WebApi.ContactEmailIndexes.PersonalIndex);
            }
            catch (Exception indexEx)
            {
                Log.Warning(indexEx, "Could not put the per-workspace contact email indexes in place; it will be retried on the next start");
            }

            // Imports (see ImportService). Each run is a row in ImportBatches, and the
            // leads, contacts and companies it creates carry its id so it can be undone.
            // Here, after the blocks above that bring those three tables up to date, so an
            // existing database gets the columns and a new one gets them after the
            // migrations made the tables. The indexes are guarded and caught one by one,
            // as above: one failure must not skip the schema work after it.
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ImportBatches')
                CREATE TABLE [ImportBatches] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [EntityType] nvarchar(16) NOT NULL,
                    [FileName] nvarchar(260) NOT NULL,
                    [DuplicateMode] nvarchar(16) NOT NULL,
                    [RanAutomations] bit NOT NULL,
                    [TotalRows] int NOT NULL,
                    [CreatedCount] int NOT NULL,
                    [UpdatedCount] int NOT NULL,
                    [SkippedCount] int NOT NULL,
                    [FailedCount] int NOT NULL,
                    [StartedAtUtc] datetime2 NOT NULL,
                    [LastSavedAtUtc] datetime2 NULL,
                    [FinishedAtUtc] datetime2 NULL,
                    [UndoneAtUtc] datetime2 NULL,
                    [UndoneByUserId] uniqueidentifier NULL,
                    [RemovedCount] int NOT NULL
                );
                -- LastSavedAtUtc came after the first version of this table; a database
                -- made with that version is given it here.
                IF COL_LENGTH(N'dbo.ImportBatches', N'LastSavedAtUtc') IS NULL
                    ALTER TABLE [ImportBatches] ADD [LastSavedAtUtc] datetime2 NULL;
                IF COL_LENGTH(N'dbo.Leads', N'ImportBatchId') IS NULL
                    ALTER TABLE [Leads] ADD [ImportBatchId] uniqueidentifier NULL;
                IF COL_LENGTH(N'dbo.Contacts', N'ImportBatchId') IS NULL
                    ALTER TABLE [Contacts] ADD [ImportBatchId] uniqueidentifier NULL;
                IF COL_LENGTH(N'dbo.Companies', N'ImportBatchId') IS NULL
                    ALTER TABLE [Companies] ADD [ImportBatchId] uniqueidentifier NULL;
            ");
            await db.Database.ExecuteSqlRawAsync(@"
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ImportBatches_OrganizationId_StartedAtUtc')
                        CREATE INDEX [IX_ImportBatches_OrganizationId_StartedAtUtc] ON [ImportBatches] ([OrganizationId], [StartedAtUtc]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Leads_ImportBatchId')
                        CREATE INDEX [IX_Leads_ImportBatchId] ON [Leads] ([ImportBatchId]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Contacts_ImportBatchId')
                        CREATE INDEX [IX_Contacts_ImportBatchId] ON [Contacts] ([ImportBatchId]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Companies_ImportBatchId')
                        CREATE INDEX [IX_Companies_ImportBatchId] ON [Companies] ([ImportBatchId]);
                END TRY BEGIN CATCH END CATCH;
            ");

            // Every save gives a lead, deal, contact or company a new version, and a save made
            // from an older read of it is refused instead of written over the newer one (see
            // IVersioned). Existing rows each take a fresh value, and the default covers a row
            // written during a deploy by an instance still running the previous code. Adding a
            // NOT NULL column writes every row once, which on the Basic database tier can
            // outlast a command's default 30 seconds, so the batch has longer. Each ALTER is
            // guarded on its own, so a start that stops part-way is finished by the next.
            var versionTimeout = db.Database.GetCommandTimeout();
            db.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
            try
            {
                await db.Database.ExecuteSqlRawAsync(@"
                IF COL_LENGTH(N'dbo.Leads', N'Version') IS NULL
                    ALTER TABLE [Leads] ADD [Version] uniqueidentifier NOT NULL CONSTRAINT [DF_Leads_Version] DEFAULT NEWID();
                IF COL_LENGTH(N'dbo.Deals', N'Version') IS NULL
                    ALTER TABLE [Deals] ADD [Version] uniqueidentifier NOT NULL CONSTRAINT [DF_Deals_Version] DEFAULT NEWID();
                IF COL_LENGTH(N'dbo.Contacts', N'Version') IS NULL
                    ALTER TABLE [Contacts] ADD [Version] uniqueidentifier NOT NULL CONSTRAINT [DF_Contacts_Version] DEFAULT NEWID();
                IF COL_LENGTH(N'dbo.Companies', N'Version') IS NULL
                    ALTER TABLE [Companies] ADD [Version] uniqueidentifier NOT NULL CONSTRAINT [DF_Companies_Version] DEFAULT NEWID();
            ");
            }
            finally
            {
                db.Database.SetCommandTimeout(versionTimeout);
            }

            // Duplicates (see DuplicateService). Contacts carry the keys they are matched on, and
            // the team's "not the same" decisions are kept in DuplicateDecisions. The contact
            // indexes go in a batch of their own, after the columns they cover exist, each
            // guarded and caught on its own as above.
            await db.Database.ExecuteSqlRawAsync(@"
                IF COL_LENGTH(N'dbo.Contacts', N'PhoneKey') IS NULL
                    ALTER TABLE [Contacts] ADD [PhoneKey] nvarchar(16) NULL;
                IF COL_LENGTH(N'dbo.Contacts', N'NameKey') IS NULL
                    ALTER TABLE [Contacts] ADD [NameKey] nvarchar(256) NULL;
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DuplicateDecisions')
                BEGIN
                    CREATE TABLE [DuplicateDecisions] (
                        [Id] uniqueidentifier NOT NULL,
                        [OrganizationId] uniqueidentifier NULL,
                        [UserId] uniqueidentifier NOT NULL,
                        [Kind] nvarchar(16) NOT NULL,
                        [RecordIdA] uniqueidentifier NOT NULL,
                        [RecordIdB] uniqueidentifier NOT NULL,
                        [CreatedAtUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_DuplicateDecisions] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_DuplicateDecisions_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId])
                            REFERENCES [Organizations] ([Id]),
                        CONSTRAINT [FK_DuplicateDecisions_Users_UserId] FOREIGN KEY ([UserId])
                            REFERENCES [Users] ([Id])
                    );
                    CREATE INDEX [IX_DuplicateDecisions_OrganizationId_Kind] ON [DuplicateDecisions] ([OrganizationId], [Kind]);
                    CREATE INDEX [IX_DuplicateDecisions_UserId] ON [DuplicateDecisions] ([UserId]);
                END;
            ");
            await db.Database.ExecuteSqlRawAsync(@"
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Contacts_OrganizationId_PhoneKey')
                        CREATE INDEX [IX_Contacts_OrganizationId_PhoneKey] ON [Contacts] ([OrganizationId], [PhoneKey]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Contacts_OrganizationId_NameKey')
                        CREATE INDEX [IX_Contacts_OrganizationId_NameKey] ON [Contacts] ([OrganizationId], [NameKey]);
                END TRY BEGIN CATCH END CATCH;
            ");
            // The match keys of contacts saved before those columns (see ContactMatchKeys). On
            // its own: a failure is logged and retried on the next start.
            try
            {
                var keyed = await ACI.WebApi.ContactMatchKeys.FillMissingAsync(db);
                if (keyed > 0) Log.Information("Gave {Count} contacts the keys duplicates are matched on", keyed);
            }
            catch (Exception keysEx)
            {
                Log.Warning(keysEx, "Could not give older contacts their duplicate-matching keys; it will be retried on the next start");
            }

            // The outbox (see OutboxService): every email and integration event a workspace
            // sends, the addresses the mail service reported as bounced or complaining, and
            // the replies matched back to what was sent. New tables, so an existing database
            // and a new one get them the same way; the indexes follow in their own batch,
            // each guarded and caught on its own as above.
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OutboundMessages')
                CREATE TABLE [OutboundMessages] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NULL,
                    [Channel] nvarchar(16) NOT NULL,
                    [Source] nvarchar(16) NOT NULL,
                    [SourceId] uniqueidentifier NULL,
                    [Status] nvarchar(16) NOT NULL,
                    [Attempts] int NOT NULL,
                    [NextAttemptAtUtc] datetime2 NULL,
                    [ClaimedUntilUtc] datetime2 NULL,
                    [LastError] nvarchar(1000) NULL,
                    [LastStatusCode] int NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [SentAtUtc] datetime2 NULL,
                    [CreatedByUserId] uniqueidentifier NULL,
                    [FromName] nvarchar(200) NULL,
                    [FromAddress] nvarchar(256) NULL,
                    [ReplyTo] nvarchar(600) NULL,
                    [To] nvarchar(max) NULL,
                    [Cc] nvarchar(max) NULL,
                    [Bcc] nvarchar(max) NULL,
                    [RecipientName] nvarchar(200) NULL,
                    [Subject] nvarchar(300) NULL,
                    [Body] nvarchar(max) NULL,
                    [TextBody] nvarchar(max) NULL,
                    [HtmlBody] nvarchar(max) NULL,
                    [UnsubscribeUrl] nvarchar(1000) NULL,
                    [MessageIdHeader] nvarchar(255) NULL,
                    [ReplyToken] nvarchar(64) NULL,
                    [CountsAsResponse] bit NOT NULL,
                    [LeadId] uniqueidentifier NULL,
                    [ContactId] uniqueidentifier NULL,
                    [DealId] uniqueidentifier NULL,
                    [MemberId] uniqueidentifier NULL,
                    [ActivityId] uniqueidentifier NULL,
                    [DeliveryStatus] nvarchar(32) NULL,
                    [DeliveryDetail] nvarchar(500) NULL,
                    [DeliveryUpdatedAtUtc] datetime2 NULL,
                    [OpenedAtUtc] datetime2 NULL,
                    [RepliedAtUtc] datetime2 NULL,
                    [EventName] nvarchar(64) NULL,
                    [PayloadJson] nvarchar(max) NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EmailSuppressions')
                CREATE TABLE [EmailSuppressions] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NOT NULL,
                    [Email] nvarchar(256) NOT NULL,
                    [Reason] nvarchar(32) NOT NULL,
                    [Detail] nvarchar(500) NULL,
                    [OutboundMessageId] uniqueidentifier NULL,
                    [CreatedAtUtc] datetime2 NOT NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'InboundEmails')
                CREATE TABLE [InboundEmails] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OrganizationId] uniqueidentifier NOT NULL,
                    [OutboundMessageId] uniqueidentifier NOT NULL,
                    [MessageIdHeader] nvarchar(255) NULL,
                    [FromAddress] nvarchar(256) NOT NULL,
                    [Subject] nvarchar(300) NULL,
                    [MatchedBy] nvarchar(16) NOT NULL,
                    [ActivityId] uniqueidentifier NULL,
                    [ReceivedAtUtc] datetime2 NOT NULL
                );
            ");
            await db.Database.ExecuteSqlRawAsync(@"
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OutboundMessages_Status_NextAttemptAtUtc')
                        CREATE INDEX [IX_OutboundMessages_Status_NextAttemptAtUtc] ON [OutboundMessages] ([Status], [NextAttemptAtUtc]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OutboundMessages_OrganizationId_CreatedAtUtc')
                        CREATE INDEX [IX_OutboundMessages_OrganizationId_CreatedAtUtc] ON [OutboundMessages] ([OrganizationId], [CreatedAtUtc]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OutboundMessages_SourceId_Status')
                        CREATE INDEX [IX_OutboundMessages_SourceId_Status] ON [OutboundMessages] ([SourceId], [Status]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OutboundMessages_MessageIdHeader')
                        CREATE UNIQUE INDEX [IX_OutboundMessages_MessageIdHeader] ON [OutboundMessages] ([MessageIdHeader])
                            WHERE [MessageIdHeader] IS NOT NULL;
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OutboundMessages_ReplyToken')
                        CREATE UNIQUE INDEX [IX_OutboundMessages_ReplyToken] ON [OutboundMessages] ([ReplyToken])
                            WHERE [ReplyToken] IS NOT NULL;
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmailSuppressions_OrganizationId_Email')
                        CREATE UNIQUE INDEX [IX_EmailSuppressions_OrganizationId_Email] ON [EmailSuppressions] ([OrganizationId], [Email]);
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InboundEmails_OrganizationId_MessageIdHeader')
                        CREATE UNIQUE INDEX [IX_InboundEmails_OrganizationId_MessageIdHeader] ON [InboundEmails] ([OrganizationId], [MessageIdHeader])
                            WHERE [MessageIdHeader] IS NOT NULL;
                END TRY BEGIN CATCH END CATCH;
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InboundEmails_OutboundMessageId')
                        CREATE INDEX [IX_InboundEmails_OutboundMessageId] ON [InboundEmails] ([OutboundMessageId]);
                END TRY BEGIN CATCH END CATCH;
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
            
            // Create DealStageChanges table if missing (HP-11)
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DealStageChanges')
                BEGIN
                    CREATE TABLE [DealStageChanges] (
                        [Id] uniqueidentifier NOT NULL,
                        [DealId] uniqueidentifier NOT NULL,
                        [FromDealStageId] uniqueidentifier NULL,
                        [FromStageName] nvarchar(128) NULL,
                        [ToDealStageId] uniqueidentifier NULL,
                        [ToStageName] nvarchar(128) NULL,
                        [ChangedByUserId] uniqueidentifier NOT NULL,
                        [ChangedAtUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_DealStageChanges] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_DealStageChanges_Deals_DealId] FOREIGN KEY ([DealId])
                            REFERENCES [Deals] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_DealStageChanges_Users_ChangedByUserId] FOREIGN KEY ([ChangedByUserId])
                            REFERENCES [Users] ([Id]) ON DELETE NO ACTION
                    );
                    CREATE INDEX [IX_DealStageChanges_DealId] ON [DealStageChanges] ([DealId]);
                    CREATE INDEX [IX_DealStageChanges_ChangedByUserId] ON [DealStageChanges] ([ChangedByUserId]);
                END;
            ");
            
            // Create TaskComments table if missing (HP-12)
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TaskComments')
                BEGIN
                    CREATE TABLE [TaskComments] (
                        [Id] uniqueidentifier NOT NULL,
                        [TaskItemId] uniqueidentifier NOT NULL,
                        [AuthorId] uniqueidentifier NOT NULL,
                        [Body] nvarchar(max) NOT NULL,
                        [CreatedAtUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_TaskComments] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_TaskComments_TaskItems_TaskItemId] FOREIGN KEY ([TaskItemId])
                            REFERENCES [TaskItems] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_TaskComments_Users_AuthorId] FOREIGN KEY ([AuthorId])
                            REFERENCES [Users] ([Id]) ON DELETE NO ACTION
                    );
                    CREATE INDEX [IX_TaskComments_TaskItemId] ON [TaskComments] ([TaskItemId]);
                    CREATE INDEX [IX_TaskComments_AuthorId] ON [TaskComments] ([AuthorId]);
                END;
            ");
            
            // Create ContactRelationships table if missing: who is whose spouse, parent, and so on.
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ContactRelationships')
                BEGIN
                    CREATE TABLE [ContactRelationships] (
                        [Id] uniqueidentifier NOT NULL,
                        [UserId] uniqueidentifier NOT NULL,
                        [OrganizationId] uniqueidentifier NULL,
                        [ContactId] uniqueidentifier NOT NULL,
                        [RelatedContactId] uniqueidentifier NOT NULL,
                        [Type] nvarchar(32) NOT NULL,
                        [CreatedAtUtc] datetime2 NOT NULL,
                        [UpdatedAtUtc] datetime2 NULL,
                        CONSTRAINT [PK_ContactRelationships] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_ContactRelationships_Contacts_ContactId] FOREIGN KEY ([ContactId])
                            REFERENCES [Contacts] ([Id]),
                        CONSTRAINT [FK_ContactRelationships_Contacts_RelatedContactId] FOREIGN KEY ([RelatedContactId])
                            REFERENCES [Contacts] ([Id]),
                        CONSTRAINT [FK_ContactRelationships_Users_UserId] FOREIGN KEY ([UserId])
                            REFERENCES [Users] ([Id]),
                        CONSTRAINT [FK_ContactRelationships_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId])
                            REFERENCES [Organizations] ([Id])
                    );
                    CREATE UNIQUE INDEX [IX_ContactRelationships_ContactId_RelatedContactId] ON [ContactRelationships] ([ContactId], [RelatedContactId]);
                    CREATE INDEX [IX_ContactRelationships_RelatedContactId] ON [ContactRelationships] ([RelatedContactId]);
                    CREATE INDEX [IX_ContactRelationships_OrganizationId] ON [ContactRelationships] ([OrganizationId]);
                    CREATE INDEX [IX_ContactRelationships_UserId] ON [ContactRelationships] ([UserId]);
                END;
            ");

            // Create Notifications table if missing.
            //
            // AddNotifications is one of the two migrations with no .Designer.cs, so
            // EF never discovered it and MigrateAsync never ran it -- yet
            // AppDbContext has always exposed DbSet<Notification>. Every read of it
            // therefore failed on "Invalid object name 'Notifications'". Mirrors the
            // migration exactly, including the filtered unique index the reminder
            // generator relies on to re-run without creating duplicates.
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Notifications')
                BEGIN
                    CREATE TABLE [Notifications] (
                        [Id] uniqueidentifier NOT NULL,
                        [UserId] uniqueidentifier NOT NULL,
                        [OrganizationId] uniqueidentifier NULL,
                        [Type] nvarchar(32) NOT NULL,
                        [Title] nvarchar(512) NOT NULL,
                        [Message] nvarchar(max) NULL,
                        [Link] nvarchar(512) NULL,
                        [Read] bit NOT NULL,
                        [CreatedAtUtc] datetime2 NOT NULL,
                        [SourceKey] nvarchar(256) NULL,
                        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Notifications_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId])
                            REFERENCES [Organizations] ([Id]),
                        CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId])
                            REFERENCES [Users] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_Notifications_CreatedAtUtc] ON [Notifications] ([CreatedAtUtc]);
                    CREATE INDEX [IX_Notifications_OrganizationId] ON [Notifications] ([OrganizationId]);
                    CREATE INDEX [IX_Notifications_UserId_OrganizationId_Read] ON [Notifications] ([UserId], [OrganizationId], [Read]);
                    CREATE UNIQUE INDEX [IX_Notifications_UserId_SourceKey] ON [Notifications] ([UserId], [SourceKey])
                        WHERE [SourceKey] IS NOT NULL;
                END;
            ");
            
            // Fix existing orphan templates to be system templates
            await db.Database.ExecuteSqlRawAsync(@"
                UPDATE [Templates] 
                SET [IsSystemTemplate] = 1 
                WHERE [UserId] IS NULL AND [IsSystemTemplate] = 0;
            ");
            Log.Information("Schema check complete");

            // A database made before the baseline migration, now up to date, is recorded at
            // the baseline without running it; one that still lacks a table or column the
            // model maps is refused instead (see SchemaBaseline). This replaces marking
            // InitialCreate as applied whenever MigrateAsync hit a table that already
            // existed, which left every later migration to fail the same way.
            await ACI.WebApi.SchemaBaseline.RecordOnExistingDatabaseAsync(
                db, scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ACI.WebApi.SchemaBaseline"));

            Log.Information("Applying database migrations...");
            // A migration that moves data runs it as one command: the club ledger's copy of
            // the money documents took 16 seconds for a club with 140,000 of them on a
            // developer's machine, and longer on the Basic tier, against a command's default
            // 30. A timeout rolls the migration back, and every start after it fails the
            // same way. So migrations have the ten minutes the long DDL above has.
            var migrationTimeout = db.Database.GetCommandTimeout();
            db.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
            try
            {
                await db.Database.MigrateAsync();
            }
            finally
            {
                db.Database.SetCommandTimeout(migrationTimeout);
            }

            Log.Information("Seeding database...");
            await SeedData.SeedAsync(db, passwordHasher, seedDemoUser: app.Environment.IsDevelopment());
            
            // Bring EXISTING organisations onto the agreed lead status vocabulary.
            //
            // The AlignLeadStatusVocabulary migration that was meant to do this had no
            // .Designer.cs, so EF never discovered it and it never ran. Established
            // tenants kept the old six labels, the pipeline could not resolve a status
            // against them, and leads stayed on "New" however much work was logged. So
            // the alignment has lived here, next to the rest of the start-up work.
            //
            // It runs once, as a data fix (see DataFixes): run on every start, it
            // deleted statuses managers had made and put back ones they had removed.
            // After seeding, and in its own try/catch so a failure degrades the
            // pipeline without taking the whole initialisation -- or /db-status --
            // down with it; it is retried on the next start.
            try
            {
                Log.Information("Aligning lead status vocabulary...");
                var aligned = await db.Database.ExecuteSqlRawAsync(ACI.WebApi.DataFixes.AlignLeadStatusVocabularySql);
                Log.Information("Lead status vocabulary aligned ({Rows} row(s) affected)", aligned);
            }
            catch (Exception vocabEx)
            {
                Log.Error(vocabEx, "Could not align the lead status vocabulary. Organisations still on "
                    + "retired labels will not have a status derived from their pipeline: {ErrorMessage}",
                    vocabEx.Message);
            }
            
            Log.Information("Database initialization complete");
        }
        catch (Exception dbEx)
        {
            dbError = dbEx.ToString();
            Log.Error(dbEx, "Database migration failed (app will start but DB operations will fail): {ErrorMessage}", dbEx.Message);
            // Don't throw - allow app to start for debugging
        }
    }
    
    // Whether start-up initialisation worked. Anonymous, so outside Development it says
    // only that: the exception text names the SQL server and login, tables and stack
    // frames, and is in the log already (logged above).
    app.MapGet("/db-status", () => dbError == null
        ? Results.Ok(new { status = "ok", message = "Database initialized successfully" })
        : app.Environment.IsDevelopment()
            ? Results.Ok(new { status = "error", message = dbError })
            : Results.Ok(new { status = "error", message = "Database initialization failed. The details are in the server log." }));

    // Diagnostic endpoints below leak stack traces and schema. Restrict to Development.
    if (app.Environment.IsDevelopment())
    {

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
    
    // Test leads
    app.MapGet("/db-test-leads", async (AppDbContext db) =>
    {
        try
        {
            var count = await db.Leads.CountAsync();
            return Results.Ok(new { status = "ok", count });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.Message, detail = ex.ToString() });
        }
    });
    
    // Test deals
    app.MapGet("/db-test-deals", async (AppDbContext db) =>
    {
        try
        {
            var count = await db.Deals.CountAsync();
            return Results.Ok(new { status = "ok", count });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.Message, detail = ex.ToString() });
        }
    });
    
    // Test companies
    app.MapGet("/db-test-companies", async (AppDbContext db) =>
    {
        try
        {
            var count = await db.Companies.CountAsync();
            return Results.Ok(new { status = "ok", count });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.Message, detail = ex.ToString() });
        }
    });
    
    // Test activities
    app.MapGet("/db-test-activities", async (AppDbContext db) =>
    {
        try
        {
            var count = await db.Activities.CountAsync();
            return Results.Ok(new { status = "ok", count });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.Message, detail = ex.ToString() });
        }
    });
    
    // Test leads service
    app.MapGet("/db-test-leads-service", async (AppDbContext db, ILeadService leadService) =>
    {
        try
        {
            var firstUser = await db.Users.FirstOrDefaultAsync();
            if (firstUser == null) return Results.Ok(new { status = "no_users" });
            var leads = await leadService.GetLeadsPagedAsync(firstUser.Id, null, 1, 10);
            return Results.Ok(new { status = "ok", totalCount = leads.TotalCount });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.Message, detail = ex.ToString() });
        }
    });
    
    // Test deals service
    app.MapGet("/db-test-deals-service", async (AppDbContext db, IDealService dealService) =>
    {
        try
        {
            var firstUser = await db.Users.FirstOrDefaultAsync();
            if (firstUser == null) return Results.Ok(new { status = "no_users" });
            var deals = await dealService.GetDealsPagedAsync(firstUser.Id, null, 1, 10);
            return Results.Ok(new { status = "ok", totalCount = deals.TotalCount });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.Message, detail = ex.ToString() });
        }
    });
    
    // Test companies service
    app.MapGet("/db-test-companies-service", async (AppDbContext db, ICompanyService companyService) =>
    {
        try
        {
            var firstUser = await db.Users.FirstOrDefaultAsync();
            if (firstUser == null) return Results.Ok(new { status = "no_users" });
            var companies = await companyService.GetCompaniesPagedAsync(firstUser.Id, null, 1, 10);
            return Results.Ok(new { status = "ok", totalCount = companies.TotalCount });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.Message, detail = ex.ToString() });
        }
    });
    
    // Test activities service
    app.MapGet("/db-test-activities-service", async (AppDbContext db, IActivityService activityService) =>
    {
        try
        {
            var firstUser = await db.Users.FirstOrDefaultAsync();
            if (firstUser == null) return Results.Ok(new { status = "no_users" });
            var activities = await activityService.GetPagedAsync(firstUser.Id, null, 1, 10);
            return Results.Ok(new { status = "ok", totalCount = activities.TotalCount });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.Message, detail = ex.ToString() });
        }
    });
    
    // Test tasks
    app.MapGet("/db-test-tasks", async (AppDbContext db) =>
    {
        try
        {
            var count = await db.TaskItems.CountAsync();
            return Results.Ok(new { status = "ok", count });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.Message });
        }
    });
    
    // Test copy history
    app.MapGet("/db-test-copyhistory", async (AppDbContext db) =>
    {
        try
        {
            var count = await db.CopyHistoryItems.CountAsync();
            return Results.Ok(new { status = "ok", count });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.Message });
        }
    });
    
    
    // Test tasks service
    app.MapGet("/db-test-tasks-service", async (AppDbContext db, ITaskService taskService) =>
    {
        try
        {
            var firstUser = await db.Users.FirstOrDefaultAsync();
            if (firstUser == null) return Results.Ok(new { status = "no_users" });
            var tasks = await taskService.GetPagedAsync(firstUser.Id, null, 1, 10);
            return Results.Ok(new { status = "ok", totalCount = tasks.TotalCount });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.Message });
        }
    });
    
    // Test copy history service
    app.MapGet("/db-test-copyhistory-service", async (AppDbContext db, ICopyHistoryService copyHistoryService) =>
    {
        try
        {
            var firstUser = await db.Users.FirstOrDefaultAsync();
            if (firstUser == null) return Results.Ok(new { status = "no_users" });
            var history = await copyHistoryService.GetHistoryAsync(firstUser.Id, null);
            return Results.Ok(new { status = "ok", count = history.Count });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { status = "error", message = ex.Message });
        }
    });

    } // end Development-only diagnostic endpoints

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
