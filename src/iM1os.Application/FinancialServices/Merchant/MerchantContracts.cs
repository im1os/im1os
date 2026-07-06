namespace iM1os.Application.FinancialServices.Merchant;

public sealed record MerchantOnboardingRequest(
    string LegalBusinessName,
    string Country,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string Region,
    string PostalCode,
    string TimeZone,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string? Website = null,
    string? ExternalIdentifier = null,
    string ProviderCode = "NMI");

public sealed record MerchantApplicationRequest(
    string BusinessName,
    string? Dba,
    string? Ein,
    string? TaxId,
    string? BusinessType,
    string PhysicalAddressLine1,
    string? PhysicalAddressLine2,
    string PhysicalCity,
    string PhysicalRegion,
    string PhysicalPostalCode,
    string PhysicalCountry,
    string? MailingAddressLine1,
    string? MailingAddressLine2,
    string? MailingCity,
    string? MailingRegion,
    string? MailingPostalCode,
    string? MailingCountry,
    string OwnerName,
    string OwnerEmail,
    string OwnerPhone,
    string? BankName,
    string? BankRoutingNumber,
    string? BankAccountNumber,
    decimal? ExpectedMonthlyVolume,
    decimal? AverageTicket,
    string? Website,
    string? Mcc,
    string ProviderCode = "NMI");

public sealed record MerchantAccountResult(
    Guid MerchantAccountId,
    Guid OrganizationId,
    string Status,
    string UnderwritingStatus,
    string ProviderCode,
    string ProviderMerchantId);

public sealed record CompanyMerchantAccountWorkspace(
    Guid OrganizationId,
    MerchantAccountSummary? Account,
    MerchantApplicationFormModel Application,
    IReadOnlyCollection<MerchantStatusHistoryRow> StatusHistory,
    IReadOnlyCollection<string> RequiredNextSteps,
    bool IsProcessingReady);

public sealed record MerchantAccountSummary(
    Guid MerchantAccountId,
    Guid OrganizationId,
    string Status,
    string UnderwritingStatus,
    string? LegalBusinessName,
    string? ProviderCode,
    string? ProviderDisplayName,
    string? ProviderRelationshipStatus,
    bool PaymentsEnabled,
    bool HasProviderMerchant,
    bool IsProcessingReady);

public sealed record MerchantApplicationFormModel(
    Guid OrganizationId,
    Guid? MerchantAccountId,
    string Status,
    string BusinessName,
    string? Dba,
    string? Ein,
    string? TaxId,
    string? BusinessType,
    string PhysicalAddressLine1,
    string? PhysicalAddressLine2,
    string PhysicalCity,
    string PhysicalRegion,
    string PhysicalPostalCode,
    string PhysicalCountry,
    string? MailingAddressLine1,
    string? MailingAddressLine2,
    string? MailingCity,
    string? MailingRegion,
    string? MailingPostalCode,
    string? MailingCountry,
    string OwnerName,
    string OwnerEmail,
    string OwnerPhone,
    string? BankName,
    string? BankRoutingLastFour,
    string? BankAccountLastFour,
    decimal? ExpectedMonthlyVolume,
    decimal? AverageTicket,
    string? Website,
    string? Mcc,
    bool CanEdit,
    bool CanSubmit);

public sealed record MerchantStatusHistoryRow(
    Guid CompanyId,
    Guid MerchantAccountId,
    string? OldStatus,
    string NewStatus,
    string? Reason,
    string? Provider,
    string? ProviderReference,
    string? CreatedBy,
    DateTimeOffset CreatedAt);

public sealed record PlatformMerchantApplicationsWorkspace(
    IReadOnlyCollection<PlatformMerchantAccountRow> Applications,
    int SubmittedCount,
    int UnderReviewCount,
    int ApprovedCount,
    int RejectedCount);

public sealed record PlatformActiveMerchantsWorkspace(
    IReadOnlyCollection<PlatformMerchantAccountRow> Merchants,
    int ActiveCount,
    int SuspendedCount,
    int ClosedCount);

public sealed record PlatformMerchantApplicationDetail(
    PlatformMerchantAccountRow Merchant,
    MerchantApplicationFormModel Application);

public sealed record PlatformMerchantAccountRow(
    Guid CompanyId,
    Guid MerchantAccountId,
    string? CompanyName,
    string Status,
    string UnderwritingStatus,
    string? LegalBusinessName,
    string? OwnerName,
    string? OwnerEmail,
    decimal? ExpectedMonthlyVolume,
    string? ProviderCode,
    string? ProviderMerchantId,
    string? GatewayUsername,
    bool HasPaymentApiKey,
    bool HasPublicTokenizationKey,
    string? ProviderReference,
    string? ProviderRelationshipStatus,
    string? LastProviderError,
    string? SupportNotes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyCollection<MerchantStatusHistoryRow> StatusHistory);
