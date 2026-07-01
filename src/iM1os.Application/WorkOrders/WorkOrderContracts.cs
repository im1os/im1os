namespace iM1os.Application.WorkOrders;

public sealed record WorkOrderWorkspace(
    IReadOnlyCollection<WorkOrderRow> WorkOrders,
    string? Query,
    string? Stage);

public sealed record WorkOrderRow(
    Guid Id,
    string WorkOrderNumber,
    string? RepairOrderNumber,
    string CustomerName,
    string? Unit,
    string Stage,
    string Priority,
    string? ServiceAdvisor,
    DateOnly? PromiseDate,
    DateTimeOffset OpenedAtUtc,
    decimal EstimateTotal);

public sealed record WorkOrderEditor(
    Guid? WorkOrderId,
    string WorkOrderNumber,
    string? RepairOrderNumber,
    Guid? CustomerId,
    Guid? CustomerVehicleId,
    Guid? ServiceAdvisorEmployeeId,
    string Stage,
    string Priority,
    DateOnly? PromiseDate,
    DateOnly? IntakeDate,
    string? RequestedService,
    string? DiagnosisFindings,
    string? ServiceNotes,
    string? PartsAndSuppliesNotes,
    string DepositTerms,
    string? PaymentTerms,
    decimal DefaultLaborRate,
    bool LaborLineItemsTaxable,
    WorkOrderTechnicianAssignmentItem? LeadTechnician,
    IReadOnlyCollection<WorkOrderTechnicianAssignmentItem> AdditionalTechnicians,
    IReadOnlyCollection<EstimateLineItemEditor> LineItems,
    WorkOrderTotals Totals,
    IReadOnlyCollection<WorkOrderCustomerOption> Customers,
    IReadOnlyCollection<WorkOrderVehicleOption> Vehicles,
    WorkOrderCustomerSummary? SelectedCustomer,
    WorkOrderVehicleSummary? SelectedVehicle,
    IReadOnlyCollection<WorkOrderEmployeeOption> ServiceAdvisors,
    IReadOnlyCollection<WorkOrderEmployeeOption> Technicians);

public sealed record WorkOrderCustomerOption(
    Guid Id,
    string DisplayName,
    string? Email,
    string? Phone,
    string? PreferredContactMethod,
    bool AllowSmsMarketing,
    bool TaxExempt,
    string? TaxExemptNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Region,
    string? PostalCode);

public sealed record WorkOrderVehicleOption(
    Guid Id,
    Guid CustomerId,
    string Label,
    string Type,
    int? Year,
    string? Make,
    string? Model,
    string? Vin,
    string? Color,
    string? TagPlate,
    decimal? MileageIn,
    string? Notes);

public sealed record WorkOrderCustomerSummary(
    Guid Id,
    string DisplayName,
    string? Email,
    string? Phone,
    string? PreferredContactMethod,
    bool AllowSmsMarketing,
    bool TaxExempt,
    string? TaxExemptNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Region,
    string? PostalCode);

public sealed record WorkOrderVehicleSummary(
    Guid Id,
    Guid CustomerId,
    string Label,
    string Type,
    int? Year,
    string? Make,
    string? Model,
    string? Vin,
    string? Color,
    string? TagPlate,
    decimal? MileageIn,
    string? Notes);

public sealed record WorkOrderEmployeeOption(Guid Id, string DisplayName);

public sealed record WorkOrderLaborItem(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? ServiceCategory,
    decimal? BaseHours,
    decimal Rate,
    bool IsTaxable);

public sealed record WorkOrderTechnicianAssignmentItem(Guid? EmployeeId, string Role, decimal SplitPercent, int SortOrder);

public sealed record EstimateLineItemEditor(
    Guid? Id,
    string LineType,
    string? Description,
    string? Notes,
    string? Sku,
    decimal Quantity,
    decimal Rate,
    decimal DiscountAmount,
    decimal DiscountPercent,
    decimal LineTotal,
    bool IsTaxable,
    bool IsDeclined,
    bool IsDone,
    int SortOrder);

public sealed record WorkOrderTotals(
    decimal LaborTotal,
    decimal PartsTotal,
    decimal FeesTotal,
    decimal DiscountTotal,
    decimal Subtotal,
    decimal TaxTotal,
    decimal EstimateTotal);

public sealed record WorkOrderSearchRequest(string? Query, string? Stage);

public sealed record WorkOrderIntakePage(
    IReadOnlyCollection<WorkOrderEmployeeOption> ServiceAdvisors,
    IReadOnlyCollection<string> VehicleTypes,
    IReadOnlyCollection<int> Years,
    DateOnly IntakeDate,
    WorkOrderEmployeeOption? VerifiedServiceAdvisor);

public sealed record WorkOrderIntakePinResult(
    Guid EmployeeId,
    string DisplayName);

public sealed record WorkOrderCustomerLookupResult(
    IReadOnlyCollection<WorkOrderCustomerLookupMatch> Matches);

public sealed record WorkOrderCustomerLookupMatch(
    Guid Id,
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    string? PreferredContactMethod,
    bool TaxExempt,
    string? TaxExemptNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Region,
    string? PostalCode,
    bool AllowSmsMarketing,
    IReadOnlyCollection<WorkOrderCustomerVehicleMatch> Vehicles);

