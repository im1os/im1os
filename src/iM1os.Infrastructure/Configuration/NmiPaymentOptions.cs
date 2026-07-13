namespace iM1os.Infrastructure.Configuration;

public sealed class NmiPaymentOptions
{
    public const string SectionName = "NmiPayments";

    public string Environment { get; set; } = "Sandbox";

    public string PaymentsBaseUrl { get; set; } = "https://sandbox.nmi.com/api/v5/";

    public string AccountManagementBaseUrl { get; set; } = "https://sandbox.nmi.com/api/v4/";

    public string SignUpBaseUrl { get; set; } = "https://sandbox.signup.nmi.com/api/v1/";

    public string? SignUpPackageId { get; set; }

    public string? SignUpPricingType { get; set; }

    public decimal? SignUpQualifiedRate { get; set; }

    public decimal? SignUpQualifiedRatePerAuthorization { get; set; }

    public decimal? SignUpCostPlusRate { get; set; }

    public decimal? SignUpCostPlusPerAuthorization { get; set; }

    public string CollectJsUrl { get; set; } = "https://secure.nmi.com/token/Collect.js";

    public string? MerchantPrivateKey { get; set; }

    public string? MerchantTokenizationKey { get; set; }

    public string? PartnerApiKey { get; set; }

    public string? PartnerOAuthClientId { get; set; }

    public string? PartnerOAuthClientSecret { get; set; }
}
