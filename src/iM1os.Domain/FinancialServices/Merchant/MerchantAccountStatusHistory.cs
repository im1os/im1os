using iM1os.Domain.Common;

namespace iM1os.Domain.FinancialServices.Merchant;

public sealed class MerchantAccountStatusHistory : Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid MerchantAccountId { get; set; }

    public string? OldStatus { get; set; }

    public string NewStatus { get; set; } = MerchantAccountStatuses.Draft;

    public string? Reason { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderReference { get; set; }

    public string? CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
