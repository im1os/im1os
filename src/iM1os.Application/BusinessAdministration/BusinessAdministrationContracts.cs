namespace iM1os.Application.BusinessAdministration;

public sealed record BusinessAdministrationWorkspace(
    Guid OrganizationId,
    BusinessProfileDto Profile,
    BusinessConfigurationDto Configuration,
    IReadOnlyCollection<LocationDto> Locations,
    IReadOnlyCollection<EmployeeDto> Employees,
    IReadOnlyCollection<RoleDto> Roles,
    IReadOnlyCollection<ConnectorDto> Connectors,
    IReadOnlyCollection<string> RecentActivity,
    int SetupProgress,
    bool IsReadyForOperations);

public sealed record BusinessProfileDto(
    string BusinessName,
    string? LegalName,
    string? Dba,
    string? LogoUrl,
    string? Website,
    string? Phone,
    string? Email,
    string? TaxId,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Region,
    string? PostalCode,
    string? Country,
    string TimeZone,
    string Language,
    string Currency,
    string DateFormat,
    string TimeFormat);

public sealed record UpdateBusinessProfileRequest(
    string BusinessName,
    string? LegalName,
    string? Dba,
    string? LogoUrl,
    string? Website,
    string? Phone,
    string? Email,
    string? TaxId,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Region,
    string? PostalCode,
    string? Country,
    string TimeZone,
    string Language,
    string Currency,
    string DateFormat,
    string TimeFormat);

public sealed record LocationDto(
    Guid Id,
    string Name,
    string Code,
    string? Phone,
    string? AddressLine1,
    string? City,
    string? Region,
    string TimeZone,
    decimal DefaultLaborRate,
    string? DefaultTaxRegion,
    string Status);

public sealed record UpsertLocationRequest(
    Guid? Id,
    string Name,
    string Code,
    string? Phone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Region,
    string? PostalCode,
    string TimeZone,
    decimal DefaultLaborRate,
    string? DefaultTaxRegion,
    string Status);

public sealed record EmployeeDto(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    string Role,
    string Status,
    string? PrimaryLocation);

public sealed record InviteEmployeeRequest(
    string Name,
    string Email,
    string? Phone,
    string RoleName,
    Guid? PrimaryLocationId);

public sealed record RoleDto(string Name, bool IsSystemRole, IReadOnlyCollection<string> Permissions);

public sealed record LaborConfigurationRequest(
    decimal DefaultLaborRate,
    decimal DiagnosticRate,
    decimal EmergencyRate,
    decimal WeekendRate,
    decimal EnvironmentalFee,
    decimal ShopSuppliesPercent,
    bool LaborLineItemsTaxable);

public sealed record TaxConfigurationRequest(decimal DefaultTaxRate, string RegionalOverridesJson);

public sealed record NotificationPreferencesRequest(
    bool EmailEnabled,
    bool SmsEnabled,
    bool PushFutureEnabled,
    bool CustomerNotificationsEnabled,
    bool TechnicianNotificationsEnabled);

public sealed record BusinessConfigurationDto(
    decimal DefaultLaborRate,
    decimal DiagnosticRate,
    decimal EmergencyRate,
    decimal WeekendRate,
    decimal EnvironmentalFee,
    decimal ShopSuppliesPercent,
    bool LaborLineItemsTaxable,
    decimal DefaultTaxRate,
    string RegionalTaxOverridesJson,
    string NumberSequencesJson,
    string NotificationPreferencesJson,
    string DepartmentsJson);

public sealed record ConnectorDto(
    string Key,
    string Name,
    string Category,
    string Status,
    string Description,
    bool IsEnabled,
    string SyncCadence,
    DateTimeOffset? LastSyncAtUtc,
    IReadOnlyCollection<string> Capabilities,
    WpsConnectorConfigurationDto? WpsConfiguration);

public sealed record WpsConnectorConfigurationDto(
    string DealerNumber,
    string Username,
    string Endpoint,
    string PriceCode,
    string DefaultWarehouse,
    bool ImportCatalog,
    bool ImportInventory,
    bool SubmitPurchaseOrders,
    bool UseSandbox,
    string CredentialStatus);

public sealed record SaveWpsConnectorRequest(
    string? DealerNumber,
    string? Username,
    string? ApiPassword,
    string? Endpoint,
    string? PriceCode,
    string? DefaultWarehouse,
    bool ImportCatalog,
    bool ImportInventory,
    bool SubmitPurchaseOrders,
    bool UseSandbox,
    string? SyncCadence,
    string? Status);
