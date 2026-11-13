using System.Linq.Expressions;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// Every read of a workspace's records is filtered to the workspace the request or job
/// acts for (ITenantContext), whatever repository does the reading.
/// </summary>
/// <remarks>
/// <para>
/// Isolation used to be a predicate each repository wrote for itself, copied into every
/// one, and the copies had drifted: colleagues could not open each other's companies,
/// and a check forgotten in one query was a leak. The repositories still say which
/// workspace they mean; these filters make sure that nothing outside it comes back even
/// where one does not. They apply to reads, and to bulk updates and deletes
/// (ExecuteUpdate, ExecuteDelete), but not to what a save writes.
/// </para>
/// <para>
/// In an organization a record is visible when it belongs to that organization. In a
/// personal workspace (no organization) it is visible when it has no organization and is
/// the person's own, by the column the table records its owner in. A table with no owner
/// column shows nothing in a personal workspace: its personal rows were one bucket that
/// every user without an organization shared. With no workspace at all (an anonymous
/// request, a background job, a tool) nothing is visible: such code acts for a workspace
/// first (<see cref="ActFor"/>), or reads across workspaces with IgnoreQueryFilters and
/// says why next to it.
/// </para>
/// <para>
/// Left unfiltered, on purpose: users and their settings; organizations, memberships,
/// invitations and join requests, which are how a person finds the workspaces they belong
/// to or are asked into, and so are read across them; and records that belong to another
/// record rather than to a workspace (a pipeline's stages, a deal's line items and stage
/// changes, a task's comments, a contract's events, a sequence's steps, a team's members,
/// the marks that stop an automation firing twice), which are only ever reached through that
/// record, filtered itself.
/// WorkspaceFilterTests fails when a new table with an OrganizationId has no filter.
/// </para>
/// </remarks>
public partial class AppDbContext
{
    /// <summary>
    /// The organization reads are filtered to; null in a personal workspace or with none.
    /// EF reads this on every query, so a filter always uses the workspace of the moment.
    /// </summary>
    public Guid? TenantOrganizationId => _tenant.OrganizationId;

    /// <summary>Whose personal workspace reads are filtered to when there is no organization; null with no workspace.</summary>
    public Guid? TenantUserId => _tenant.UserId;

