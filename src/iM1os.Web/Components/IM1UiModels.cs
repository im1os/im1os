using Microsoft.AspNetCore.Html;

namespace iM1os.Web.Components;

public sealed record IM1PageModel(
    string Title,
    string? Eyebrow = null,
    string? Description = null,
    IReadOnlyCollection<IM1BreadcrumbModel>? Breadcrumbs = null,
    IM1ToolbarModel? Toolbar = null,
    IHtmlContent? Content = null,
    string? Status = null);

public sealed record IM1BreadcrumbModel(string Label, string? Url = null);

public sealed record IM1ToolbarModel(
    IReadOnlyCollection<IM1ActionModel>? Actions = null,
    string? SearchPlaceholder = null,
    string? SearchName = "query",
    string? SearchValue = null,
    bool ShowRefresh = false,
    bool ShowFilters = false,
    bool ShowExport = false,
    string? FormAction = null,
    string Method = "get");

public sealed record IM1ActionModel(
    string Label,
    string? Url = null,
    string Kind = "secondary",
    string? Icon = null,
    string? Permission = null,
    bool IsEnabled = true);

public sealed record IM1CardModel(
    string Title,
    string? Value = null,
    string? Description = null,
    string? Eyebrow = null,
    IHtmlContent? Content = null,
    IReadOnlyCollection<IM1ActionModel>? Actions = null);

public sealed record IM1WorkspaceHeroModel(
    string Title,
    string Eyebrow,
    string? Summary = null,
    IReadOnlyCollection<IM1WorkspaceStatModel>? Stats = null,
    IReadOnlyCollection<IM1ActionModel>? Actions = null,
    string? Status = null);

public sealed record IM1WorkspaceStatModel(
    string Label,
    string Value,
    string? Detail = null);

public sealed record IM1DialogModel(
    string Id,
    string Title,
    string? Description = null,
    IHtmlContent? Content = null,
    string PrimaryActionLabel = "Save",
    string SecondaryActionLabel = "Cancel",
    string Size = "md");

public sealed record IM1SidePanelModel(
    string Id,
    string Title,
    string? Description = null,
    IHtmlContent? Content = null,
    bool IsOpen = false);

public sealed record IM1FormModel(
    string? Title = null,
    string? Description = null,
    string Method = "post",
    string? Action = null,
    IHtmlContent? Fields = null,
    string SubmitLabel = "Save",
    string? ValidationSummary = null);

public sealed record IM1DataGridModel(
    IReadOnlyCollection<IM1DataGridColumnModel> Columns,
    IReadOnlyCollection<IReadOnlyDictionary<string, object?>> Rows,
    IM1ToolbarModel? Toolbar = null,
    IReadOnlyCollection<IM1ActionModel>? RowActions = null,
    string? EmptyText = "No records found.",
    int? PageSize = null,
    int? PageNumber = null,
    int? TotalRows = null);

public sealed record IM1DataGridColumnModel(
    string Key,
    string Header,
    bool Sortable = true,
    bool Filterable = true);

public sealed record IM1TabsModel(
    string Label,
    IReadOnlyCollection<IM1TabItemModel> Items);

public sealed record IM1TabItemModel(
    string Id,
    string Label,
    bool IsActive = false);

public sealed record IM1DocumentDropzoneModel(
    string FormAction,
    string OwnerIdFieldName,
    Guid OwnerId,
    string ReturnTab = "documents",
    string Title = "Add Document",
    string Description = "Drop files here or choose files from your computer.",
    string SubmitLabel = "Add Document",
    IReadOnlyCollection<string>? DocumentTypes = null);

public sealed record IM1ShellNavigationModel(
    string ProductName,
    IReadOnlyCollection<IM1ShellNavigationItemModel> Items,
    string? CurrentController = null,
    string? CurrentAction = null);

public sealed record IM1ShellNavigationItemModel(
    string Label,
    string? Controller = null,
    string? Action = null,
    string? Icon = null,
    IReadOnlyCollection<IM1ShellNavigationItemModel>? Children = null);

