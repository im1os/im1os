namespace iM1os.Application.FinancialServices;

public sealed record FinancialServicesWorkspace(
    IReadOnlyCollection<FinancialServicesModuleDefinition> Modules,
    int ActiveModules,
    int PlaceholderModules,
    int PlannedModules);

public sealed record FinancialServicesModuleDefinition(
    string Key,
    string Name,
    string Category,
    string Summary,
    string Status,
    string WorkflowStage,
    string? Controller = null,
    string? Action = null);

public static class FinancialServicesModuleCatalog
{
    public static IReadOnlyCollection<FinancialServicesModuleDefinition> Modules { get; } =
    [
        new(
            "merchant-account",
            "Merchant Account",
            "Account",
            "The company's own processing account status, underwriting state, settlement account, limits, documents, and processing profile.",
            "Active",
            "Manage payment account",
            "FinancialServices",
            "MerchantAccount"),
        new(
            "terminal-management",
            "Terminals",
            "Hardware",
            "Company payment devices, assignment, health, firmware status, and register/location ownership.",
            "Placeholder",
            "Manage devices"),
        new(
            "payment-engine",
            "Payments",
            "Payment Engine",
            "Tokenized payments and recorded payment attempts through the iM1 payment abstraction.",
            "Active",
            "Accept payment",
            "Payments",
            "Index"),
        new(
            "transaction-center",
            "Transactions",
            "Payment Engine",
            "Sales, declines, refunds, voids, partial refunds, disputes, chargebacks, and receipts.",
            "Active",
            "Record and review transactions",
            "Payments",
            "Index"),
        new(
            "customer-wallet",
            "Customer Wallet",
            "Wallet",
            "Secure customer payment methods, ACH accounts, preferred tender, and recurring authorization tokens.",
            "Placeholder",
            "Store reusable tokens"),
        new(
            "subscription-billing",
            "Subscriptions",
            "Billing Engine",
            "Recurring memberships, service plans, storage fees, maintenance programs, and software subscriptions.",
            "Placeholder",
            "Bill recurring agreement"),
        new(
            "ach-processing",
            "ACH Processing",
            "Banking Engine",
            "Customer ACH, vendor ACH, bank verification, recurring ACH, and ACH refunds.",
            "Placeholder",
            "Move bank funds"),
        new(
            "payment-links",
            "Payment Links",
            "Payment Engine",
            "Secure payment links for estimates, deposits, invoices, event registrations, and parts orders.",
            "Placeholder",
            "Send payment request"),
        new(
            "virtual-terminal",
            "Virtual Terminal",
            "Payment Engine",
            "Remote card-not-present payments for phone orders, mail orders, customer service, invoices, and deposits.",
            "Active",
            "Accept keyed payment",
            "Payments",
            "Index"),
        new(
            "financial-ledger",
            "Financial Ledger",
            "Ledger",
            "Immutable operational financial entries for payments, refunds, chargebacks, ACH, store credit, gift cards, settlements, subscriptions, and future reconciliation.",
            "Active",
            "Record money movement"),
        new(
            "deposits",
            "Deposits",
            "Reporting",
            "Deposit batches, settlement timing, net funding, fees, and reconciliation state for the company.",
            "Placeholder",
            "Review funding"),
        new(
            "statements",
            "Statements",
            "Reporting",
            "Monthly merchant statements, fees, disputes, funding summaries, and downloadable financial records.",
            "Placeholder",
            "Review statements"),
        new(
            "reports",
            "Reports",
            "Reporting",
            "Payment volume, average ticket, decline rate, settlement timing, ACH activity, and subscription revenue.",
            "Placeholder",
            "Analyze finance"),
        new(
            "settings",
            "Settings",
            "Configuration",
            "Company-level payment settings, receipt preferences, statement contacts, wallet settings, and tender rules.",
            "Placeholder",
            "Configure finance"),
        new(
            "financing",
            "Financing",
            "Future Financial Products",
            "Repair financing, parts financing, buy now pay later, business lending, and future financing handoffs.",
            "Planned",
            "Offer financing")
    ];

