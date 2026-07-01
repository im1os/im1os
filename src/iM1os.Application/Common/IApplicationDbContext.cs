using iM1os.Domain.Audit;
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

namespace iM1os.Application.Common;

public interface IApplicationDbContext
{
    DbSet<Organization> Organizations { get; }

    DbSet<Location> Locations { get; }

    DbSet<ApplicationUser> Users { get; }

    DbSet<Employee> Employees { get; }

    DbSet<EmployeeCompensation> EmployeeCompensations { get; }

    DbSet<EmployeeDocument> EmployeeDocuments { get; }

    DbSet<UserInvitation> UserInvitations { get; }

    DbSet<PasswordResetRequest> PasswordResetRequests { get; }

    DbSet<OrganizationMembership> OrganizationMemberships { get; }

    DbSet<Role> Roles { get; }

    DbSet<Permission> Permissions { get; }

    DbSet<UserRole> UserRoles { get; }

    DbSet<RolePermission> RolePermissions { get; }

    DbSet<UserPermissionOverride> UserPermissionOverrides { get; }

    DbSet<UserSession> UserSessions { get; }

    DbSet<FeatureFlag> FeatureFlags { get; }

    DbSet<ApplicationSetting> ApplicationSettings { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<DomainEventRecord> DomainEvents { get; }

    DbSet<TimelineEvent> TimelineEvents { get; }

    DbSet<TenantIdentityEvent> TenantIdentityEvents { get; }

    DbSet<BusinessOnboarding> BusinessOnboardings { get; }

    DbSet<BusinessConfiguration> BusinessConfigurations { get; }

    DbSet<Customer> Customers { get; }

    DbSet<CustomerAddress> CustomerAddresses { get; }

    DbSet<CustomerPhoneNumber> CustomerPhoneNumbers { get; }

    DbSet<CustomerNote> CustomerNotes { get; }

    DbSet<CustomerTag> CustomerTags { get; }

    DbSet<CustomerCustomField> CustomerCustomFields { get; }

    DbSet<CustomerExternalLink> CustomerExternalLinks { get; }

    DbSet<CustomerDocument> CustomerDocuments { get; }

    DbSet<CustomerVehicle> CustomerVehicles { get; }

    DbSet<CustomerVehicleAttachment> CustomerVehicleAttachments { get; }

    DbSet<WorkOrder> WorkOrders { get; }

    DbSet<WorkOrderAttachment> WorkOrderAttachments { get; }

    DbSet<Estimate> Estimates { get; }

    DbSet<EstimateLineItem> EstimateLineItems { get; }

    DbSet<LaborOperation> LaborOperations { get; }

    DbSet<WorkOrderTechnicianAssignment> WorkOrderTechnicianAssignments { get; }

    DbSet<GlobalProduct> GlobalProducts { get; }

    DbSet<GlobalVehicle> GlobalVehicles { get; }

    DbSet<Supplier> Suppliers { get; }

    DbSet<SupplierProduct> SupplierProducts { get; }

    DbSet<SupplierPrice> SupplierPrices { get; }

    DbSet<VehicleFitment> VehicleFitments { get; }

    DbSet<SupplierFitmentRecord> SupplierFitmentRecords { get; }

    DbSet<ProductMatchReviewItem> ProductMatchReviewItems { get; }

    DbSet<SupplierConnectorConfiguration> SupplierConnectorConfigurations { get; }

    DbSet<SupplierConnectorImportRun> SupplierConnectorImportRuns { get; }

    DbSet<CompanySupplierConnectorConfiguration> CompanySupplierConnectorConfigurations { get; }

    DbSet<CompanySupplierConnectorImportRun> CompanySupplierConnectorImportRuns { get; }

    DbSet<CompanySupplierPrice> CompanySupplierPrices { get; }

    DbSet<ManufacturerPart> ManufacturerParts { get; }

    DbSet<ManufacturerPartImage> ManufacturerPartImages { get; }

    DbSet<ManufacturerPartCrossReference> ManufacturerPartCrossReferences { get; }

    DbSet<SupplierListing> SupplierListings { get; }

    DbSet<InventoryItem> InventoryItems { get; }

    DbSet<PlatformUser> PlatformUsers { get; }

    DbSet<PlatformTenant> PlatformTenants { get; }

    DbSet<PlatformSubscription> PlatformSubscriptions { get; }

    DbSet<TenantModuleEntitlement> TenantModuleEntitlements { get; }

    DbSet<PlatformEvent> PlatformEvents { get; }

    DbSet<PlatformAuditEvent> PlatformAuditEvents { get; }

    DbSet<WelcomeEmail> WelcomeEmails { get; }

    DbSet<MarketingPage> MarketingPages { get; }

    DbSet<MarketingContentBlock> MarketingContentBlocks { get; }

    DbSet<MarketingLead> MarketingLeads { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
