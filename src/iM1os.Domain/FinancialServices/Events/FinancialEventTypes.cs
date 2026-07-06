namespace iM1os.Domain.FinancialServices.Events;

public static class FinancialEventTypes
{
    public const string PaymentApproved = nameof(PaymentApproved);

    public const string PaymentDeclined = nameof(PaymentDeclined);

    public const string RefundCompleted = nameof(RefundCompleted);

    public const string MerchantApplicationSubmitted = nameof(MerchantApplicationSubmitted);

    public const string MerchantUnderReview = nameof(MerchantUnderReview);

    public const string MerchantApproved = nameof(MerchantApproved);

    public const string MerchantActivated = nameof(MerchantActivated);

    public const string MerchantRejected = nameof(MerchantRejected);

    public const string MerchantSuspended = nameof(MerchantSuspended);

    public const string MerchantClosed = nameof(MerchantClosed);

    public const string SubscriptionRenewed = nameof(SubscriptionRenewed);

    public const string SettlementPosted = nameof(SettlementPosted);
}