    public static FinancialServicesWorkspace Workspace()
    {
        return new FinancialServicesWorkspace(
            Modules,
            Modules.Count(x => x.Status == "Active"),
            Modules.Count(x => x.Status == "Placeholder"),
            Modules.Count(x => x.Status == "Planned"));
    }

    public static FinancialServicesModuleDefinition? Find(string key)
    {
        return Modules.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record PlatformFinancialServicesWorkspace(
    IReadOnlyCollection<PlatformFinancialServicesModuleDefinition> Modules,
    int ActiveModules,
    int PlaceholderModules);

public sealed record PlatformFinancialServicesModuleDefinition(
    string Key,
    string Name,
    string Category,
    string Summary,
    string Status,
    string WorkflowStage);

public static class PlatformFinancialServicesModuleCatalog
{
    public static IReadOnlyCollection<PlatformFinancialServicesModuleDefinition> Modules { get; } =
    [
        new("merchant-applications", "Merchant Applications", "Onboarding", "New merchant applications submitted by companies.", "Active", "Review application"),
        new("active-merchants", "Active Merchants", "Portfolio", "Live merchant accounts, processing health, limits, and account state.", "Active", "Manage merchant"),
        new("underwriting-queue", "Underwriting Queue", "Risk", "Pending underwriting tasks, document review, identity checks, and approval decisions.", "Placeholder", "Underwrite merchant"),
        new("risk-monitoring", "Risk Monitoring", "Risk", "Velocity, disputes, chargebacks, unusual activity, holds, and reserves.", "Placeholder", "Monitor risk"),
        new("processor-management", "Processor Management", "Providers", "Processor relationships, processor-level status, capabilities, and routing rules.", "Placeholder", "Manage processors"),
        new("gateway-providers", "Gateway Providers", "Providers", "Gateway provider setup, health, credentials, and capability mapping.", "Placeholder", "Manage gateways"),
        new("residual-reporting", "Residual Reporting", "Revenue", "Residuals, revenue share, fees, and partner economics for iM1 Financial Services.", "Placeholder", "Review residuals"),
        new("settlement-monitoring", "Settlement Monitoring", "Operations", "Settlement batches, funding delays, failed deposits, reserves, and reconciliation exceptions.", "Placeholder", "Monitor settlements"),
        new("chargeback-management", "Chargeback Management", "Risk", "Chargebacks, disputes, evidence packages, deadlines, and outcomes.", "Placeholder", "Manage disputes"),
        new("hardware-catalog", "Hardware Catalog", "Hardware", "Certified readers, terminals, printers, scanner bundles, and hardware pricing.", "Placeholder", "Manage catalog"),
        new("device-inventory", "Device Inventory", "Hardware", "Owned hardware inventory, serials, assignment, returns, replacements, and device state.", "Placeholder", "Track devices"),
        new("shipping-fulfillment", "Shipping / Fulfillment", "Hardware", "Hardware orders, shipping labels, fulfillment queue, tracking, and returns.", "Placeholder", "Fulfill hardware"),
        new("pricing-plans", "Pricing Plans", "Revenue", "Merchant pricing plans, fees, discounts, promos, and plan eligibility.", "Placeholder", "Manage pricing"),
        new("merchant-support", "Merchant Support", "Support", "Merchant support cases, provider escalations, hardware support, and account requests.", "Placeholder", "Support merchants"),
        new("provider-configuration", "Provider Configuration", "Providers", "Provider credentials, environment settings, feature flags, webhooks, and routing defaults.", "Placeholder", "Configure providers")
    ];

    public static PlatformFinancialServicesWorkspace Workspace()
    {
        return new PlatformFinancialServicesWorkspace(
            Modules,
            Modules.Count(x => x.Status == "Active"),
            Modules.Count(x => x.Status == "Placeholder"));
    }

    public static PlatformFinancialServicesModuleDefinition? Find(string key)
    {
        return Modules.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
    }
}
