namespace iM1os.Application.Platform;

public static class TenantModuleCatalog
{
    public const string WpsSupplierConnector = "SupplierConnector:WPS";
    public const string PartsUnlimitedSupplierConnector = "SupplierConnector:PU";
    public const string Turn14SupplierConnector = "SupplierConnector:TURN14";

    public static IReadOnlyCollection<TenantModuleCatalogItem> All { get; } =
    [
        new("Service", "Service", "Core", "Service workflow and shop operations."),
        new("Parts", "Parts", "Core", "Parts catalog and inventory workflows."),
        new("CustomerPortal", "Customer Portal", "Core", "Customer-facing account and status tools."),
        new("Reporting", "Reporting", "Core", "Operational reporting and dashboards."),
        new(WpsSupplierConnector, "WPS", "Suppliers", "Enable WPS in the company supplier workspace."),
        new(PartsUnlimitedSupplierConnector, "Parts Unlimited", "Suppliers", "Enable Parts Unlimited in the company supplier workspace."),
        new(Turn14SupplierConnector, "Turn14", "Suppliers", "Enable Turn14 in the company supplier workspace.")
    ];

    public static IReadOnlyCollection<TenantModuleCatalogItem> SupplierConnectors { get; } =
        All.Where(x => x.Key.StartsWith("SupplierConnector:", StringComparison.OrdinalIgnoreCase)).ToArray();

    public static string SupplierConnectorModuleKey(string supplierCode)
    {
        return $"SupplierConnector:{supplierCode.Trim().ToUpperInvariant()}";
    }

    public static string? SupplierCodeFromModuleKey(string moduleKey)
    {
        const string prefix = "SupplierConnector:";
        return moduleKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? moduleKey[prefix.Length..].Trim().ToUpperInvariant()
            : null;
    }
}

public sealed record TenantModuleCatalogItem(
    string Key,
    string Label,
    string Category,
    string Description);