    /// <summary>
    /// Reads as this workspace (an organization, or with none <paramref name="userId"/>'s
    /// personal workspace) until the returned scope is disposed. The same as acting for it
    /// through the request's ITenantContext, which this context reads its workspace from.
    /// </summary>
    public IDisposable ActFor(Guid? organizationId, Guid? userId) => _tenant.ActFor(organizationId, userId);

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        // A record that belongs to a filtered one (a deal's line items, a pipeline's stages)
        // is not filtered itself, so EF warns that loading it with a parent from another
        // workspace drops it. Dropping it is the point.
        optionsBuilder.ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
    }

    private void ScopeToWorkspace(ModelBuilder model)
    {
        // Records a person can keep in a personal workspace as well as in an organization.
        Scope<Lead>(model, e => e.OrganizationId, e => e.UserId);
        Scope<Contact>(model, e => e.OrganizationId, e => e.UserId);
        Scope<Company>(model, e => e.OrganizationId, e => e.UserId);
        Scope<ContactRelationship>(model, e => e.OrganizationId, e => e.UserId);
        Scope<Deal>(model, e => e.OrganizationId, e => e.UserId);
        Scope<TaskItem>(model, e => e.OrganizationId, e => e.UserId);
        Scope<Activity>(model, e => e.OrganizationId, e => e.UserId);
        Scope<Notification>(model, e => e.OrganizationId, e => e.UserId);
        Scope<CopyHistoryItem>(model, e => e.OrganizationId, e => e.UserId);
        Scope<DuplicateDecision>(model, e => e.OrganizationId, e => e.UserId);
        Scope<ImportBatch>(model, e => e.OrganizationId, e => e.UserId);
        Scope<OutreachSequence>(model, e => e.OrganizationId, e => e.UserId);
        Scope<OutreachEnrollment>(model, e => e.OrganizationId, e => e.EnrolledByUserId);
        Scope<LeadStatusEvent>(model, e => e.OrganizationId, e => e.ChangedByUserId);
        Scope<LeadCaptureForm>(model, e => e.OrganizationId, e => e.CreatedByUserId);
        Scope<Attachment>(model, e => e.OrganizationId, e => e.OwnerUserId);
        Scope<AuditEvent>(model, e => e.OrganizationId, e => e.OwnerUserId);
        Scope<CustomFieldDefinition>(model, e => e.OrganizationId, e => e.OwnerUserId);
        Scope<CustomFieldValueSet>(model, e => e.OrganizationId, e => e.OwnerUserId);
        Scope<Product>(model, e => e.OrganizationId, e => e.OwnerUserId);
        Scope<AutomationRule>(model, e => e.OrganizationId, e => e.OwnerUserId);
        Scope<ClubMember>(model, e => e.OrganizationId, e => e.OwnerUserId);
        Scope<ForecastSnapshot>(model, e => e.OrganizationId, e => e.SavedByUserId);
        Scope<SentMessage>(model, e => e.OrganizationId, e => e.SentByUserId);
        Scope<OutboundMessage>(model, e => e.OrganizationId, e => e.CreatedByUserId);

        // An organization's alone: nothing of these is visible in a personal workspace.
        Scope<Pipeline>(model, e => e.OrganizationId);
        Scope<LeadStatus>(model, e => e.OrganizationId);
        Scope<LeadSource>(model, e => e.OrganizationId);
        Scope<OrgSettings>(model, e => e.OrganizationId);
        Scope<SalesTeam>(model, e => e.OrganizationId);
        Scope<Contract>(model, e => e.OrganizationId);
        Scope<EmailSuppression>(model, e => e.OrganizationId);
        Scope<InboundEmail>(model, e => e.OrganizationId);
        Scope<LeadScoringRule>(model, e => e.OrganizationId);
        Scope<LeadAssignmentRule>(model, e => e.OrganizationId);
        Scope<LeadTerritory>(model, e => e.OrganizationId);
        Scope<LeadQueue>(model, e => e.OrganizationId);
        Scope<LeadOpsSettings>(model, e => e.OrganizationId);
        Scope<ClubVisit>(model, e => e.OrganizationId);
        Scope<LoyaltyLedgerEntry>(model, e => e.OrganizationId);
        Scope<ClubDocument>(model, e => e.OrganizationId);
        // The club's money ledger. Every row says its organization, the ones that hang off
        // another (a tab's items, an allocation, a card's transactions) included, so none
        // is ever read through a parent from another workspace.
        Scope<ClubHouseAccount>(model, e => e.OrganizationId);
        Scope<ClubCharge>(model, e => e.OrganizationId);
        Scope<ClubPayment>(model, e => e.OrganizationId);
        Scope<ClubPaymentAllocation>(model, e => e.OrganizationId);
        Scope<ClubSubscription>(model, e => e.OrganizationId);
        Scope<ClubGiftCard>(model, e => e.OrganizationId);
        Scope<ClubGiftCardTransaction>(model, e => e.OrganizationId);
        Scope<ClubTab>(model, e => e.OrganizationId);
        Scope<ClubTabItem>(model, e => e.OrganizationId);
        Scope<ClubDonation>(model, e => e.OrganizationId);
        // Events and their guest lists, the bookable resources and their bookings: club
        // records, an organization's alone. A registration says its organization too, so it is
        // never read through an event from another workspace.
        Scope<ClubEvent>(model, e => e.OrganizationId);
        Scope<ClubEventRegistration>(model, e => e.OrganizationId);
        Scope<ClubResource>(model, e => e.OrganizationId);
        Scope<ClubReservation>(model, e => e.OrganizationId);
        // Member communications and consent: club records, so an organization's alone.
        Scope<ConsentRecord>(model, e => e.OrganizationId);
        Scope<Campaign>(model, e => e.OrganizationId);
        Scope<MemberMessage>(model, e => e.OrganizationId);
        // The club's price catalogue and what its billing run records. A file that goes with
        // an outbox email says its organization too, though it is only reached through the email.
        Scope<ClubCatalogItem>(model, e => e.OrganizationId);
        Scope<ClubBillingRun>(model, e => e.OrganizationId);
        Scope<ClubPaymentReminder>(model, e => e.OrganizationId);
        Scope<ClubStatementDelivery>(model, e => e.OrganizationId);
        Scope<OutboundAttachment>(model, e => e.OrganizationId);
        // Applications, the public application form and households: club records, an
        // organization's alone. A household's people say their organization too, so none is
        // ever read through a household from another workspace.
        Scope<ClubApplication>(model, e => e.OrganizationId);
        Scope<ClubApplicationForm>(model, e => e.OrganizationId);
        Scope<ClubHousehold>(model, e => e.OrganizationId);
        Scope<ClubHouseholdMember>(model, e => e.OrganizationId);
        // Payments from outside the club and the accounting settings: club money, so an
        // organization's alone. Stripe's posts and the payment job find their workspace
        // across workspaces first (IgnoreQueryFilters, said where), then act for it.
        Scope<ClubPaymentSettings>(model, e => e.OrganizationId);
        Scope<ClubPaymentProfile>(model, e => e.OrganizationId);
        Scope<ClubOnlinePayment>(model, e => e.OrganizationId);
        Scope<ClubProviderEvent>(model, e => e.OrganizationId);
        Scope<ClubPaymentReference>(model, e => e.OrganizationId);
        // A contract's deposit: the signing link's request acts for the contract's workspace
        // before it reads one, and Stripe's webhook for the workspace its address names.
        Scope<ContractDeposit>(model, e => e.OrganizationId);
        Scope<ClubBankImport>(model, e => e.OrganizationId);
        Scope<ClubBankTransaction>(model, e => e.OrganizationId);
        // Sales, the POS's tickets and connection, and members' codes: club money and club records,
        // an organization's alone. The POS import finds the workspaces it runs for across
        // workspaces first (IgnoreQueryFilters, said where), then acts for each.
        Scope<ClubSale>(model, e => e.OrganizationId);
        Scope<ClubSaleLine>(model, e => e.OrganizationId);
        Scope<ClubMemberCode>(model, e => e.OrganizationId);
        Scope<ClubPosSettings>(model, e => e.OrganizationId);
        Scope<ClubPosTicket>(model, e => e.OrganizationId);
        // Privacy: the settings, the log of erasures and the retention runs. An organization's alone;
        // the retention job finds the workspaces it runs for across workspaces first
        // (IgnoreQueryFilters, said where), then acts for each.
        Scope<PrivacySettings>(model, e => e.OrganizationId);
        Scope<PersonErasure>(model, e => e.OrganizationId);
        Scope<RetentionRun>(model, e => e.OrganizationId);
        Scope<OnboardingSettings>(model, e => e.OrganizationId);
        Scope<DealOnboarding>(model, e => e.OrganizationId);
        Scope<EmailImage>(model, e => e.OrganizationId);
        // An automation's runs and the log of their steps: an organization's alone, as its
        // rules are. The runner finds what is due across workspaces first (IgnoreQueryFilters,
        // said where), then acts for each run's own.
        Scope<AutomationRun>(model, e => e.OrganizationId);
        Scope<AutomationRunStep>(model, e => e.OrganizationId);
        // The member portal: an organization's alone. A member signing in names no workspace
        // until their link or session is found, which is looked up across workspaces by its
        // secret's hash or its id (IgnoreQueryFilters, said where); a signed-in member reads as
        // the organization their session names (TenantContext).
        Scope<MemberPortalSettings>(model, e => e.OrganizationId);
        Scope<MemberPortalAccount>(model, e => e.OrganizationId);
        Scope<MemberPortalSession>(model, e => e.OrganizationId);
        Scope<MemberPortalLink>(model, e => e.OrganizationId);
        // Retention and referrals: an organization's alone. The daily scoring run finds the
        // workspaces it scores across workspaces first (IgnoreQueryFilters, said where), then
        // acts for each.
        Scope<ClubRetentionSettings>(model, e => e.OrganizationId);
        Scope<ClubMemberRisk>(model, e => e.OrganizationId);
        Scope<ClubRenewal>(model, e => e.OrganizationId);
        Scope<ClubReferralBonus>(model, e => e.OrganizationId);
        Scope<ClubMemberConcern>(model, e => e.OrganizationId);
        Scope<ClubScoringRun>(model, e => e.OrganizationId);

        // Copy templates follow a person rather than a workspace (TemplateRepository): the
        // system's templates are everyone's, a person's own go with them, and a team's
        // shared ones stay in its organization.
        model.Entity<Template>().HasQueryFilter(e =>
            e.IsSystemTemplate
            || (TenantUserId != null && e.UserId == TenantUserId)
            || (TenantOrganizationId != null && e.OrganizationId == TenantOrganizationId));
    }

    /// <summary>
    /// Filters <typeparamref name="TEntity"/> to the workspace: its organization's rows, and
    /// in a personal workspace the person's own rows that have no organization, when the
    /// table records an owner.
    /// </summary>
    /// <remarks>
    /// The filter is "OrganizationId = the workspace's organization", and with that, either
    /// the workspace is an organization or the row is the acting person's own:
    /// <c>e.OrganizationId == org &amp;&amp; (org != null || (user != null &amp;&amp; e.Owner == user))</c>.
    /// Written that way round, the SQL always starts with a plain match on OrganizationId
    /// (EF writes "= @org" for an organization and "IS NULL" for a personal workspace,
    /// from the parameter's value), which the OrganizationId indexes serve; the rest are
    /// tests on parameters. The user must be known for the personal branch: in C# terms
    /// null equals null, so with no one acting a row with neither an organization nor an
    /// owner would otherwise show.
    /// </remarks>
    private void Scope<TEntity>(
        ModelBuilder model,
        Expression<Func<TEntity, Guid?>> organizationOf,
        Expression<Func<TEntity, Guid?>>? ownerOf = null)
        where TEntity : class
    {
        var entity = Expression.Parameter(typeof(TEntity), "e");
        var context = Expression.Constant(this);
        var tenantOrganization = Expression.Property(context, nameof(TenantOrganizationId));
        var tenantUser = Expression.Property(context, nameof(TenantUserId));
        var none = Expression.Constant(null, typeof(Guid?));

        Expression either = Expression.NotEqual(tenantOrganization, none);
        if (ownerOf != null)
        {
            either = Expression.OrElse(either, Expression.AndAlso(
                Expression.NotEqual(tenantUser, none),
                Expression.Equal(Rebind(ownerOf, entity), tenantUser)));
        }
        var visible = Expression.AndAlso(Expression.Equal(Rebind(organizationOf, entity), tenantOrganization), either);

        model.Entity<TEntity>().HasQueryFilter(Expression.Lambda<Func<TEntity, bool>>(visible, entity));
    }

    private static Expression Rebind<TEntity>(Expression<Func<TEntity, Guid?>> selector, ParameterExpression entity) =>
        new ParameterSwap(selector.Parameters[0], entity).Visit(selector.Body);

    private sealed class ParameterSwap : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly ParameterExpression _to;

        public ParameterSwap(ParameterExpression from, ParameterExpression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node) => node == _from ? _to : node;
    }
}
