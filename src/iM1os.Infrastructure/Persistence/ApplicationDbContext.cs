using iM1os.Application.Common;
using iM1os.Application.Tenancy;
using iM1os.Domain.Audit;
using iM1os.Domain.Common;
using iM1os.Domain.Configuration;
using iM1os.Domain.Customers;
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

    public DbSet<CustomerVehicle> CustomerVehicles => Set<CustomerVehicle>();

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    public DbSet<Estimate> Estimates => Set<Estimate>();

    public DbSet<LaborOperation> LaborOperations => Set<LaborOperation>();

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
            entity.HasIndex(x => new { x.OrganizationId, x.Email });
            entity.HasIndex(x => new { x.OrganizationId, x.Phone });
            entity.Property(x => x.DisplayName).HasMaxLength(220).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(120);
            entity.Property(x => x.LastName).HasMaxLength(120);
            entity.Property(x => x.CompanyName).HasMaxLength(220);
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<CustomerVehicle>(entity =>
        {
            entity.ToTable("customer_vehicles");
            entity.HasIndex(x => new { x.OrganizationId, x.CustomerId });
            entity.HasIndex(x => new { x.OrganizationId, x.Vin });
            entity.Property(x => x.Make).HasMaxLength(120);
            entity.Property(x => x.Model).HasMaxLength(160);
            entity.Property(x => x.Trim).HasMaxLength(120);
            entity.Property(x => x.Vin).HasMaxLength(80);
            entity.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<WorkOrder>(entity =>
        {
            entity.ToTable("work_orders");
            entity.HasIndex(x => new { x.OrganizationId, x.WorkOrderNumber }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.LocationId, x.Stage });
            entity.Property(x => x.WorkOrderNumber).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Stage).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Priority).HasMaxLength(80).IsRequired();
            entity.Property(x => x.RequestedService).HasMaxLength(2000);
            entity.Property(x => x.CustomerConcern).HasMaxLength(2000);
            entity.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
            entity.HasOne<CustomerVehicle>().WithMany().HasForeignKey(x => x.CustomerVehicleId);
            entity.HasQueryFilter(x => tenantProvider.CurrentOrganizationId == null || x.OrganizationId == tenantProvider.CurrentOrganizationId);
        });

        modelBuilder.Entity<Estimate>(entity =>
        {
            entity.ToTable("estimates");
            entity.HasIndex(x => new { x.OrganizationId, x.EstimateNumber }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.WorkOrderId });
            entity.Property(x => x.EstimateNumber).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LaborTotal).HasPrecision(12, 2);
            entity.Property(x => x.PartsTotal).HasPrecision(12, 2);
            entity.Property(x => x.TaxTotal).HasPrecision(12, 2);
            entity.Property(x => x.GrandTotal).HasPrecision(12, 2);
            entity.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId);
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