public sealed record WorkOrderCustomerVehicleMatch(
    Guid Id,
    string Label,
    string Type,
    int? Year,
    string? Make,
    string? Model,
    string? Vin,
    string? Color,
    string? TagPlate,
    decimal? MileageIn,
    string? Notes);

public sealed record WorkOrderIntakeResult(Guid WorkOrderId, Guid CustomerId, Guid? CustomerVehicleId);

public sealed record WorkOrderAttachmentUpload(
    string AttachmentType,
    string FileName,
    string? Url,
    string? ContentType);

public sealed class CreateWorkOrderIntakeRequest
{
    public Guid? CustomerId { get; set; }

    public string? LookupQuery { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string PreferredContactMethod { get; set; } = "Phone";

    public bool SmsConsent { get; set; }

    public bool TaxExempt { get; set; }

    public string? TaxExemptNumber { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? PostalCode { get; set; }

    public string? City { get; set; }

    public string? Region { get; set; }

    public Guid? CustomerVehicleId { get; set; }

    public string Type { get; set; } = "Street";

    public int? Year { get; set; }

    public string? Make { get; set; }

    public string? Model { get; set; }

    public string? Vin { get; set; }

    public string? Color { get; set; }

    public string? TagPlate { get; set; }

    public decimal? MileageIn { get; set; }

    public string? EngineSerialNotes { get; set; }

    public bool BypassVehicleSelection { get; set; }

    public Guid? ServiceAdvisorEmployeeId { get; set; }

    public string Priority { get; set; } = "Normal";

    public DateOnly? PromiseDate { get; set; }

    public string? RequestedService { get; set; }

    public bool CreateOrUpdateSquareCustomer { get; set; } = true;
}

public sealed class AddWorkOrderAttachmentRequest
{
    public Guid WorkOrderId { get; set; }

    public IReadOnlyCollection<WorkOrderAttachmentUpload> Attachments { get; set; } = [];
}

public sealed class SaveWorkOrderRequest
{
    public Guid? WorkOrderId { get; set; }

    public string? WorkOrderNumber { get; set; }

    public string? RepairOrderNumber { get; set; }

    public Guid CustomerId { get; set; }

    public Guid? CustomerVehicleId { get; set; }

    public string? CustomerDisplayName { get; set; }

    public string? CustomerEmail { get; set; }

    public string? CustomerPhone { get; set; }

    public string? CustomerPreferredContactMethod { get; set; }

    public bool CustomerSmsConsent { get; set; }

    public bool CustomerTaxExempt { get; set; }

    public string? CustomerTaxExemptNumber { get; set; }

    public string? CustomerAddressLine1 { get; set; }

    public string? CustomerAddressLine2 { get; set; }

    public string? CustomerCity { get; set; }

    public string? CustomerRegion { get; set; }

    public string? CustomerPostalCode { get; set; }

    public string? UnitType { get; set; }

    public int? UnitYear { get; set; }

    public string? UnitMake { get; set; }

    public string? UnitModel { get; set; }

    public string? UnitVin { get; set; }

    public string? UnitColor { get; set; }

    public string? UnitTagPlate { get; set; }

    public decimal? UnitMileageIn { get; set; }

    public string? UnitNotes { get; set; }

    public Guid? ServiceAdvisorEmployeeId { get; set; }

    public string Stage { get; set; } = "Intake / Check-in";

    public string Priority { get; set; } = "Normal";

    public DateOnly? PromiseDate { get; set; }

    public DateOnly? IntakeDate { get; set; }

    public string? RequestedService { get; set; }

    public string? DiagnosisFindings { get; set; }

    public string? ServiceNotes { get; set; }

    public string? PartsAndSuppliesNotes { get; set; }

    public Guid? LeadTechnicianEmployeeId { get; set; }

    public decimal? LeadTechnicianSplitPercent { get; set; }

    public Guid? AdditionalTechnician1EmployeeId { get; set; }

    public decimal? AdditionalTechnician1SplitPercent { get; set; }

    public Guid? AdditionalTechnician2EmployeeId { get; set; }

    public decimal? AdditionalTechnician2SplitPercent { get; set; }

    public List<SaveWorkOrderTechnicianAssignmentRequest> TechnicianAssignments { get; set; } = [];

    public string DepositTerms { get; set; } = "No Deposit";

    public string? PaymentTerms { get; set; }

    public List<SaveEstimateLineItemRequest> LineItems { get; set; } = [];
}

public sealed class SaveWorkOrderTechnicianAssignmentRequest
{
    public Guid? EmployeeId { get; set; }

    public decimal? SplitPercent { get; set; }
}

public sealed class SaveEstimateLineItemRequest
{
    public string LineType { get; set; } = "Labor";

    public string? Description { get; set; }

    public string? Notes { get; set; }

    public string? Sku { get; set; }

    public decimal Quantity { get; set; } = 1m;

    public decimal Rate { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal DiscountPercent { get; set; }

    public bool IsTaxable { get; set; } = true;

    public bool IsDeclined { get; set; }

    public bool IsDone { get; set; }
}
