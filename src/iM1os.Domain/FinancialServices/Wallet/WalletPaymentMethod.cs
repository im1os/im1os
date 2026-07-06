using iM1os.Domain.Common;

namespace iM1os.Domain.FinancialServices.Wallet;

public sealed class WalletPaymentMethod : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid WalletId { get; set; }

    public string ProviderCode { get; set; } = string.Empty;

    public string ProviderToken { get; set; } = string.Empty;

    public string MethodType { get; set; } = "Card";

    public string? DisplayBrand { get; set; }

    public string? LastFour { get; set; }

    public string Status { get; set; } = "Active";

    public bool IsPreferred { get; set; }

    public DateTimeOffset? AuthorizedAtUtc { get; set; }
}
