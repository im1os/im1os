using iM1os.Domain.Common;

namespace iM1os.Domain.Tenancy;

public sealed class Organization : AuditableEntity
{
    public required string Name { get; set; }

    public required string Slug { get; set; }

    public string? LogoUrl { get; set; }

    public string? LegalName { get; set; }

    public string? Dba { get; set; }

    public string? Website { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? TaxId { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? Region { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    public string TimeZone { get; set; } = "America/Chicago";

    public string Language { get; set; } = "en-US";

    public string Currency { get; set; } = "USD";

    public string DateFormat { get; set; } = "MM/dd/yyyy";

    public string TimeFormat { get; set; } = "h:mm tt";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? OnboardingCompletedAtUtc { get; set; }
}
