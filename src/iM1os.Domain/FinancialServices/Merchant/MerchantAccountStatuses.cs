namespace iM1os.Domain.FinancialServices.Merchant;

public static class MerchantAccountStatuses
{
    public const string NotStarted = nameof(NotStarted);

    public const string Draft = nameof(Draft);

    public const string Submitted = nameof(Submitted);

    public const string UnderReview = nameof(UnderReview);

    public const string Approved = nameof(Approved);

    public const string Active = nameof(Active);

    public const string Rejected = nameof(Rejected);

    public const string Suspended = nameof(Suspended);

    public const string Closed = nameof(Closed);

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Draft,
        NotStarted,
        Submitted,
        UnderReview,
        Approved,
        Active,
        Rejected,
        Suspended,
        Closed
    };
}
