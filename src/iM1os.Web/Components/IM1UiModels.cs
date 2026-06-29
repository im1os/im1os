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
    public static IM1ShellNavigationConfiguration AppShell { get; } = new(
        PlatformItems:
        [
            new(
                "Admin",
                Icon: "AD",
                Children:
                [
                    new("Dashboard", "Platform", "Dashboard", "DB")
                ]),
            new(
                "Company Manager",
                Icon: "TM",
                Children:
                [
                    new("Companies", "Platform", "Tenants", "CO")
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
                "CRM",
                Icon: "CR",
                Children:
                [
                    new("Customers", "Customers", "Index", "CU")
                ]),
            new(
                "HR",
                Icon: "HR",
                Children:
                [
                    new("Employees", "Employees", "Index", "EM"),
                    new("Time Clock", Icon: "TC"),
                    new("Work Schedule", Icon: "WS"),
                    new("Time Off", Icon: "TO"),
                    new("Payroll", Icon: "PY"),
                    new("Sales Commissions", Icon: "SC"),
                    new("Work Order Commissions", Icon: "WC"),
                    new("Certifications", Icon: "CE"),
                    new("Documents", Icon: "DC"),
                    new("OSHA / Safety", Icon: "OS"),
                    new("Company Assets", Icon: "AS"),
                    new("Performance Reviews", Icon: "PR")
                ])
        ]);
}
