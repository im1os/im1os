using iM1os.Domain.Common;

namespace iM1os.Domain.FinancialServices.Payments;

public sealed class PaymentTransaction : AuditableEntity, IOrganizationOwned, ILocationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid? LocationId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string Environment { get; set; } = "Sandbox";

    public string TransactionType { get; set; } = "Sale";

    public string PaymentMethod { get; set; } = "Card";

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public string Status { get; set; } = "Pending";

    public bool IsApproved { get; set; }

    public string? GatewayTransactionId { get; set; }

    public string? AuthorizationCode { get; set; }

    public string? ResponseCode { get; set; }

    public string? ResponseText { get; set; }

    public string? OrderId { get; set; }

    public string? ReferenceType { get; set; }

    public string? ReferenceId { get; set; }

    public string? Description { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerEmail { get; set; }

    public string? CustomerPhone { get; set; }

    public string? CardBrand { get; set; }

    public string? CardLastFour { get; set; }

    public string? RequestCorrelationId { get; set; }

    public string? RawResponseJson { get; set; }
}