public sealed record IM1ShellNavigationConfiguration(
    IReadOnlyCollection<IM1ShellNavigationItemModel> PlatformItems,
    IReadOnlyCollection<IM1ShellNavigationItemModel> CompanyItems);

public static class IM1ShellNavigation
{
    private static readonly IReadOnlyDictionary<string, string> CompanySupplierConnectorActions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["SupplierWpsConnector"] = "WPS",
        ["SupplierPartsUnlimitedConnector"] = "PU",
        ["SupplierTurn14Connector"] = "TURN14"
    };

    public static IM1ShellNavigationConfiguration AppShell { get; } = new(
        PlatformItems:
        [
            new(
                "Admin",
                Icon: "AD",
                Children:
                [
                    new("Dashboard", "Platform", "Dashboard", "DB"),
                    new("Scheduler", "Platform", "Scheduler", "SC")
                ]),
            new(
                "Company Manager",
                Icon: "TM",
                Children:
                [
                    new("Companies", "Platform", "Tenants", "CO")
                ]),
            new(
                "Suppliers",
                Icon: "SU",
                Children:
                [
                    new("Item Search", "Platform", "ItemSearch", "IS"),
                    new("WPS", "Platform", "WpsConnector", "WP"),
                    new("Turn14", "Platform", "Turn14Connector", "T14"),
                    new("Parts Unlimited", "Platform", "PartsUnlimitedConnector", "PU")
                ]),
            new(
                "Financial Services",
                Icon: "FS",
                Children:
                [
                    new("Overview", "PlatformFinancialServices", "Index", "OV"),
                    new("Merchant Applications", "PlatformFinancialServices", "MerchantApplications", "MA"),
                    new("Active Merchants", "PlatformFinancialServices", "ActiveMerchants", "AM"),
                    new("Underwriting Queue", "PlatformFinancialServices", "UnderwritingQueue", "UQ"),
                    new("Risk Monitoring", "PlatformFinancialServices", "RiskMonitoring", "RM"),
                    new("Processor Management", "PlatformFinancialServices", "ProcessorManagement", "PM"),
                    new("Gateway Providers", "PlatformFinancialServices", "GatewayProviders", "GP"),
                    new("Residual Reporting", "PlatformFinancialServices", "ResidualReporting", "RR"),
                    new("Settlements", "PlatformFinancialServices", "SettlementMonitoring", "SM"),
                    new("Chargebacks", "PlatformFinancialServices", "ChargebackManagement", "CB"),
                    new("Hardware Catalog", "PlatformFinancialServices", "HardwareCatalog", "HC"),
                    new("Device Inventory", "PlatformFinancialServices", "DeviceInventory", "DI"),
                    new("Fulfillment", "PlatformFinancialServices", "ShippingFulfillment", "SF"),
                    new("Pricing Plans", "PlatformFinancialServices", "PricingPlans", "PP"),
                    new("Merchant Support", "PlatformFinancialServices", "MerchantSupport", "MS"),
                    new("Provider Config", "PlatformFinancialServices", "ProviderConfiguration", "PC")
                ]),
            new(
                "Marketing CMS",
                Icon: "MC",
                Children:
                [
                    new("Pages", "MarketingAdmin", "Index", "PG")
                ])
        ],
        CompanyItems:
        [
            new(
                "Administration",
                Icon: "OA",
                Children:
                [
                    new("Company Dashboard", "Business", "Dashboard", "CD"),
                    new("Company Admin", "Business", "Administration", "CA")
                ]),
            new(
                "Suppliers",
                Icon: "SU",
                Children:
                [
                    new("Item Search", "Business", "SupplierItemSearch", "IS"),
                    new("WPS", "Business", "SupplierWpsConnector", "WP"),
                    new("Parts Unlimited", "Business", "SupplierPartsUnlimitedConnector", "PU"),
                    new("Turn14", "Business", "SupplierTurn14Connector", "T14")
                ]),
            new(
                "Inventory",
                Icon: "IV",
                Children:
                [
                    new("Inventory Management", "Inventory", "Index", "IM"),
                    new("Add / Import", "Inventory", "Add", "AI"),
                    new("Scanner", "Inventory", "Scanner", "SC")
                ]),
            new(
                "Payments & Finance",
                Icon: "PF",
                Children:
                [
                    new("Dashboard", "FinancialServices", "Index", "DB"),
                    new("Merchant Account", "FinancialServices", "MerchantAccount", "MA"),
                    new("Transactions", "FinancialServices", "TransactionCenter", "TR"),
                    new("Payments", "Payments", "Index", "PY"),
                    new("Customer Wallet", "FinancialServices", "CustomerWallet", "CW"),
                    new("Payment Links", "FinancialServices", "PaymentLinks", "PL"),
                    new("Terminals", "FinancialServices", "TerminalManagement", "TM"),
                    new("ACH", "FinancialServices", "AchProcessing", "ACH"),
                    new("Subscriptions", "FinancialServices", "SubscriptionBilling", "SB"),
                    new("Financial Ledger", "FinancialServices", "FinancialLedger", "FL"),
                    new("Deposits", "FinancialServices", "Deposits", "DP"),
                    new("Statements", "FinancialServices", "Statements", "ST"),
                    new("Reports", "FinancialServices", "Reports", "RP"),
                    new("Settings", "FinancialServices", "Settings", "SE")
                ]),
            new(
                "CRM",
                Icon: "CR",
                Children:
                [
                    new("Customers", "Customers", "Index", "CU")
                ]),
            new(
                "Service",
                Icon: "SV",
                Children:
                [
                    new("Intake", "WorkOrders", "Intake", "IN"),
                    new("Work Orders", "WorkOrders", "Index", "WO")
                ]),
            new(
                "HR",
                Icon: "HR",
                Children:
                [
                    new("Employees", "Employees", "Index", "EM"),
                    new("Time Clock", "Hr", "TimeClock", "TC"),
                    new("Work Schedule", "Hr", "WorkSchedule", "WS"),
                    new("Time Off", "Hr", "TimeOff", "TO"),
                    new("Payroll", "Hr", "Payroll", "PY"),
                    new("Sales Commissions", "Hr", "SalesCommissions", "SC"),
                    new("Work Order Commissions", "Hr", "WorkOrderCommissions", "WC"),
                    new("Certifications", "Hr", "Certifications", "CE"),
                    new("Documents", "Hr", "Documents", "DC"),
                    new("OSHA / Safety", "Hr", "Safety", "OS"),
                    new("Company Assets", "Hr", "CompanyAssets", "AS"),
                    new("Performance Reviews", "Hr", "PerformanceReviews", "PR")
                ])
        ]);

    public static IReadOnlyCollection<IM1ShellNavigationItemModel> FilterCompanySupplierConnectors(
        IReadOnlyCollection<IM1ShellNavigationItemModel> items,
        IReadOnlySet<string> enabledSupplierConnectorCodes)
    {
        var hasEnabledSupplierConnector = enabledSupplierConnectorCodes.Count > 0;
        return items
            .Select(item => FilterCompanySupplierGroup(item, enabledSupplierConnectorCodes, hasEnabledSupplierConnector))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();
    }

    private static IM1ShellNavigationItemModel? FilterCompanySupplierGroup(
        IM1ShellNavigationItemModel item,
        IReadOnlySet<string> enabledSupplierConnectorCodes,
        bool hasEnabledSupplierConnector)
    {
        if (!string.Equals(item.Label, "Suppliers", StringComparison.OrdinalIgnoreCase) || item.Children is null)
        {
            return item;
        }

        var children = item.Children
            .Where(child =>
                string.Equals(child.Action, "SupplierItemSearch", StringComparison.OrdinalIgnoreCase)
                    ? hasEnabledSupplierConnector
                    : child.Action is not null &&
                        CompanySupplierConnectorActions.TryGetValue(child.Action, out var supplierCode) &&
                        enabledSupplierConnectorCodes.Contains(supplierCode))
            .ToArray();

        return children.Length == 0
            ? null
            : item with { Children = children };
    }
}
