using iM1os.Application.Common;
using iM1os.Application.Tenancy;
using iM1os.Domain.Audit;
using iM1os.Domain.Common;
using iM1os.Domain.Configuration;
using iM1os.Domain.Customers;
using iM1os.Domain.Employees;
using iM1os.Domain.FinancialServices.Accounting;
using iM1os.Domain.FinancialServices.Banking;
using iM1os.Domain.FinancialServices.Billing;
using iM1os.Domain.FinancialServices.Hardware;
using iM1os.Domain.FinancialServices.Ledger;
using iM1os.Domain.FinancialServices.Lending;
using iM1os.Domain.FinancialServices.Merchant;
using iM1os.Domain.FinancialServices.Payments;
using iM1os.Domain.FinancialServices.Providers;
using iM1os.Domain.FinancialServices.Wallet;
using iM1os.Domain.GlobalCatalog;
using iM1os.Domain.Identity;
using iM1os.Domain.Marketing;
using iM1os.Domain.Parts;
using iM1os.Domain.Platform;
using iM1os.Domain.Service;
using iM1os.Domain.Tenancy;
using iM1os.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Persistence;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ITenantProvider tenantProvider) : DbContext(options), IApplicationDbContext
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<EmployeeCompensation> EmployeeCompensations => Set<EmployeeCompensation>();

    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();

    public DbSet<EmployeeTimePunch> EmployeeTimePunches => Set<EmployeeTimePunch>();

    public DbSet<EmployeeScheduleShift> EmployeeScheduleShifts => Set<EmployeeScheduleShift>();

    public DbSet<EmployeeTimeOffRequest> EmployeeTimeOffRequests => Set<EmployeeTimeOffRequest>();

    public DbSet<EmployeeSafetyIncident> EmployeeSafetyIncidents => Set<EmployeeSafetyIncident>();

    public DbSet<EmployeeCompanyAsset> EmployeeCompanyAssets => Set<EmployeeCompanyAsset>();

    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();

    public DbSet<PasswordResetRequest> PasswordResetRequests => Set<PasswordResetRequest>();

    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    public DbSet<ApplicationSetting> ApplicationSettings => Set<ApplicationSetting>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<DomainEventRecord> DomainEvents => Set<DomainEventRecord>();

    public DbSet<TimelineEvent> TimelineEvents => Set<TimelineEvent>();

    public DbSet<TenantIdentityEvent> TenantIdentityEvents => Set<TenantIdentityEvent>();

    public DbSet<BusinessOnboarding> BusinessOnboardings => Set<BusinessOnboarding>();

    public DbSet<BusinessConfiguration> BusinessConfigurations => Set<BusinessConfiguration>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();

    public DbSet<CustomerPhoneNumber> CustomerPhoneNumbers => Set<CustomerPhoneNumber>();

    public DbSet<CustomerNote> CustomerNotes => Set<CustomerNote>();

    public DbSet<CustomerTag> CustomerTags => Set<CustomerTag>();

    public DbSet<CustomerCustomField> CustomerCustomFields => Set<CustomerCustomField>();

    public DbSet<CustomerExternalLink> CustomerExternalLinks => Set<CustomerExternalLink>();

    public DbSet<CustomerDocument> CustomerDocuments => Set<CustomerDocument>();

    public DbSet<CustomerVehicle> CustomerVehicles => Set<CustomerVehicle>();

    public DbSet<CustomerVehicleAttachment> CustomerVehicleAttachments => Set<CustomerVehicleAttachment>();

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    public DbSet<WorkOrderAttachment> WorkOrderAttachments => Set<WorkOrderAttachment>();

    public DbSet<Estimate> Estimates => Set<Estimate>();

    public DbSet<EstimateLineItem> EstimateLineItems => Set<EstimateLineItem>();

    public DbSet<LaborOperation> LaborOperations => Set<LaborOperation>();

    public DbSet<WorkOrderTechnicianAssignment> WorkOrderTechnicianAssignments => Set<WorkOrderTechnicianAssignment>();

    public DbSet<GlobalProduct> GlobalProducts => Set<GlobalProduct>();

    public DbSet<CanonicalItem> CanonicalItems => Set<CanonicalItem>();

    public DbSet<CanonicalBrandAlias> CanonicalBrandAliases => Set<CanonicalBrandAlias>();

    public DbSet<CanonicalItemIdentifier> CanonicalItemIdentifiers => Set<CanonicalItemIdentifier>();

    public DbSet<CanonicalItemSupplierOffer> CanonicalItemSupplierOffers => Set<CanonicalItemSupplierOffer>();

    public DbSet<CanonicalFitment> CanonicalFitments => Set<CanonicalFitment>();

    public DbSet<CanonicalItemSource> CanonicalItemSources => Set<CanonicalItemSource>();

    public DbSet<GlobalVehicle> GlobalVehicles => Set<GlobalVehicle>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<SupplierProduct> SupplierProducts => Set<SupplierProduct>();

    public DbSet<SupplierPrice> SupplierPrices => Set<SupplierPrice>();

    public DbSet<VehicleFitment> VehicleFitments => Set<VehicleFitment>();

    public DbSet<SupplierFitmentRecord> SupplierFitmentRecords => Set<SupplierFitmentRecord>();

    public DbSet<ProductMatchReviewItem> ProductMatchReviewItems => Set<ProductMatchReviewItem>();

    public DbSet<SupplierConnectorConfiguration> SupplierConnectorConfigurations => Set<SupplierConnectorConfiguration>();

    public DbSet<SupplierConnectorImportRun> SupplierConnectorImportRuns => Set<SupplierConnectorImportRun>();

    public DbSet<CompanySupplierConnectorConfiguration> CompanySupplierConnectorConfigurations => Set<CompanySupplierConnectorConfiguration>();

    public DbSet<CompanySupplierConnectorImportRun> CompanySupplierConnectorImportRuns => Set<CompanySupplierConnectorImportRun>();

    public DbSet<CompanySupplierPrice> CompanySupplierPrices => Set<CompanySupplierPrice>();

    public DbSet<ManufacturerPart> ManufacturerParts => Set<ManufacturerPart>();

    public DbSet<ManufacturerPartImage> ManufacturerPartImages => Set<ManufacturerPartImage>();

    public DbSet<ManufacturerPartCrossReference> ManufacturerPartCrossReferences => Set<ManufacturerPartCrossReference>();

    public DbSet<SupplierListing> SupplierListings => Set<SupplierListing>();

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    public DbSet<CompanyInventoryItem> CompanyInventoryItems => Set<CompanyInventoryItem>();

    public DbSet<CompanyInventoryLocationStock> CompanyInventoryLocationStocks => Set<CompanyInventoryLocationStock>();

    public DbSet<CompanyInventoryMovement> CompanyInventoryMovements => Set<CompanyInventoryMovement>();

    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    public DbSet<FinancialLedgerEntry> FinancialLedgerEntries => Set<FinancialLedgerEntry>();

    public DbSet<MerchantAccount> MerchantAccounts => Set<MerchantAccount>();

    public DbSet<MerchantProviderRelationship> MerchantProviderRelationships => Set<MerchantProviderRelationship>();

    public DbSet<MerchantAccountStatusHistory> MerchantAccountStatusHistories => Set<MerchantAccountStatusHistory>();

    public DbSet<CustomerWallet> CustomerWallets => Set<CustomerWallet>();

    public DbSet<WalletPaymentMethod> WalletPaymentMethods => Set<WalletPaymentMethod>();

    public DbSet<PaymentTerminal> PaymentTerminals => Set<PaymentTerminal>();

    public DbSet<FinancialProviderConnection> FinancialProviderConnections => Set<FinancialProviderConnection>();

    public DbSet<SubscriptionAgreement> SubscriptionAgreements => Set<SubscriptionAgreement>();

    public DbSet<BankingConnection> BankingConnections => Set<BankingConnection>();

    public DbSet<FinancingApplication> FinancingApplications => Set<FinancingApplication>();

    public DbSet<AccountingExportBatch> AccountingExportBatches => Set<AccountingExportBatch>();

    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();

    public DbSet<PlatformTenant> PlatformTenants => Set<PlatformTenant>();

    public DbSet<PlatformSubscription> PlatformSubscriptions => Set<PlatformSubscription>();

    public DbSet<TenantModuleEntitlement> TenantModuleEntitlements => Set<TenantModuleEntitlement>();

    public DbSet<PlatformEvent> PlatformEvents => Set<PlatformEvent>();

    public DbSet<PlatformAuditEvent> PlatformAuditEvents => Set<PlatformAuditEvent>();

    public DbSet<WelcomeEmail> WelcomeEmails => Set<WelcomeEmail>();

    public DbSet<MarketingPage> MarketingPages => Set<MarketingPage>();

    public DbSet<MarketingContentBlock> MarketingContentBlocks => Set<MarketingContentBlock>();

    public DbSet<MarketingLead> MarketingLeads => Set<MarketingLead>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("platform");

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            entity.Property(x => x.LogoUrl).HasMaxLength(1000);
            entity.Property(x => x.LegalName).HasMaxLength(200);
            entity.Property(x => x.Dba).HasMaxLength(200);
            entity.Property(x => x.Website).HasMaxLength(300);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.TaxId).HasMaxLength(80);
            entity.Property(x => x.AddressLine1).HasMaxLength(200);
            entity.Property(x => x.AddressLine2).HasMaxLength(200);
            entity.Property(x => x.City).HasMaxLength(120);
            entity.Property(x => x.Region).HasMaxLength(80);
            entity.Property(x => x.PostalCode).HasMaxLength(20);
            entity.Property(x => x.Country).HasMaxLength(80);
            entity.Property(x => x.TimeZone).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Language).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(12).IsRequired();
            entity.Property(x => x.DateFormat).HasMaxLength(40).IsRequired();
            entity.Property(x => x.TimeFormat).HasMaxLength(40).IsRequired();
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.ToTable("locations");
            entity.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.AddressLine1).HasMaxLength(200);
            entity.Property(x => x.AddressLine2).HasMaxLength(200);
            entity.Property(x => x.City).HasMaxLength(120);
            entity.Property(x => x.Region).HasMaxLength(80);
            entity.Property(x => x.PostalCode).HasMaxLength(20);
            entity.Property(x => x.TimeZone).HasMaxLength(120).IsRequired();
            entity.Property(x => x.HoursJson).HasColumnType("jsonb");
            entity.Property(x => x.DefaultLaborRate).HasPrecision(12, 2);
            entity.Property(x => x.DefaultTaxRegion).HasMaxLength(120);
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasIndex(x => new { x.OrganizationId, x.NormalizedEmail }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.PinHash }).IsUnique().HasFilter("\"PinHash\" IS NOT NULL");
            entity.HasIndex(x => x.EmployeeId).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(120);
            entity.Property(x => x.LastName).HasMaxLength(120);
            entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.JobTitle).HasMaxLength(160);
            entity.Property(x => x.PasswordHash).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.MfaMethod).HasMaxLength(40);
            entity.Property(x => x.MfaSecretProtected).HasMaxLength(1000);
            entity.Property(x => x.AvatarUrl).HasMaxLength(1000);
            entity.Property(x => x.PinHash).HasMaxLength(1000);
            entity.Property(x => x.Language).HasMaxLength(20).IsRequired();
            entity.Property(x => x.TimeZone).HasMaxLength(120).IsRequired();
            entity.HasOne(x => x.Employee).WithOne(x => x.LoginAccount).HasForeignKey<ApplicationUser>(x => x.EmployeeId);
            entity.HasQueryFilter(x => x.DeletedAtUtc == null && (tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId));
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("employees");
            entity.HasIndex(x => new { x.OrganizationId, x.DisplayName });
            entity.HasIndex(x => new { x.OrganizationId, x.Email });
            entity.HasIndex(x => new { x.OrganizationId, x.EmployeeNumber }).IsUnique();
            entity.Property(x => x.EmployeeNumber).HasMaxLength(80);
            entity.Property(x => x.FirstName).HasMaxLength(120);
            entity.Property(x => x.LastName).HasMaxLength(120);
            entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.JobTitle).HasMaxLength(160);
            entity.Property(x => x.Department).HasMaxLength(120);
            entity.Property(x => x.EmploymentType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.IsTechnician).HasDefaultValue(false);
            entity.Property(x => x.IsServiceAdvisor).HasDefaultValue(false);
            entity.Property(x => x.IsSales).HasDefaultValue(false);
            entity.Property(x => x.IsParts).HasDefaultValue(false);
            entity.Property(x => x.IsAccounting).HasDefaultValue(false);
            entity.Property(x => x.IsInventory).HasDefaultValue(false);
            entity.Property(x => x.IsManager).HasDefaultValue(false);
            entity.HasQueryFilter(x => x.DeletedAtUtc == null && (tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId));
        });

        modelBuilder.Entity<EmployeeCompensation>(entity =>
        {
            entity.ToTable("employee_compensations");
            entity.HasIndex(x => new { x.OrganizationId, x.EmployeeId, x.EffectiveStartDate });
            entity.Property(x => x.PayrollType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.HourlyRate).HasPrecision(12, 2);
            entity.Property(x => x.SalaryAmount).HasPrecision(12, 2);
            entity.Property(x => x.WorkOrderCommissionRate).HasPrecision(8, 4);
            entity.Property(x => x.SalesCommissionRate).HasPrecision(8, 4);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasOne(x => x.Employee).WithMany(x => x.CompensationRecords).HasForeignKey(x => x.EmployeeId);
            entity.HasQueryFilter(x => x.DeletedAtUtc == null && (tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId));
        });

        modelBuilder.Entity<EmployeeDocument>(entity =>
        {
            entity.ToTable("employee_documents");
            entity.HasIndex(x => new { x.OrganizationId, x.EmployeeId });
            entity.HasIndex(x => new { x.OrganizationId, x.DocumentType });
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.DocumentType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.StorageKey).HasMaxLength(500);
            entity.Property(x => x.Url).HasMaxLength(1000);
            entity.Property(x => x.ContentType).HasMaxLength(120);
            entity.HasOne(x => x.Employee).WithMany(x => x.Documents).HasForeignKey(x => x.EmployeeId);
            entity.HasQueryFilter(x => x.DeletedAtUtc == null && (tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId));
        });

        modelBuilder.Entity<EmployeeTimePunch>(entity =>
        {
            entity.ToTable("employee_time_punches");
            entity.HasIndex(x => new { x.OrganizationId, x.EmployeeId, x.ClockInUtc });
            entity.HasIndex(x => new { x.OrganizationId, x.EmployeeId, x.ClockOutUtc });
            entity.Property(x => x.Hours).HasPrecision(8, 2);
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.Property(x => x.Source).HasMaxLength(80).IsRequired();
            entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            entity.HasQueryFilter(x => x.DeletedAtUtc == null && (tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId));
        });

        modelBuilder.Entity<EmployeeScheduleShift>(entity =>
        {
            entity.ToTable("employee_schedule_shifts");
            entity.HasIndex(x => new { x.OrganizationId, x.EmployeeId, x.ShiftDate });
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            entity.HasQueryFilter(x => x.DeletedAtUtc == null && (tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId));
        });

        modelBuilder.Entity<EmployeeTimeOffRequest>(entity =>
        {
            entity.ToTable("employee_time_off_requests");
            entity.HasIndex(x => new { x.OrganizationId, x.EmployeeId, x.StartDate, x.EndDate });
            entity.HasIndex(x => new { x.OrganizationId, x.Status });
            entity.Property(x => x.Type).HasMaxLength(80).IsRequired();
            entity.Property(x => x.HoursPerDay).HasPrecision(6, 2);
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.Property(x => x.ReviewedByUserId).HasMaxLength(80);
            entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            entity.HasQueryFilter(x => x.DeletedAtUtc == null && (tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId));
        });

        modelBuilder.Entity<EmployeeSafetyIncident>(entity =>
        {
            entity.ToTable("employee_safety_incidents");
            entity.HasIndex(x => new { x.OrganizationId, x.IncidentDate });
            entity.HasIndex(x => new { x.OrganizationId, x.EmployeeId, x.IncidentDate });
            entity.HasIndex(x => new { x.OrganizationId, x.IsOshaRecordable });
            entity.Property(x => x.IncidentType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Severity).HasMaxLength(80);
            entity.Property(x => x.LostTimeHours).HasPrecision(6, 2);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            entity.HasQueryFilter(x => x.DeletedAtUtc == null && (tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId));
        });

        modelBuilder.Entity<EmployeeCompanyAsset>(entity =>
        {
            entity.ToTable("employee_company_assets");
            entity.HasIndex(x => new { x.OrganizationId, x.EmployeeId, x.Status });
            entity.HasIndex(x => new { x.OrganizationId, x.AssetTag });
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.AssetTag).HasMaxLength(120);
            entity.Property(x => x.SerialNumber).HasMaxLength(160);
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            entity.HasQueryFilter(x => x.DeletedAtUtc == null && (tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId));
        });

        modelBuilder.Entity<UserInvitation>(entity =>
        {
            entity.ToTable("user_invitations");
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.UserId });
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<PasswordResetRequest>(entity =>
        {
            entity.ToTable("password_reset_requests");
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.UserId });
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<OrganizationMembership>(entity =>
        {
            entity.ToTable("organization_memberships");
            entity.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
            entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.EmployeeNumber).HasMaxLength(80);
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId);
            entity.HasOne(x => x.User).WithMany(x => x.OrganizationMemberships).HasForeignKey(x => x.UserId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasIndex(x => new { x.OrganizationId, x.NormalizedName }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.NormalizedName).HasMaxLength(120).IsRequired();
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("permissions");
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(300).IsRequired();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(x => new { x.UserId, x.RoleId });
            entity.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permissions");
            entity.HasKey(x => new { x.RoleId, x.PermissionId });
            entity.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId);
            entity.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId);
        });

        modelBuilder.Entity<UserPermissionOverride>(entity =>
        {
            entity.ToTable("user_permission_overrides");
            entity.HasIndex(x => new { x.OrganizationId, x.UserId, x.PermissionId }).IsUnique();
            entity.HasOne(x => x.User).WithMany(x => x.PermissionOverrides).HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.ToTable("user_sessions");
            entity.HasIndex(x => new { x.OrganizationId, x.UserId, x.RevokedAtUtc });
            entity.HasIndex(x => x.SessionKey).IsUnique();
            entity.Property(x => x.SessionKey).HasMaxLength(160).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(80);
            entity.Property(x => x.UserAgent).HasMaxLength(500);
            entity.HasOne(x => x.User).WithMany(x => x.Sessions).HasForeignKey(x => x.UserId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<FeatureFlag>(entity =>
        {
            entity.ToTable("feature_flags");
            entity.HasIndex(x => new { x.OrganizationId, x.Key }).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(160).IsRequired();
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<ApplicationSetting>(entity =>
        {
            entity.ToTable("application_settings");
            entity.HasIndex(x => new { x.OrganizationId, x.Key }).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Value).HasMaxLength(4000).IsRequired();
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasIndex(x => new { x.OrganizationId, x.OccurredAtUtc });
            entity.Property(x => x.Action).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EntityName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.UserId).HasMaxLength(80);
        });

        modelBuilder.Entity<TimelineEvent>(entity =>
        {
            entity.ToTable("timeline_events");
            entity.HasIndex(x => new { x.OrganizationId, x.OccurredAtUtc });
            entity.HasIndex(x => new { x.OrganizationId, x.EntityType, x.EntityId });
            entity.Property(x => x.EntityType).HasMaxLength(160).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EventType).HasMaxLength(160).IsRequired();
            entity.Property(x => x.ActorUserId).HasMaxLength(80);
            entity.Property(x => x.Summary).HasMaxLength(500).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb");
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<TenantIdentityEvent>(entity =>
        {
            entity.ToTable("tenant_identity_events");
            entity.HasIndex(x => new { x.OrganizationId, x.OccurredAtUtc });
            entity.HasIndex(x => x.EventType);
            entity.Property(x => x.EventType).HasMaxLength(160).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(80);
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<BusinessOnboarding>(entity =>
        {
            entity.ToTable("business_onboardings");
            entity.HasIndex(x => x.OrganizationId).IsUnique();
            entity.Property(x => x.BusinessName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.BusinessEmail).HasMaxLength(320).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(40).IsRequired();
            entity.Property(x => x.TimeZone).HasMaxLength(120).IsRequired();
            entity.Property(x => x.BusinessHoursJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.LaborRate).HasPrecision(12, 2);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<BusinessConfiguration>(entity =>
        {
            entity.ToTable("business_configurations");
            entity.HasIndex(x => x.OrganizationId).IsUnique();
            entity.Property(x => x.DefaultLaborRate).HasPrecision(12, 2);
            entity.Property(x => x.DiagnosticRate).HasPrecision(12, 2);
            entity.Property(x => x.EmergencyRate).HasPrecision(12, 2);
            entity.Property(x => x.WeekendRate).HasPrecision(12, 2);
            entity.Property(x => x.EnvironmentalFee).HasPrecision(12, 2);
            entity.Property(x => x.ShopSuppliesPercent).HasPrecision(8, 4);
            entity.Property(x => x.LaborLineItemsTaxable).HasDefaultValue(true);
            entity.Property(x => x.DefaultTaxRate).HasPrecision(8, 4);
            entity.Property(x => x.RegionalTaxOverridesJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.NumberSequencesJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.NotificationPreferencesJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.DepartmentsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.ConnectorPlaceholdersJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.SupplierPreferencesJson).HasColumnType("jsonb").IsRequired();
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<DomainEventRecord>(entity =>
        {
            entity.ToTable("domain_events");
            entity.HasIndex(x => new { x.OrganizationId, x.OccurredAtUtc });
            entity.HasIndex(x => new { x.OrganizationId, x.EntityType, x.EntityId });
            entity.HasIndex(x => x.EventType);
            entity.HasIndex(x => x.CorrelationId);
            entity.Property(x => x.EntityType).HasMaxLength(160).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EventType).HasMaxLength(160).IsRequired();
            entity.Property(x => x.ActorUserId).HasMaxLength(80);
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.CorrelationId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.SourceModule).HasMaxLength(120).IsRequired();
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasIndex(x => new { x.OrganizationId, x.DisplayName });
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerNumber }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.Email });
            entity.HasIndex(x => new { x.OrganizationId, x.Phone });
            entity.HasIndex(x => new { x.OrganizationId, x.Status });
            entity.HasIndex(x => new { x.OrganizationId, x.LifecycleStage });
            entity.Property(x => x.CustomerNumber).HasMaxLength(40);
            entity.Property(x => x.DisplayName).HasMaxLength(220).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(120);
            entity.Property(x => x.MiddleName).HasMaxLength(100);
            entity.Property(x => x.LastName).HasMaxLength(120);
            entity.Property(x => x.Nickname).HasMaxLength(100);
            entity.Property(x => x.CompanyName).HasMaxLength(220);
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.SecondaryEmail).HasMaxLength(320);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.MobilePhone).HasMaxLength(40);
            entity.Property(x => x.HomePhone).HasMaxLength(40);
            entity.Property(x => x.WorkPhone).HasMaxLength(40);
            entity.Property(x => x.CustomerType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LifecycleStage).HasMaxLength(80);
            entity.Property(x => x.Source).HasMaxLength(120);
            entity.Property(x => x.PreferredContactMethod).HasMaxLength(80);
            entity.Property(x => x.TaxExemptNumber).HasMaxLength(50);
            entity.Property(x => x.PreferredLanguage).HasMaxLength(80);
            entity.Property(x => x.SummaryNotes).HasMaxLength(4000);
            entity.Property(x => x.LifetimeSales).HasPrecision(18, 2);
            entity.Property(x => x.CreditLimit).HasPrecision(18, 2);
            entity.Property(x => x.CurrentBalance).HasPrecision(18, 2);
            entity.Property(x => x.StoreCredit).HasPrecision(18, 2);
            entity.HasQueryFilter(x => x.DeletedAtUtc == null && (tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId));
        });

        modelBuilder.Entity<CustomerAddress>(entity =>
        {
            entity.ToTable("customer_addresses");
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerId });
            entity.HasIndex(x => new { x.OrganizationId, x.PostalCode });
            entity.Property(x => x.AddressType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Line1).HasMaxLength(200);
            entity.Property(x => x.Line2).HasMaxLength(200);
            entity.Property(x => x.City).HasMaxLength(120);
            entity.Property(x => x.Region).HasMaxLength(80);
            entity.Property(x => x.PostalCode).HasMaxLength(20);
            entity.Property(x => x.Country).HasMaxLength(80).IsRequired();
            entity.HasOne(x => x.Customer).WithMany(x => x.Addresses).HasForeignKey(x => x.CustomerId);
            entity.HasQueryFilter(x => x.DeletedAtUtc == null && (tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId));
        });

        modelBuilder.Entity<CustomerPhoneNumber>(entity =>
        {
            entity.ToTable("customer_phone_numbers");
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerId });
            entity.HasIndex(x => new { x.OrganizationId, x.PhoneNumber });
            entity.Property(x => x.PhoneType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Extension).HasMaxLength(20);
            entity.HasOne(x => x.Customer).WithMany(x => x.PhoneNumbers).HasForeignKey(x => x.CustomerId);
            entity.HasQueryFilter(x => x.DeletedAtUtc == null && (tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId));
        });

        modelBuilder.Entity<CustomerNote>(entity =>
        {
            entity.ToTable("customer_notes");
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerId, x.OccurredAtUtc });
            entity.HasIndex(x => new { x.OrganizationId, x.NoteType });
            entity.Property(x => x.AuthorDisplayName).HasMaxLength(160);
            entity.Property(x => x.NoteType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(200);
            entity.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            entity.HasOne(x => x.Customer).WithMany(x => x.Notes).HasForeignKey(x => x.CustomerId);
            entity.HasQueryFilter(x => x.DeletedAtUtc == null && (tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId));
        });

        modelBuilder.Entity<CustomerTag>(entity =>
        {
            entity.ToTable("customer_tags");
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerId });
            entity.HasIndex(x => new { x.OrganizationId, x.Tag });
            entity.Property(x => x.Tag).HasMaxLength(120).IsRequired();
            entity.HasOne(x => x.Customer).WithMany(x => x.Tags).HasForeignKey(x => x.CustomerId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<CustomerCustomField>(entity =>
        {
            entity.ToTable("customer_custom_fields");
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerId, x.FieldKey }).IsUnique();
            entity.Property(x => x.FieldKey).HasMaxLength(120).IsRequired();
            entity.Property(x => x.FieldLabel).HasMaxLength(160);
            entity.Property(x => x.FieldValue).HasMaxLength(2000);
            entity.HasOne(x => x.Customer).WithMany(x => x.CustomFields).HasForeignKey(x => x.CustomerId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<CustomerExternalLink>(entity =>
        {
            entity.ToTable("customer_external_links");
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerId });
            entity.HasIndex(x => new { x.OrganizationId, x.Provider, x.ExternalCustomerId }).IsUnique();
            entity.Property(x => x.Provider).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ExternalCustomerId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ExternalUrl).HasMaxLength(1000);
            entity.Property(x => x.MetadataJson).HasColumnType("jsonb");
            entity.HasOne(x => x.Customer).WithMany(x => x.ExternalLinks).HasForeignKey(x => x.CustomerId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<CustomerDocument>(entity =>
        {
            entity.ToTable("customer_documents");
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerId });
            entity.HasIndex(x => new { x.OrganizationId, x.DocumentType });
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.DocumentType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.StorageKey).HasMaxLength(500);
            entity.Property(x => x.Url).HasMaxLength(1000);
            entity.Property(x => x.ContentType).HasMaxLength(120);
            entity.HasOne(x => x.Customer).WithMany(x => x.Documents).HasForeignKey(x => x.CustomerId);
            entity.HasQueryFilter(x => x.DeletedAtUtc == null && (tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId));
        });

        modelBuilder.Entity<CustomerVehicle>(entity =>
        {
            entity.ToTable("customer_vehicles");
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerId });
            entity.HasIndex(x => new { x.OrganizationId, x.Vin });
            entity.Property(x => x.Type).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Make).HasMaxLength(120);
            entity.Property(x => x.Model).HasMaxLength(160);
            entity.Property(x => x.Trim).HasMaxLength(120);
            entity.Property(x => x.Vin).HasMaxLength(80);
            entity.Property(x => x.Color).HasMaxLength(80);
            entity.Property(x => x.TagPlate).HasMaxLength(80);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<CustomerVehicleAttachment>(entity =>
        {
            entity.ToTable("customer_vehicle_attachments");
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerVehicleId });
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerId });
            entity.Property(x => x.AttachmentType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.Url).HasMaxLength(1000);
            entity.Property(x => x.ContentType).HasMaxLength(120);
            entity.HasOne(x => x.CustomerVehicle).WithMany().HasForeignKey(x => x.CustomerVehicleId);
            entity.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<WorkOrder>(entity =>
        {
            entity.ToTable("work_orders");
            entity.HasIndex(x => new { x.OrganizationId, x.WorkOrderNumber }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerId, x.OpenedAtUtc });
            entity.HasIndex(x => new { x.OrganizationId, x.ServiceAdvisorEmployeeId });
            entity.HasIndex(x => new { x.OrganizationId, x.LocationId, x.Stage });
            entity.Property(x => x.WorkOrderNumber).HasMaxLength(80).IsRequired();
            entity.Property(x => x.RepairOrderNumber).HasMaxLength(80);
            entity.Property(x => x.Stage).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Priority).HasMaxLength(80).IsRequired();
            entity.Property(x => x.RequestedService).HasMaxLength(2000);
            entity.Property(x => x.CustomerConcern).HasMaxLength(2000);
            entity.Property(x => x.DiagnosisFindings).HasMaxLength(4000);
            entity.Property(x => x.ServiceNotes).HasMaxLength(4000);
            entity.Property(x => x.PartsAndSuppliesNotes).HasMaxLength(4000);
            entity.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
            entity.HasOne<CustomerVehicle>().WithMany().HasForeignKey(x => x.CustomerVehicleId);
            entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.ServiceAdvisorEmployeeId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<WorkOrderAttachment>(entity =>
        {
            entity.ToTable("work_order_attachments");
            entity.HasIndex(x => new { x.OrganizationId, x.WorkOrderId });
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerId });
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerVehicleId });
            entity.Property(x => x.AttachmentType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.Url).HasMaxLength(1000);
            entity.Property(x => x.ContentType).HasMaxLength(120);
            entity.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId);
            entity.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
            entity.HasOne<CustomerVehicle>().WithMany().HasForeignKey(x => x.CustomerVehicleId);
            entity.HasQueryFilter(x => x.DeletedAtUtc == null && (tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId));
        });

        modelBuilder.Entity<Estimate>(entity =>
        {
            entity.ToTable("estimates");
            entity.HasIndex(x => new { x.OrganizationId, x.EstimateNumber }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.WorkOrderId });
            entity.Property(x => x.EstimateNumber).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.DepositTerms).HasMaxLength(120).IsRequired();
            entity.Property(x => x.PaymentTerms).HasMaxLength(1000);
            entity.Property(x => x.FeesTotal).HasPrecision(12, 2);
            entity.Property(x => x.DiscountTotal).HasPrecision(12, 2);
            entity.Property(x => x.Subtotal).HasPrecision(12, 2);
            entity.Property(x => x.LaborTotal).HasPrecision(12, 2);
            entity.Property(x => x.PartsTotal).HasPrecision(12, 2);
            entity.Property(x => x.TaxTotal).HasPrecision(12, 2);
            entity.Property(x => x.GrandTotal).HasPrecision(12, 2);
            entity.HasOne<WorkOrder>().WithMany(x => x.Estimates).HasForeignKey(x => x.WorkOrderId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<EstimateLineItem>(entity =>
        {
            entity.ToTable("estimate_line_items");
            entity.HasIndex(x => new { x.OrganizationId, x.EstimateId, x.SortOrder });
            entity.HasIndex(x => new { x.OrganizationId, x.WorkOrderId });
            entity.HasIndex(x => new { x.OrganizationId, x.SupplierProductId });
            entity.HasIndex(x => new { x.OrganizationId, x.InventoryItemId });
            entity.Property(x => x.LineType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.Sku).HasMaxLength(120);
            entity.Property(x => x.Quantity).HasPrecision(12, 2);
            entity.Property(x => x.Rate).HasPrecision(12, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(12, 2);
            entity.Property(x => x.DiscountPercent).HasPrecision(8, 4);
            entity.Property(x => x.LineTotal).HasPrecision(12, 2);
            entity.HasOne<Estimate>().WithMany(x => x.LineItems).HasForeignKey(x => x.EstimateId);
            entity.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId);
            entity.HasOne<LaborOperation>().WithMany().HasForeignKey(x => x.LaborOperationId);
            entity.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId);
            entity.HasOne<SupplierProduct>().WithMany().HasForeignKey(x => x.SupplierProductId);
            entity.HasOne<ManufacturerPart>().WithMany().HasForeignKey(x => x.ManufacturerPartId);
            entity.HasOne<InventoryItem>().WithMany().HasForeignKey(x => x.InventoryItemId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<LaborOperation>(entity =>
        {
            entity.ToTable("labor_operations");
            entity.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(220).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.ServiceCategory).HasMaxLength(120);
            entity.Property(x => x.BaseHours).HasPrecision(8, 2);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<WorkOrderTechnicianAssignment>(entity =>
        {
            entity.ToTable("work_order_technician_assignments");
            entity.HasIndex(x => new { x.OrganizationId, x.WorkOrderId, x.SortOrder });
            entity.HasIndex(x => new { x.OrganizationId, x.EmployeeId });
            entity.Property(x => x.Role).HasMaxLength(80).IsRequired();
            entity.Property(x => x.SplitPercent).HasPrecision(8, 4);
            entity.HasOne<WorkOrder>().WithMany(x => x.TechnicianAssignments).HasForeignKey(x => x.WorkOrderId);
            entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<GlobalProduct>(entity =>
        {
            entity.ToTable("global_products");
            entity.HasIndex(x => new { x.Brand, x.ManufacturerPartNumber }).IsUnique();
            entity.HasIndex(x => new { x.Brand, x.NormalizedManufacturerPartNumber });
            entity.HasIndex(x => x.Upc);
            entity.HasIndex(x => x.Description);
            entity.Property(x => x.Brand).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Manufacturer).HasMaxLength(160);
            entity.Property(x => x.ManufacturerPartNumber).HasMaxLength(120);
            entity.Property(x => x.NormalizedManufacturerPartNumber).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.LongDescription).HasMaxLength(4000);
            entity.Property(x => x.Category).HasMaxLength(160);
            entity.Property(x => x.Upc).HasMaxLength(80);
            entity.Property(x => x.TirePosition).HasMaxLength(24);
            entity.Property(x => x.TireConstruction).HasMaxLength(24);
            entity.Property(x => x.TireType).HasMaxLength(40);
            entity.Property(x => x.TireModelLine).HasMaxLength(80);
            entity.HasIndex(x => new { x.TireRimDiameter, x.TireWidth, x.TireAspectRatio });
            entity.HasIndex(x => x.TireModelLine);
            entity.Property(x => x.Length).HasPrecision(12, 3);
            entity.Property(x => x.Width).HasPrecision(12, 3);
            entity.Property(x => x.Height).HasPrecision(12, 3);
            entity.Property(x => x.Weight).HasPrecision(12, 3);
            entity.Property(x => x.ImagesJson).HasColumnType("jsonb");
            entity.Property(x => x.SpecificationsJson).HasColumnType("jsonb");
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
        });

        modelBuilder.Entity<CanonicalItem>(entity =>
        {
            entity.ToTable("canonical_items");
            entity.HasIndex(x => new { x.NormalizedManufacturerPartNumber, x.Brand });
            entity.HasIndex(x => x.NormalizedManufacturerPartNumber);
            entity.HasIndex(x => x.PrimaryUpc);
            entity.HasIndex(x => x.Category);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.IsActive);
            entity.Property(x => x.Brand).HasMaxLength(120);
            entity.Property(x => x.Manufacturer).HasMaxLength(160);
            entity.Property(x => x.ManufacturerPartNumber).HasMaxLength(120);
            entity.Property(x => x.NormalizedManufacturerPartNumber).HasMaxLength(120);
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(160);
            entity.Property(x => x.Subcategory).HasMaxLength(160);
            entity.Property(x => x.PrimaryUpc).HasMaxLength(80);
            entity.Property(x => x.PrimaryImageUrl).HasMaxLength(1000);
            entity.Property(x => x.SearchText);
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
        });

        modelBuilder.Entity<CanonicalBrandAlias>(entity =>
        {
            entity.ToTable("canonical_brand_aliases");
            entity.HasIndex(x => x.NormalizedBrand).IsUnique();
            entity.HasIndex(x => new { x.CanonicalBrand, x.IsActive });
            entity.Property(x => x.Brand).HasMaxLength(160).IsRequired();
            entity.Property(x => x.NormalizedBrand).HasMaxLength(160).IsRequired();
            entity.Property(x => x.CanonicalBrand).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<CanonicalItemIdentifier>(entity =>
        {
            entity.ToTable("canonical_item_identifiers");
            entity.HasIndex(x => x.CanonicalItemId);
            entity.HasIndex(x => new { x.CanonicalItemId, x.IdentifierType, x.NormalizedValue, x.SupplierProductId }).IsUnique();
            entity.HasIndex(x => x.NormalizedValue);
            entity.HasIndex(x => new { x.IdentifierType, x.NormalizedValue });
            entity.HasIndex(x => new { x.SupplierId, x.IdentifierType, x.NormalizedValue });
            entity.HasIndex(x => x.SupplierProductId);
            entity.Property(x => x.IdentifierType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.IdentifierValue).HasMaxLength(200).IsRequired();
            entity.Property(x => x.NormalizedValue).HasMaxLength(200).IsRequired();
            entity.Property(x => x.SupplierCode).HasMaxLength(80);
            entity.Property(x => x.Source).HasMaxLength(120);
            entity.HasOne<CanonicalItem>().WithMany().HasForeignKey(x => x.CanonicalItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId);
            entity.HasOne<SupplierProduct>().WithMany().HasForeignKey(x => x.SupplierProductId);
        });

        modelBuilder.Entity<CanonicalItemSupplierOffer>(entity =>
        {
            entity.ToTable("canonical_item_supplier_offers");
            entity.HasIndex(x => x.CanonicalItemId);
            entity.HasIndex(x => new { x.CanonicalItemId, x.SupplierCode });
            entity.HasIndex(x => x.SupplierProductId).IsUnique();
            entity.HasIndex(x => x.SupplierSku);
            entity.HasIndex(x => x.SupplierPartNumber);
            entity.HasIndex(x => new { x.SupplierId, x.SupplierSku });
            entity.Property(x => x.SupplierCode).HasMaxLength(80).IsRequired();
            entity.Property(x => x.SupplierSku).HasMaxLength(120).IsRequired();
            entity.Property(x => x.SupplierPartNumber).HasMaxLength(120);
            entity.Property(x => x.SupplierTitle).HasMaxLength(500);
            entity.Property(x => x.ListPrice).HasPrecision(12, 2);
            entity.Property(x => x.DealerCost).HasPrecision(12, 2);
            entity.Property(x => x.WarehouseAvailability).HasColumnType("jsonb");
            entity.Property(x => x.ImageUrl).HasMaxLength(1000);
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.HasOne<CanonicalItem>().WithMany().HasForeignKey(x => x.CanonicalItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId);
            entity.HasOne<SupplierProduct>().WithMany().HasForeignKey(x => x.SupplierProductId);
        });

        modelBuilder.Entity<CanonicalFitment>(entity =>
        {
            entity.ToTable("canonical_fitments");
            entity.HasIndex(x => x.CanonicalItemId);
            entity.HasIndex(x => new { x.Year, x.MakeKey, x.ModelKey });
            entity.HasIndex(x => new { x.CanonicalItemId, x.Year, x.MakeKey, x.ModelKey, x.SubmodelKey, x.EngineKey }).IsUnique();
            entity.HasIndex(x => x.VehicleType);
            entity.Property(x => x.Make).HasMaxLength(120).IsRequired();
            entity.Property(x => x.MakeKey).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Model).HasMaxLength(160).IsRequired();
            entity.Property(x => x.ModelKey).HasMaxLength(160).IsRequired();
            entity.Property(x => x.VehicleType).HasMaxLength(120);
            entity.Property(x => x.Submodel).HasMaxLength(160);
            entity.Property(x => x.SubmodelKey).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Engine).HasMaxLength(160);
            entity.Property(x => x.EngineKey).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne<CanonicalItem>().WithMany().HasForeignKey(x => x.CanonicalItemId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CanonicalItemSource>(entity =>
        {
            entity.ToTable("canonical_item_sources");
            entity.HasIndex(x => x.CanonicalItemId);
            entity.HasIndex(x => x.GlobalProductId);
            entity.HasIndex(x => x.SupplierProductId);
            entity.HasIndex(x => new { x.SourceTable, x.SourceKey }).IsUnique();
            entity.Property(x => x.SupplierCode).HasMaxLength(80);
            entity.Property(x => x.SourceTable).HasMaxLength(120).IsRequired();
            entity.Property(x => x.SourceKey).HasMaxLength(200).IsRequired();
            entity.Property(x => x.MatchMethod).HasMaxLength(120).IsRequired();
            entity.Property(x => x.MatchConfidence).HasPrecision(5, 4);
            entity.HasOne<CanonicalItem>().WithMany().HasForeignKey(x => x.CanonicalItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<GlobalProduct>().WithMany().HasForeignKey(x => x.GlobalProductId);
            entity.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId);
            entity.HasOne<SupplierProduct>().WithMany().HasForeignKey(x => x.SupplierProductId);
        });

        modelBuilder.Entity<GlobalVehicle>(entity =>
        {
            entity.ToTable("global_vehicles");
            entity.HasIndex(x => new { x.Year, x.Make, x.Model, x.Submodel, x.Engine, x.Market }).IsUnique();
            entity.HasIndex(x => x.VehicleType);
            entity.Property(x => x.VehicleClass).HasMaxLength(80);
            entity.Property(x => x.VehicleType).HasMaxLength(120);
            entity.Property(x => x.Make).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Model).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Submodel).HasMaxLength(160);
            entity.Property(x => x.Engine).HasMaxLength(160);
            entity.Property(x => x.VinRange).HasMaxLength(160);
            entity.Property(x => x.Market).HasMaxLength(80);
            entity.Property(x => x.Notes).HasMaxLength(1000);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("suppliers");
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ConnectorKey).HasMaxLength(120);
        });

        modelBuilder.Entity<SupplierProduct>(entity =>
        {
            entity.ToTable("supplier_products");
            entity.HasIndex(x => new { x.SupplierId, x.SupplierSku }).IsUnique();
            entity.HasIndex(x => new { x.SupplierId, x.SourceSupplierProductId });
            entity.HasIndex(x => x.GlobalProductId);
            entity.HasIndex(x => x.CanonicalItemId);
            entity.HasIndex(x => x.SupplierPartNumber);
            entity.HasIndex(x => new { x.SupplierId, x.SupplierPartNumber });
            entity.HasIndex(x => new { x.SupplierId, x.ManufacturerPartNumber });
            entity.HasIndex(x => new { x.SupplierId, x.NormalizedManufacturerPartNumber });
            entity.HasIndex(x => x.NormalizedManufacturerPartNumber);
            entity.Property(x => x.SupplierSku).HasMaxLength(120).IsRequired();
            entity.Property(x => x.SourceSupplierProductId).HasMaxLength(120);
            entity.Property(x => x.SupplierDescription).HasMaxLength(1000);
            entity.Property(x => x.SupplierPartNumber).HasMaxLength(120);
            entity.Property(x => x.ManufacturerPartNumber).HasMaxLength(120);
            entity.Property(x => x.NormalizedManufacturerPartNumber).HasMaxLength(120);
            entity.Property(x => x.SupplierStatus).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Packaging).HasMaxLength(160);
            entity.Property(x => x.WarehouseAvailability).HasMaxLength(500);
            entity.Property(x => x.SupplierImagesJson).HasColumnType("jsonb");
            entity.Property(x => x.SourceDataJson).HasColumnType("jsonb");
            entity.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId);
            entity.HasOne<GlobalProduct>().WithMany().HasForeignKey(x => x.GlobalProductId);
            entity.HasOne<CanonicalItem>().WithMany().HasForeignKey(x => x.CanonicalItemId);
        });

        modelBuilder.Entity<SupplierPrice>(entity =>
        {
            entity.ToTable("supplier_prices");
            entity.HasIndex(x => new { x.SupplierProductId, x.EffectiveDate });
            entity.Property(x => x.Msrp).HasPrecision(12, 2);
            entity.Property(x => x.Map).HasPrecision(12, 2);
            entity.Property(x => x.DealerCost).HasPrecision(12, 2);
            entity.HasOne<SupplierProduct>().WithMany().HasForeignKey(x => x.SupplierProductId);
        });

        modelBuilder.Entity<VehicleFitment>(entity =>
        {
            entity.ToTable("vehicle_fitments");
            entity.HasIndex(x => new { x.GlobalProductId, x.GlobalVehicleId, x.Position }).IsUnique();
            entity.Property(x => x.Position).HasMaxLength(120);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne<GlobalProduct>().WithMany().HasForeignKey(x => x.GlobalProductId);
            entity.HasOne<GlobalVehicle>().WithMany().HasForeignKey(x => x.GlobalVehicleId);
        });

        modelBuilder.Entity<SupplierFitmentRecord>(entity =>
        {
            entity.ToTable("supplier_fitment_records");
            entity.HasIndex(x => new { x.SupplierId, x.SupplierSku });
            entity.HasIndex(x => new { x.SupplierId, x.SourceSupplierProductId });
            entity.HasIndex(x => new { x.SupplierId, x.SourceFitmentItemId, x.Year, x.Make, x.Model });
            entity.HasIndex(x => x.SupplierProductId);
            entity.HasIndex(x => x.GlobalProductId);
            entity.HasIndex(x => x.GlobalVehicleId);
            entity.HasIndex(x => x.VehicleFitmentId);
            entity.HasIndex(x => x.VehicleType);
            entity.HasIndex(x => x.ResolutionStatus);
            entity.Property(x => x.SupplierKey).HasMaxLength(80).IsRequired();
            entity.Property(x => x.SourceSupplierProductId).HasMaxLength(120);
            entity.Property(x => x.SupplierPartNumber).HasMaxLength(120);
            entity.Property(x => x.SupplierSku).HasMaxLength(120).IsRequired();
            entity.Property(x => x.SourceFitmentItemId).HasMaxLength(120);
            entity.Property(x => x.SourceFitmentPartNumber).HasMaxLength(120);
            entity.Property(x => x.MfgPartNumber).HasMaxLength(120);
            entity.Property(x => x.VehicleClass).HasMaxLength(80);
            entity.Property(x => x.VehicleType).HasMaxLength(120);
            entity.Property(x => x.Make).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Model).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Submodel).HasMaxLength(160);
            entity.Property(x => x.Engine).HasMaxLength(160);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.ResolutionStatus).HasMaxLength(80).IsRequired();
            entity.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId);
            entity.HasOne<SupplierProduct>().WithMany().HasForeignKey(x => x.SupplierProductId);
            entity.HasOne<GlobalProduct>().WithMany().HasForeignKey(x => x.GlobalProductId);
            entity.HasOne<GlobalVehicle>().WithMany().HasForeignKey(x => x.GlobalVehicleId);
            entity.HasOne<VehicleFitment>().WithMany().HasForeignKey(x => x.VehicleFitmentId);
        });

        modelBuilder.Entity<ProductMatchReviewItem>(entity =>
        {
            entity.ToTable("product_match_review_items");
            entity.HasIndex(x => new { x.SupplierId, x.SupplierSku, x.Status });
            entity.Property(x => x.SupplierSku).HasMaxLength(120).IsRequired();
            entity.Property(x => x.SupplierPartNumber).HasMaxLength(120);
            entity.Property(x => x.Upc).HasMaxLength(80);
            entity.Property(x => x.Brand).HasMaxLength(120);
            entity.Property(x => x.SupplierDescription).HasMaxLength(1000);
            entity.Property(x => x.MatchReason).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId);
            entity.HasOne<GlobalProduct>().WithMany().HasForeignKey(x => x.CandidateGlobalProductId);
        });

        modelBuilder.Entity<SupplierConnectorConfiguration>(entity =>
        {
            entity.ToTable("supplier_connector_configurations");
            entity.HasIndex(x => new { x.SupplierId, x.ConnectorKey }).IsUnique();
            entity.Property(x => x.ConnectorKey).HasMaxLength(80).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.BaseApiUrl).HasMaxLength(1000);
            entity.Property(x => x.MasterFileUrl).HasMaxLength(1000);
            entity.Property(x => x.DealerAccountNumber).HasMaxLength(120);
            entity.Property(x => x.Username).HasMaxLength(160);
            entity.Property(x => x.ApiKey).HasMaxLength(500);
            entity.Property(x => x.ApiSecretProtected);
            entity.Property(x => x.AuthMode).HasMaxLength(80).IsRequired();
            entity.Property(x => x.MasterFileImportMode).HasMaxLength(80);
            entity.Property(x => x.FitmentSourceBaseUrl).HasMaxLength(1000);
            entity.Property(x => x.MediaScheduleCadenceMinutes).HasDefaultValue(1440);
            entity.Property(x => x.MediaScheduleDelayMilliseconds).HasDefaultValue(750);
            entity.Property(x => x.LastConnectionStatus).HasMaxLength(80);
            entity.Property(x => x.LastConnectionMessage).HasMaxLength(500);
            entity.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId);
        });

        modelBuilder.Entity<SupplierConnectorImportRun>(entity =>
        {
            entity.ToTable("supplier_connector_import_runs");
            entity.HasIndex(x => new { x.SupplierConnectorConfigurationId, x.RequestedAtUtc });
            entity.HasIndex(x => x.Status);
            entity.Property(x => x.ImportType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.RequestedByPlatformUserId).HasMaxLength(80);
            entity.Property(x => x.Source).HasMaxLength(160);
            entity.Property(x => x.ParametersJson).HasColumnType("jsonb");
            entity.Property(x => x.Message).HasMaxLength(1000);
            entity.Property(x => x.ProgressProcessed).HasDefaultValue(0);
            entity.HasOne<SupplierConnectorConfiguration>().WithMany().HasForeignKey(x => x.SupplierConnectorConfigurationId);
        });

        modelBuilder.Entity<CompanySupplierConnectorConfiguration>(entity =>
        {
            entity.ToTable("company_supplier_connector_configurations");
            entity.HasIndex(x => new { x.OrganizationId, x.SupplierId, x.ConnectorKey }).IsUnique();
            entity.Property(x => x.ConnectorKey).HasMaxLength(80).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.BaseApiUrl).HasMaxLength(1000);
            entity.Property(x => x.DealerAccountNumber).HasMaxLength(120);
            entity.Property(x => x.Username).HasMaxLength(160);
            entity.Property(x => x.ApiKey).HasMaxLength(500);
            entity.Property(x => x.ApiSecretProtected).HasMaxLength(1000);
            entity.Property(x => x.AuthMode).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LastConnectionStatus).HasMaxLength(80);
            entity.Property(x => x.LastConnectionMessage).HasMaxLength(500);
            entity.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<CompanySupplierConnectorImportRun>(entity =>
        {
            entity.ToTable("company_supplier_connector_import_runs");
            entity.HasIndex(x => new { x.OrganizationId, x.CompanySupplierConnectorConfigurationId, x.RequestedAtUtc });
            entity.HasIndex(x => new { x.OrganizationId, x.Status });
            entity.Property(x => x.ImportType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.RequestedByUserId).HasMaxLength(80);
            entity.Property(x => x.Source).HasMaxLength(160);
            entity.Property(x => x.ParametersJson).HasColumnType("jsonb");
            entity.Property(x => x.Message).HasMaxLength(1000);
            entity.Property(x => x.ProgressProcessed).HasDefaultValue(0);
            entity.HasOne<CompanySupplierConnectorConfiguration>().WithMany().HasForeignKey(x => x.CompanySupplierConnectorConfigurationId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<CompanySupplierPrice>(entity =>
        {
            entity.ToTable("company_supplier_prices");
            entity.HasIndex(x => new { x.OrganizationId, x.SupplierProductId }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.SupplierId, x.SupplierSku });
            entity.Property(x => x.SupplierSku).HasMaxLength(120).IsRequired();
            entity.Property(x => x.SourceSupplierProductId).HasMaxLength(120);
            entity.Property(x => x.ActualDealerCost).HasPrecision(12, 2);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.SourceDataJson).HasColumnType("jsonb");
            entity.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId);
            entity.HasOne<SupplierProduct>().WithMany().HasForeignKey(x => x.SupplierProductId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<ManufacturerPart>(entity =>
        {
            entity.ToTable("manufacturer_parts");
            entity.HasIndex(x => new { x.OrganizationId, x.Brand, x.ManufacturerPartNumber }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.Upc });
            entity.HasIndex(x => new { x.OrganizationId, x.Description });
            entity.Property(x => x.ManufacturerPartNumber).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Brand).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Upc).HasMaxLength(80);
            entity.Property(x => x.Category).HasMaxLength(160);
            entity.Property(x => x.Subcategory).HasMaxLength(160);
            entity.Property(x => x.Weight).HasPrecision(12, 3);
            entity.Property(x => x.Length).HasPrecision(12, 3);
            entity.Property(x => x.Width).HasPrecision(12, 3);
            entity.Property(x => x.Height).HasPrecision(12, 3);
            entity.Property(x => x.Msrp).HasPrecision(12, 2);
            entity.Property(x => x.Map).HasPrecision(12, 2);
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.HasOne<ManufacturerPart>().WithMany().HasForeignKey(x => x.SupersededByManufacturerPartId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<ManufacturerPartImage>(entity =>
        {
            entity.ToTable("manufacturer_part_images");
            entity.HasIndex(x => new { x.OrganizationId, x.ManufacturerPartId, x.SortOrder });
            entity.Property(x => x.Url).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.AltText).HasMaxLength(300);
            entity.HasOne<ManufacturerPart>().WithMany().HasForeignKey(x => x.ManufacturerPartId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<ManufacturerPartCrossReference>(entity =>
        {
            entity.ToTable("manufacturer_part_cross_references");
            entity.HasIndex(x => new { x.OrganizationId, x.ManufacturerPartId });
            entity.HasIndex(x => new { x.OrganizationId, x.ReferenceValue });
            entity.Property(x => x.ReferenceType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ReferenceValue).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Brand).HasMaxLength(120);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasOne<ManufacturerPart>().WithMany().HasForeignKey(x => x.ManufacturerPartId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<SupplierListing>(entity =>
        {
            entity.ToTable("supplier_listings");
            entity.HasIndex(x => new { x.OrganizationId, x.Supplier, x.SupplierSku }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.ManufacturerPartId });
            entity.Property(x => x.Supplier).HasMaxLength(120).IsRequired();
            entity.Property(x => x.SupplierSku).HasMaxLength(120).IsRequired();
            entity.Property(x => x.SupplierCost).HasPrecision(12, 2);
            entity.Property(x => x.SupplierMsrp).HasPrecision(12, 2);
            entity.Property(x => x.WarehouseAvailability).HasMaxLength(120).IsRequired();
            entity.Property(x => x.FreightClass).HasMaxLength(80);
            entity.HasOne<ManufacturerPart>().WithMany().HasForeignKey(x => x.ManufacturerPartId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.ToTable("inventory_items");
            entity.HasIndex(x => new { x.OrganizationId, x.LocationId, x.ManufacturerPartId }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.BinLocation });
            entity.Property(x => x.BinLocation).HasMaxLength(120);
            entity.Property(x => x.AverageCost).HasPrecision(12, 2);
            entity.Property(x => x.LastCost).HasPrecision(12, 2);
            entity.HasOne<ManufacturerPart>().WithMany().HasForeignKey(x => x.ManufacturerPartId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<CompanyInventoryItem>(entity =>
        {
            entity.ToTable("company_inventory_items");
            entity.HasIndex(x => new { x.OrganizationId, x.Sku });
            entity.HasIndex(x => new { x.OrganizationId, x.ManufacturerPartNumber });
            entity.HasIndex(x => new { x.OrganizationId, x.NormalizedManufacturerPartNumber });
            entity.HasIndex(x => new { x.OrganizationId, x.Upc });
            entity.HasIndex(x => new { x.OrganizationId, x.Brand });
            entity.HasIndex(x => new { x.OrganizationId, x.Category });
            entity.HasIndex(x => new { x.OrganizationId, x.SupplierProductId });
            entity.HasIndex(x => new { x.OrganizationId, x.Status });
            entity.Property(x => x.SourceType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SourceSupplierCode).HasMaxLength(40);
            entity.Property(x => x.SourceSupplierName).HasMaxLength(160);
            entity.Property(x => x.SourceSupplierSku).HasMaxLength(120);
            entity.Property(x => x.SourceSupplierProductId).HasMaxLength(120);
            entity.Property(x => x.Sku).HasMaxLength(120);
            entity.Property(x => x.ManufacturerPartNumber).HasMaxLength(120);
            entity.Property(x => x.NormalizedManufacturerPartNumber).HasMaxLength(120);
            entity.Property(x => x.Upc).HasMaxLength(80);
            entity.Property(x => x.Brand).HasMaxLength(120);
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.Category).HasMaxLength(160);
            entity.Property(x => x.Subcategory).HasMaxLength(160);
            entity.Property(x => x.ImageUrl).HasMaxLength(1000);
            entity.Property(x => x.RetailPrice).HasPrecision(12, 2);
            entity.Property(x => x.SalePrice).HasPrecision(12, 2);
            entity.Property(x => x.DefaultCost).HasPrecision(12, 2);
            entity.Property(x => x.AverageCost).HasPrecision(12, 2);
            entity.Property(x => x.LastCost).HasPrecision(12, 2);
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne<GlobalProduct>().WithMany().HasForeignKey(x => x.GlobalProductId);
            entity.HasOne<SupplierProduct>().WithMany().HasForeignKey(x => x.SupplierProductId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<CompanyInventoryLocationStock>(entity =>
        {
            entity.ToTable("company_inventory_location_stocks");
            entity.HasIndex(x => new { x.OrganizationId, x.CompanyInventoryItemId, x.LocationId }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.LocationId });
            entity.HasIndex(x => new { x.OrganizationId, x.StockInStore });
            entity.Property(x => x.LocationNameSnapshot).HasMaxLength(200);
            entity.Property(x => x.BinLocation).HasMaxLength(120);
            entity.Property(x => x.QuantityOnHand).HasPrecision(12, 2);
            entity.Property(x => x.QuantityAllocated).HasPrecision(12, 2);
            entity.Property(x => x.QuantityAvailable).HasPrecision(12, 2);
            entity.Property(x => x.QuantityOnOrder).HasPrecision(12, 2);
            entity.Property(x => x.QuantityBackordered).HasPrecision(12, 2);
            entity.Property(x => x.MinQuantity).HasPrecision(12, 2);
            entity.Property(x => x.MaxQuantity).HasPrecision(12, 2);
            entity.Property(x => x.ReorderPoint).HasPrecision(12, 2);
            entity.Property(x => x.ReorderQuantity).HasPrecision(12, 2);
            entity.HasOne<CompanyInventoryItem>().WithMany().HasForeignKey(x => x.CompanyInventoryItemId);
            entity.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<CompanyInventoryMovement>(entity =>
        {
            entity.ToTable("company_inventory_movements");
            entity.HasIndex(x => new { x.OrganizationId, x.CompanyInventoryItemId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.OrganizationId, x.LocationId, x.CreatedAtUtc });
            entity.Property(x => x.MovementType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.QuantityDelta).HasPrecision(12, 2);
            entity.Property(x => x.QuantityAfter).HasPrecision(12, 2);
            entity.Property(x => x.UnitCost).HasPrecision(12, 2);
            entity.Property(x => x.ReferenceType).HasMaxLength(80);
            entity.Property(x => x.ReferenceId).HasMaxLength(120);
            entity.Property(x => x.Reason).HasMaxLength(160);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne<CompanyInventoryItem>().WithMany().HasForeignKey(x => x.CompanyInventoryItemId);
            entity.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.ToTable("payment_transactions");
            entity.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.OrganizationId, x.GatewayTransactionId });
            entity.HasIndex(x => new { x.OrganizationId, x.Status });
            entity.Property(x => x.Provider).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Environment).HasMaxLength(40).IsRequired();
            entity.Property(x => x.TransactionType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.PaymentMethod).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(12, 2);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.GatewayTransactionId).HasMaxLength(120);
            entity.Property(x => x.AuthorizationCode).HasMaxLength(80);
            entity.Property(x => x.ResponseCode).HasMaxLength(40);
            entity.Property(x => x.ResponseText).HasMaxLength(1000);
            entity.Property(x => x.OrderId).HasMaxLength(120);
            entity.Property(x => x.ReferenceType).HasMaxLength(80);
            entity.Property(x => x.ReferenceId).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.CustomerName).HasMaxLength(200);
            entity.Property(x => x.CustomerEmail).HasMaxLength(320);
            entity.Property(x => x.CustomerPhone).HasMaxLength(40);
            entity.Property(x => x.CardBrand).HasMaxLength(40);
            entity.Property(x => x.CardLastFour).HasMaxLength(4);
            entity.Property(x => x.RequestCorrelationId).HasMaxLength(80);
            entity.Property(x => x.RawResponseJson).HasColumnType("text");
            entity.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<FinancialLedgerEntry>(entity =>
        {
            entity.ToTable("financial_ledger_entries");
            entity.HasIndex(x => new { x.OrganizationId, x.OccurredAtUtc });
            entity.HasIndex(x => new { x.OrganizationId, x.SourceType, x.SourceId });
            entity.HasIndex(x => new { x.OrganizationId, x.EntryType });
            entity.Property(x => x.EntryType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Direction).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(12, 2);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SourceModule).HasMaxLength(80).IsRequired();
            entity.Property(x => x.SourceType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.SourceId).HasMaxLength(120).IsRequired();
            entity.Property(x => x.ReferenceType).HasMaxLength(80);
            entity.Property(x => x.ReferenceId).HasMaxLength(120);
            entity.Property(x => x.Provider).HasMaxLength(40);
            entity.Property(x => x.ProviderTransactionId).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.CorrelationId).HasMaxLength(80);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<MerchantAccount>(entity =>
        {
            entity.ToTable("merchant_accounts");
            entity.HasIndex(x => x.OrganizationId).IsUnique();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.UnderwritingStatus).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LegalBusinessName).HasMaxLength(200);
            entity.Property(x => x.Dba).HasMaxLength(200);
            entity.Property(x => x.Ein).HasMaxLength(20);
            entity.Property(x => x.TaxIdentifierLastFour).HasMaxLength(4);
            entity.Property(x => x.TaxIdentifierProtected).HasMaxLength(2000);
            entity.Property(x => x.BusinessType).HasMaxLength(120);
            entity.Property(x => x.BusinessDescription).HasMaxLength(500);
            entity.Property(x => x.PhysicalAddressLine1).HasMaxLength(200);
            entity.Property(x => x.PhysicalAddressLine2).HasMaxLength(200);
            entity.Property(x => x.PhysicalCity).HasMaxLength(120);
            entity.Property(x => x.PhysicalRegion).HasMaxLength(80);
            entity.Property(x => x.PhysicalPostalCode).HasMaxLength(20);
            entity.Property(x => x.PhysicalCountry).HasMaxLength(2);
            entity.Property(x => x.MailingAddressLine1).HasMaxLength(200);
            entity.Property(x => x.MailingAddressLine2).HasMaxLength(200);
            entity.Property(x => x.MailingCity).HasMaxLength(120);
            entity.Property(x => x.MailingRegion).HasMaxLength(80);
            entity.Property(x => x.MailingPostalCode).HasMaxLength(20);
            entity.Property(x => x.MailingCountry).HasMaxLength(2);
            entity.Property(x => x.OwnerName).HasMaxLength(200);
            entity.Property(x => x.OwnerEmail).HasMaxLength(320);
            entity.Property(x => x.OwnerPhone).HasMaxLength(40);
            entity.Property(x => x.OwnerTitle).HasMaxLength(120);
            entity.Property(x => x.OwnerOwnershipPercentage).HasPrecision(5, 2);
            entity.Property(x => x.OwnerDateOfBirthProtected).HasMaxLength(2000);
            entity.Property(x => x.OwnerSsnLastFour).HasMaxLength(4);
            entity.Property(x => x.OwnerSsnProtected).HasMaxLength(2000);
            entity.Property(x => x.BankName).HasMaxLength(160);
            entity.Property(x => x.BankRoutingLastFour).HasMaxLength(4);
            entity.Property(x => x.BankRoutingNumberProtected).HasMaxLength(2000);
            entity.Property(x => x.BankAccountLastFour).HasMaxLength(4);
            entity.Property(x => x.BankAccountNumberProtected).HasMaxLength(2000);
            entity.Property(x => x.ExpectedMonthlyVolume).HasPrecision(12, 2);
            entity.Property(x => x.AverageTicket).HasPrecision(12, 2);
            entity.Property(x => x.HighTicket).HasPrecision(12, 2);
            entity.Property(x => x.CardPresentPercentage).HasPrecision(5, 2);
            entity.Property(x => x.KeyEnteredPercentage).HasPrecision(5, 2);
            entity.Property(x => x.EcommercePercentage).HasPrecision(5, 2);
            entity.Property(x => x.MotoPercentage).HasPrecision(5, 2);
            entity.Property(x => x.Website).HasMaxLength(300);
            entity.Property(x => x.Mcc).HasMaxLength(10);
            entity.Property(x => x.ProcessingProfile).HasMaxLength(120);
            entity.Property(x => x.SettlementSchedule).HasMaxLength(120);
            entity.Property(x => x.PrimaryProviderCode).HasMaxLength(40);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<MerchantProviderRelationship>(entity =>
        {
            entity.ToTable("merchant_provider_relationships");
            entity.HasIndex(x => new { x.OrganizationId, x.ProviderCode }).IsUnique();
            entity.Property(x => x.ProviderCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ProviderMerchantId).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.CapabilitiesJson).HasColumnType("jsonb");
            entity.Property(x => x.ProviderReference).HasMaxLength(160);
            entity.Property(x => x.ApplicationCreateIdempotencyKey).HasMaxLength(100);
            entity.Property(x => x.ApplicationSubmitIdempotencyKey).HasMaxLength(100);
            entity.Property(x => x.PaymentCredentialIdempotencyKey).HasMaxLength(100);
            entity.Property(x => x.TokenizationCredentialIdempotencyKey).HasMaxLength(100);
            entity.Property(x => x.LegalConsentUrlProtected).HasMaxLength(4000);
            entity.Property(x => x.CredentialProvisioningStatus).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LastProviderError).HasMaxLength(1000);
            entity.Property(x => x.SupportNotes).HasMaxLength(2000);
            entity.Property(x => x.GatewayUsername).HasMaxLength(160);
            entity.Property(x => x.GatewayPasswordProtected).HasMaxLength(500);
            entity.Property(x => x.PaymentApiKeyProtected).HasMaxLength(500);
            entity.Property(x => x.QueryApiKeyProtected).HasMaxLength(500);
            entity.Property(x => x.PublicTokenizationKeyProtected).HasColumnName("PublicTokenizationKey").HasMaxLength(2000);
            entity.Property(x => x.CredentialMetadataJson).HasColumnType("jsonb");
            entity.HasOne<MerchantAccount>().WithMany().HasForeignKey(x => x.MerchantAccountId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<MerchantAccountStatusHistory>(entity =>
        {
            entity.ToTable("merchant_account_status_history");
            entity.HasIndex(x => new { x.OrganizationId, x.MerchantAccountId, x.CreatedAtUtc });
            entity.Property(x => x.OldStatus).HasMaxLength(80);
            entity.Property(x => x.NewStatus).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(1000);
            entity.Property(x => x.ProviderCode).HasMaxLength(40);
            entity.Property(x => x.ProviderReference).HasMaxLength(160);
            entity.Property(x => x.CreatedByUserId).HasMaxLength(80);
            entity.HasOne<MerchantAccount>().WithMany().HasForeignKey(x => x.MerchantAccountId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<CustomerWallet>(entity =>
        {
            entity.ToTable("customer_wallets");
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerId }).IsUnique();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<WalletPaymentMethod>(entity =>
        {
            entity.ToTable("wallet_payment_methods");
            entity.HasIndex(x => new { x.OrganizationId, x.WalletId });
            entity.HasIndex(x => new { x.OrganizationId, x.ProviderCode, x.ProviderToken }).IsUnique();
            entity.Property(x => x.ProviderCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ProviderToken).HasMaxLength(500).IsRequired();
            entity.Property(x => x.MethodType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.DisplayBrand).HasMaxLength(80);
            entity.Property(x => x.LastFour).HasMaxLength(4);
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.HasOne<CustomerWallet>().WithMany().HasForeignKey(x => x.WalletId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<PaymentTerminal>(entity =>
        {
            entity.ToTable("payment_terminals");
            entity.HasIndex(x => new { x.OrganizationId, x.ProviderCode, x.ProviderTerminalId }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.LocationId });
            entity.Property(x => x.ProviderCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.DeviceType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Model).HasMaxLength(120).IsRequired();
            entity.Property(x => x.ProviderTerminalId).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.AssignedRegister).HasMaxLength(120);
            entity.Property(x => x.AssignedEmployeeId).HasMaxLength(80);
            entity.Property(x => x.FirmwareVersion).HasMaxLength(120);
            entity.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<FinancialProviderConnection>(entity =>
        {
            entity.ToTable("financial_provider_connections");
            entity.HasIndex(x => new { x.OrganizationId, x.ProviderCode, x.ProviderType }).IsUnique();
            entity.Property(x => x.ProviderCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ProviderType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.CapabilitiesJson).HasColumnType("jsonb");
            entity.Property(x => x.ConfigurationReference).HasMaxLength(200);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<SubscriptionAgreement>(entity =>
        {
            entity.ToTable("subscription_agreements");
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerId });
            entity.Property(x => x.PlanName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(12, 2);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.BillingCadence).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<BankingConnection>(entity =>
        {
            entity.ToTable("banking_connections");
            entity.HasIndex(x => new { x.OrganizationId, x.ProviderCode });
            entity.Property(x => x.ProviderCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.AccountDescriptor).HasMaxLength(160);
            entity.Property(x => x.ProviderToken).HasMaxLength(500);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<FinancingApplication>(entity =>
        {
            entity.ToTable("financing_applications");
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerId });
            entity.Property(x => x.RequestedAmount).HasPrecision(12, 2);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ProviderCode).HasMaxLength(40);
            entity.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<AccountingExportBatch>(entity =>
        {
            entity.ToTable("accounting_export_batches");
            entity.HasIndex(x => new { x.OrganizationId, x.PeriodStartUtc, x.PeriodEndUtc });
            entity.Property(x => x.ProviderCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ExportReference).HasMaxLength(160);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<PlatformUser>(entity =>
        {
            entity.ToTable("platform_users");
            entity.HasIndex(x => x.NormalizedEmail).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(80).IsRequired();
        });

        modelBuilder.Entity<PlatformTenant>(entity =>
        {
            entity.ToTable("platform_tenants");
            entity.HasIndex(x => x.OrganizationId).IsUnique();
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.Property(x => x.OrganizationName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.SubscriptionPlan).HasMaxLength(120).IsRequired();
            entity.Property(x => x.CurrentVersion).HasMaxLength(40).IsRequired();
            entity.Property(x => x.HealthStatus).HasMaxLength(80).IsRequired();
            entity.Property(x => x.BillingStatus).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ProvisioningStatus).HasMaxLength(80).IsRequired();
        });

        modelBuilder.Entity<PlatformSubscription>(entity =>
        {
            entity.ToTable("platform_subscriptions");
            entity.HasIndex(x => x.OrganizationId).IsUnique();
            entity.Property(x => x.Plan).HasMaxLength(120).IsRequired();
            entity.Property(x => x.BillingStatus).HasMaxLength(80).IsRequired();
            entity.Property(x => x.BillingProviderCustomerId).HasMaxLength(160);
        });

        modelBuilder.Entity<TenantModuleEntitlement>(entity =>
        {
            entity.ToTable("tenant_module_entitlements");
            entity.HasIndex(x => new { x.OrganizationId, x.ModuleKey }).IsUnique();
            entity.Property(x => x.ModuleKey).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EnabledByPlatformUserId).HasMaxLength(80);
        });

        modelBuilder.Entity<PlatformEvent>(entity =>
        {
            entity.ToTable("platform_events");
            entity.HasIndex(x => new { x.TargetOrganizationId, x.OccurredAtUtc });
            entity.HasIndex(x => x.EventType);
            entity.HasIndex(x => x.CorrelationId);
            entity.Property(x => x.ActorPlatformUserId).HasMaxLength(80);
            entity.Property(x => x.EventType).HasMaxLength(160).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.CorrelationId).HasMaxLength(80).IsRequired();
        });

        modelBuilder.Entity<PlatformAuditEvent>(entity =>
        {
            entity.ToTable("platform_audit_events");
            entity.HasIndex(x => new { x.TargetOrganizationId, x.OccurredAtUtc });
            entity.Property(x => x.ActorPlatformUserId).HasMaxLength(80);
            entity.Property(x => x.Action).HasMaxLength(160).IsRequired();
            entity.Property(x => x.PreviousValuesJson).HasColumnType("jsonb");
            entity.Property(x => x.NewValuesJson).HasColumnType("jsonb");
            entity.Property(x => x.IpAddress).HasMaxLength(80);
        });

        modelBuilder.Entity<WelcomeEmail>(entity =>
        {
            entity.ToTable("welcome_emails");
            entity.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc });
            entity.Property(x => x.RecipientEmail).HasMaxLength(320).IsRequired();
            entity.Property(x => x.RecipientName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        });

        modelBuilder.Entity<MarketingPage>(entity =>
        {
            entity.ToTable("marketing_pages");
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => new { x.IsPublished, x.SortOrder });
            entity.Property(x => x.Slug).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(240).IsRequired();
            entity.Property(x => x.NavigationLabel).HasMaxLength(120).IsRequired();
            entity.Property(x => x.MetaDescription).HasMaxLength(500);
            entity.Property(x => x.OpenGraphTitle).HasMaxLength(240);
            entity.Property(x => x.OpenGraphDescription).HasMaxLength(500);
            entity.Property(x => x.OpenGraphImageUrl).HasMaxLength(1000);
            entity.Property(x => x.CanonicalUrl).HasMaxLength(1000);
            entity.Property(x => x.RawHtmlBody).HasColumnType("text");
        });

        modelBuilder.Entity<MarketingContentBlock>(entity =>
        {
            entity.ToTable("marketing_content_blocks");
            entity.HasIndex(x => new { x.MarketingPageId, x.SortOrder });
            entity.Property(x => x.BlockType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Eyebrow).HasMaxLength(120);
            entity.Property(x => x.Heading).HasMaxLength(260).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(4000);
            entity.Property(x => x.PrimaryActionLabel).HasMaxLength(120);
            entity.Property(x => x.PrimaryActionUrl).HasMaxLength(1000);
            entity.Property(x => x.SecondaryActionLabel).HasMaxLength(120);
            entity.Property(x => x.SecondaryActionUrl).HasMaxLength(1000);
            entity.Property(x => x.ItemsJson).HasColumnType("jsonb");
            entity.HasOne<MarketingPage>().WithMany(x => x.Blocks).HasForeignKey(x => x.MarketingPageId);
        });

        modelBuilder.Entity<MarketingLead>(entity =>
        {
            entity.ToTable("marketing_leads");
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.Company).HasMaxLength(220);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.Message).HasMaxLength(2000);
            entity.Property(x => x.Source).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.CreatedByUserId = currentUser.UserId;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
                entry.Entity.UpdatedByUserId = currentUser.UserId;
            }
        }

        foreach (var entry in ChangeTracker.Entries<IOrganizationOwned>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (entry.Entity.OrganizationId == Guid.Empty)
            {
                if (tenantProvider.CurrentOrganizationId is null)
                {
                    throw new InvalidOperationException($"{entry.Entity.GetType().Name} requires an OrganizationId.");
                }

                entry.Entity.OrganizationId = tenantProvider.CurrentOrganizationId.Value;
            }

            if (tenantProvider.CurrentOrganizationId is not null && entry.Entity.OrganizationId != tenantProvider.CurrentOrganizationId)
            {
                throw new InvalidOperationException($"{entry.Entity.GetType().Name} belongs to a different organization.");
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
