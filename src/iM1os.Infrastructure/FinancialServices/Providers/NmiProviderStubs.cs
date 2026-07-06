using iM1os.Application.FinancialServices.Providers;

namespace iM1os.Infrastructure.FinancialServices.Providers;

public sealed class NmiMerchantProvider : IMerchantProvider
{
    public string ProviderCode => "NMI";
}

public sealed class NmiTerminalProvider : ITerminalProvider
{
    public string ProviderCode => "NMI";
}

public sealed class NmiCustomerVaultProvider : ICustomerVaultProvider
{
    public string ProviderCode => "NMI";
}

public sealed class NmiAchProvider : IACHProvider
{
    public string ProviderCode => "NMI";
}

public sealed class NmiSubscriptionProvider : ISubscriptionProvider
{
    public string ProviderCode => "NMI";
}
