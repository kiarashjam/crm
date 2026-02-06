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
            
            Log.Information("Applying database migrations...");
            await db.Database.MigrateAsync();
            
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
