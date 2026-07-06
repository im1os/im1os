namespace iM1os.Application.BusinessAdministration;

public sealed record BusinessAdministrationWorkspace(
    Guid OrganizationId,
    BusinessProfileDto Profile,
    BusinessConfigurationDto Configuration,
    IReadOnlyCollection<LocationDto> Locations,
    IReadOnlyCollection<EmployeeDto> Employees,
    IReadOnlyCollection<RoleDto> Roles,
    IReadOnlyCollection<ConnectorDto> Connectors,
    TimeClockWorkspace TimeClock,
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

public sealed record TimeClockWorkspace(
    IReadOnlyCollection<TimeClockEmployeeOption> Employees,
    IReadOnlyCollection<TimeClockOpenPunchDto> OpenPunches,
    IReadOnlyCollection<TimeClockPunchDto> RecentPunches,
    IReadOnlyCollection<TimeClockScheduleShiftDto> UpcomingShifts,
    IReadOnlyCollection<TimeClockTimeOffDto> TimeOffRequests,
    IReadOnlyCollection<EmployeeCompanyAssetDto> CompanyAssets,
    IReadOnlyCollection<EmployeeSafetyIncidentDto> SafetyIncidents,
    SafetySummaryDto SafetySummary,
    TimeClockPayrollSummary Payroll,
    DateOnly PayrollStartDate,
    DateOnly PayrollEndDate);

public sealed record TimeClockEmployeeOption(Guid Id, string Name, bool HasPin, string Status);

public sealed record TimeClockOpenPunchDto(Guid Id, Guid EmployeeId, string EmployeeName, DateTimeOffset ClockInUtc, decimal ElapsedHours);

public sealed record TimeClockPunchDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    DateTimeOffset ClockInUtc,
    DateTimeOffset? ClockOutUtc,
    decimal Hours,
    string? Note,
    bool IsOpen);

public sealed record TimeClockScheduleShiftDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    DateOnly ShiftDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal Hours,
    string? Note);

public sealed record TimeClockTimeOffDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string Type,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal HoursPerDay,
    decimal TotalHours,
    string Status,
    string? Note);

public sealed record TimeClockPayrollSummary(
    IReadOnlyCollection<TimeClockPayrollEmployeeSummary> Employees,
    decimal TotalWorkedHours,
    decimal TotalScheduledHours,
    decimal TotalTimeOffHours,
    decimal TotalPaidHours,
    decimal TotalGrossPay);

public sealed record TimeClockPayrollEmployeeSummary(
    Guid EmployeeId,
    string EmployeeName,
    string PayrollType,
    decimal? HourlyRate,
    decimal WorkedHours,
    decimal ScheduledHours,
    decimal TimeOffHours,
    decimal PaidTimeOffHours,
    decimal PaidHours,
    decimal VarianceHours,
    decimal GrossPay);

public sealed record EmployeeCompanyAssetDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string Name,
    string? AssetTag,
    string? SerialNumber,
    DateOnly? IssuedDate,
    DateOnly? ReturnedDate,
    string Status,
    string? Note);

public sealed record EmployeeSafetyIncidentDto(
    Guid Id,
    Guid? EmployeeId,
    string EmployeeName,
    DateOnly IncidentDate,
    string IncidentType,
    string? Severity,
    decimal LostTimeHours,
    bool IsOshaRecordable,
    bool ReportedToOsha,
    string? Description);

public sealed record SafetySummaryDto(
    int IncidentCount,
    int OshaRecordableCount,
    int ReportedToOshaCount,
    decimal LostTimeHours);

public sealed record ClockEmployeeRequest(Guid EmployeeId, string Pin, string Action);

public sealed record AddTimePunchRequest(Guid EmployeeId, DateTime ClockInLocal, DateTime? ClockOutLocal, string? Note);

public sealed record UpdateTimePunchRequest(Guid PunchId, DateTime ClockInLocal, DateTime? ClockOutLocal, string? Note);

public sealed record DeleteTimePunchRequest(Guid PunchId);

public sealed record AddScheduleShiftRequest(Guid EmployeeId, DateOnly ShiftDate, TimeOnly StartTime, TimeOnly EndTime, string? Note);

public sealed record DeleteScheduleShiftRequest(Guid ShiftId);

public sealed record AddTimeOffRequest(Guid EmployeeId, string Type, DateOnly StartDate, DateOnly EndDate, decimal HoursPerDay, string? Note);

public sealed record SetTimeOffStatusRequest(Guid TimeOffRequestId, string Status);

public sealed record DeleteTimeOffRequest(Guid TimeOffRequestId);

public sealed record AddCompanyAssetRequest(Guid EmployeeId, string Name, string? AssetTag, string? SerialNumber, DateOnly? IssuedDate, DateOnly? ReturnedDate, string? Note);

public sealed record ReturnCompanyAssetRequest(Guid AssetId, DateOnly ReturnedDate, string? Note);

public sealed record DeleteCompanyAssetRequest(Guid AssetId);

public sealed record AddSafetyIncidentRequest(Guid? EmployeeId, DateOnly IncidentDate, string IncidentType, string? Severity, decimal LostTimeHours, bool IsOshaRecordable, bool ReportedToOsha, string? Description);

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

public sealed record SupplierPreferencesRequest(
    string? PreferredSupplierCode,
    string? WpsPreferredWarehouseCode,
    string? Turn14PreferredWarehouseCode,
    string? PartsUnlimitedPreferredWarehouseCode);

public sealed record SupplierPreferencesDto(
    string? PreferredSupplierCode,
    IReadOnlyCollection<SupplierWarehousePreferenceDto> Warehouses);

public sealed record SupplierWarehousePreferenceDto(
    string SupplierCode,
    string SupplierName,
    string? PreferredWarehouseCode,
    IReadOnlyCollection<SupplierWarehouseOptionDto> WarehouseOptions);

public sealed record SupplierWarehouseOptionDto(string Code, string Name);

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
    string DepartmentsJson,
    SupplierPreferencesDto SupplierPreferences);

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
