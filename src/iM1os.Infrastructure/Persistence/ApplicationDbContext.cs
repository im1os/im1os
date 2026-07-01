using iM1os.Application.Common;
using iM1os.Application.Tenancy;
using iM1os.Domain.Audit;
using iM1os.Domain.Common;
using iM1os.Domain.Configuration;
using iM1os.Domain.Customers;
using iM1os.Domain.Employees;
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
            entity.Property(x => x.Length).HasPrecision(12, 3);
            entity.Property(x => x.Width).HasPrecision(12, 3);
            entity.Property(x => x.Height).HasPrecision(12, 3);
            entity.Property(x => x.Weight).HasPrecision(12, 3);
            entity.Property(x => x.ImagesJson).HasColumnType("jsonb");
            entity.Property(x => x.SpecificationsJson).HasColumnType("jsonb");
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
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
            entity.HasIndex(x => x.SupplierPartNumber);
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
