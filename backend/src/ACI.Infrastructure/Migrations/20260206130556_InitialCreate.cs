using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorSecretProtected = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TwoFactorEnabledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WebhookApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WebhookApiKeyCreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Organizations_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Timezone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, defaultValue: "UTC"),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "en"),
                    Bio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BrandTone = table.Column<int>(type: "int", nullable: false),
                    EmailSignature = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DefaultEmailSubjectPrefix = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Theme = table.Column<int>(type: "int", nullable: false),
                    DataDensity = table.Column<int>(type: "int", nullable: false),
                    SidebarCollapsed = table.Column<bool>(type: "bit", nullable: false),
                    ShowWelcomeBanner = table.Column<bool>(type: "bit", nullable: false),
                    EmailNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EmailOnNewLead = table.Column<bool>(type: "bit", nullable: false),
                    EmailOnDealUpdate = table.Column<bool>(type: "bit", nullable: false),
                    EmailOnTaskDue = table.Column<bool>(type: "bit", nullable: false),
                    EmailOnTeamMention = table.Column<bool>(type: "bit", nullable: false),
                    EmailDigestFrequency = table.Column<int>(type: "int", nullable: false),
                    InAppNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    InAppSoundEnabled = table.Column<bool>(type: "bit", nullable: false),
                    BrowserNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DefaultPipelineId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DefaultLeadStatusId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DefaultLeadSourceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DefaultFollowUpDays = table.Column<int>(type: "int", nullable: false, defaultValue: 3),
                    DefaultCurrency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true, defaultValue: "USD"),
                    ShowActivityStatus = table.Column<bool>(type: "bit", nullable: false),
                    AllowAnalytics = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Industry = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Size = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Companies_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Companies_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Companies_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CopyHistoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Copy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecipientName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecipientType = table.Column<int>(type: "int", nullable: false),
                    RecipientId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CopyTypeId = table.Column<int>(type: "int", nullable: false),
                    Goal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BrandTone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRewritten = table.Column<bool>(type: "bit", nullable: false),
                    CopyCount = table.Column<int>(type: "int", nullable: false),
                    SendCount = table.Column<int>(type: "int", nullable: false),
                    ResponseCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CopyHistoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CopyHistoryItems_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CopyHistoryItems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSharedWithOrganization = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSequences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailSequences_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EmailSequences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Token = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invites_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JoinRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RespondedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JoinRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JoinRequests_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JoinRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeadSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadSources_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadStatuses_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationMembers",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationMembers", x => new { x.OrganizationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_OrganizationMembers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrgSettings",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    BrandTone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSettings", x => x.OrganizationId);
                    table.ForeignKey(
                        name: "FK_OrgSettings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pipelines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pipelines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pipelines_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CopyTypeId = table.Column<int>(type: "int", nullable: false),
                    Goal = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BrandTone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Length = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    UseCount = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsSharedWithOrganization = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsSystemTemplate = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Templates_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Templates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmailSequenceSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CopyTypeId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DelayDays = table.Column<int>(type: "int", nullable: false),
                    DelayHours = table.Column<int>(type: "int", nullable: false),
                    StopOnReply = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSequenceSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailSequenceSteps_EmailSequences_SequenceId",
                        column: x => x.SequenceId,
                        principalTable: "EmailSequences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DealStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PipelineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsWon = table.Column<bool>(type: "bit", nullable: false),
                    IsLost = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealStages_Pipelines_PipelineId",
                        column: x => x.PipelineId,
                        principalTable: "Pipelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DealId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Participants = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Activities_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Activities_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Activities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConvertedFromLeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConvertedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchivedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DoNotContact = table.Column<bool>(type: "bit", nullable: false),
                    PreferredContactMethod = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contacts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Contacts_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Contacts_Users_ArchivedByUserId",
                        column: x => x.ArchivedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Contacts_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Contacts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Deals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    Stage = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PipelineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DealStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssigneeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExpectedCloseDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsWon = table.Column<bool>(type: "bit", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deals_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Deals_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Deals_DealStages_DealStageId",
                        column: x => x.DealStageId,
                        principalTable: "DealStages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Deals_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Deals_Pipelines_PipelineId",
                        column: x => x.PipelineId,
                        principalTable: "Pipelines",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Deals_Users_AssigneeId",
                        column: x => x.AssigneeId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Deals_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Deals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LeadSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeadStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeadScore = table.Column<int>(type: "int", nullable: true),
                    LastContactedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LifecycleStage = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsConverted = table.Column<bool>(type: "bit", nullable: false),
                    ConvertedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConvertedToContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConvertedToDealId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConvertedToCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leads_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Leads_Deals_ConvertedToDealId",
                        column: x => x.ConvertedToDealId,
                        principalTable: "Deals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Leads_LeadSources_LeadSourceId",
                        column: x => x.LeadSourceId,
                        principalTable: "LeadSources",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Leads_LeadStatuses_LeadStatusId",
                        column: x => x.LeadStatusId,
                        principalTable: "LeadStatuses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Leads_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Leads_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Leads_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailSequenceEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RecipientName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CurrentStep = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EnrolledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextSendAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSequenceEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailSequenceEnrollments_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmailSequenceEnrollments_EmailSequences_SequenceId",
                        column: x => x.SequenceId,
                        principalTable: "EmailSequences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailSequenceEnrollments_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmailSequenceEnrollments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TaskItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    DueDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReminderDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "Todo"),
                    Priority = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "None"),
                    Completed = table.Column<bool>(type: "bit", nullable: false),
                    AssigneeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DealId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskItems_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TaskItems_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TaskItems_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TaskItems_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TaskItems_Users_AssigneeId",
                        column: x => x.AssigneeId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TaskItems_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TaskItems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ContactId",
                table: "Activities",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_DealId",
                table: "Activities",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_LeadId",
                table: "Activities",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_OrganizationId",
                table: "Activities",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_UpdatedByUserId",
                table: "Activities",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_UserId",
                table: "Activities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_OrganizationId",
                table: "Companies",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_UpdatedByUserId",
                table: "Companies",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_UserId",
                table: "Companies",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_ArchivedByUserId",
                table: "Contacts",
                column: "ArchivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_CompanyId",
                table: "Contacts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_ConvertedFromLeadId",
                table: "Contacts",
                column: "ConvertedFromLeadId",
                unique: true,
                filter: "[ConvertedFromLeadId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_DoNotContact",
                table: "Contacts",
                column: "DoNotContact");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_Email_OrganizationId",
                table: "Contacts",
                columns: new[] { "Email", "OrganizationId" },
                unique: true,
                filter: "[IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_IsArchived",
                table: "Contacts",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_OrganizationId",
                table: "Contacts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_UpdatedByUserId",
                table: "Contacts",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_UserId",
                table: "Contacts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CopyHistoryItems_OrganizationId",
                table: "CopyHistoryItems",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CopyHistoryItems_UserId",
                table: "CopyHistoryItems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_AssigneeId",
                table: "Deals",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_CompanyId",
                table: "Deals",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_ContactId",
                table: "Deals",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_DealStageId",
                table: "Deals",
                column: "DealStageId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_OrganizationId",
                table: "Deals",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_PipelineId",
                table: "Deals",
                column: "PipelineId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_UpdatedByUserId",
                table: "Deals",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_UserId",
                table: "Deals",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DealStages_PipelineId",
                table: "DealStages",
                column: "PipelineId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSequenceEnrollments_ContactId",
                table: "EmailSequenceEnrollments",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSequenceEnrollments_LeadId",
                table: "EmailSequenceEnrollments",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSequenceEnrollments_NextSendAtUtc",
                table: "EmailSequenceEnrollments",
                column: "NextSendAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSequenceEnrollments_SequenceId",
                table: "EmailSequenceEnrollments",
                column: "SequenceId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSequenceEnrollments_Status",
                table: "EmailSequenceEnrollments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSequenceEnrollments_UserId",
                table: "EmailSequenceEnrollments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSequences_IsActive",
                table: "EmailSequences",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSequences_OrganizationId",
                table: "EmailSequences",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSequences_OrganizationId_IsSharedWithOrganization",
                table: "EmailSequences",
                columns: new[] { "OrganizationId", "IsSharedWithOrganization" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailSequences_UserId",
                table: "EmailSequences",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSequenceSteps_SequenceId",
                table: "EmailSequenceSteps",
                column: "SequenceId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSequenceSteps_SequenceId_StepOrder",
                table: "EmailSequenceSteps",
                columns: new[] { "SequenceId", "StepOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Invites_OrganizationId_Email",
                table: "Invites",
                columns: new[] { "OrganizationId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_Invites_Token",
                table: "Invites",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_OrganizationId_UserId",
                table: "JoinRequests",
                columns: new[] { "OrganizationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_UserId",
                table: "JoinRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CompanyId",
                table: "Leads",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_ConvertedToDealId",
                table: "Leads",
                column: "ConvertedToDealId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_IsConverted",
                table: "Leads",
                column: "IsConverted");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_LeadSourceId",
                table: "Leads",
                column: "LeadSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_LeadStatusId",
                table: "Leads",
                column: "LeadStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_OrganizationId",
                table: "Leads",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_UpdatedByUserId",
                table: "Leads",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_UserId",
                table: "Leads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadSources_OrganizationId",
                table: "LeadSources",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadStatuses_OrganizationId",
                table: "LeadStatuses",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembers_UserId",
                table: "OrganizationMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_OwnerUserId",
                table: "Organizations",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Pipelines_OrganizationId",
                table: "Pipelines",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_AssigneeId",
                table: "TaskItems",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ContactId",
                table: "TaskItems",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_DealId",
                table: "TaskItems",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_DueDateUtc",
                table: "TaskItems",
                column: "DueDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_LeadId",
                table: "TaskItems",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_OrganizationId",
                table: "TaskItems",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_OrganizationId_Status",
                table: "TaskItems",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_Status",
                table: "TaskItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_UpdatedByUserId",
                table: "TaskItems",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_UserId_Status",
                table: "TaskItems",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Templates_IsSystemTemplate",
                table: "Templates",
                column: "IsSystemTemplate");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_OrganizationId",
                table: "Templates",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_UserId",
                table: "Templates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_Contacts_ContactId",
                table: "Activities",
                column: "ContactId",
                principalTable: "Contacts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_Deals_DealId",
                table: "Activities",
                column: "DealId",
                principalTable: "Deals",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_Leads_LeadId",
                table: "Activities",
                column: "LeadId",
                principalTable: "Leads",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Contacts_Leads_ConvertedFromLeadId",
                table: "Contacts",
                column: "ConvertedFromLeadId",
                principalTable: "Leads",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Contacts_ContactId",
                table: "Deals");

            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropTable(
                name: "CopyHistoryItems");

            migrationBuilder.DropTable(
                name: "EmailSequenceEnrollments");

            migrationBuilder.DropTable(
                name: "EmailSequenceSteps");

            migrationBuilder.DropTable(
                name: "Invites");

            migrationBuilder.DropTable(
                name: "JoinRequests");

            migrationBuilder.DropTable(
                name: "OrganizationMembers");

            migrationBuilder.DropTable(
                name: "OrgSettings");

            migrationBuilder.DropTable(
                name: "TaskItems");

            migrationBuilder.DropTable(
                name: "Templates");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "EmailSequences");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropTable(
                name: "Deals");

            migrationBuilder.DropTable(
                name: "LeadSources");

            migrationBuilder.DropTable(
                name: "LeadStatuses");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "DealStages");

            migrationBuilder.DropTable(
                name: "Pipelines");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
