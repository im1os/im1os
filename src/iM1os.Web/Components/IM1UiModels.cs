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

public sealed record IM1DialogModel(
    string Id,
    string Title,
    string? Description = null,
    IHtmlContent? Content = null,
    string PrimaryActionLabel = "Save",
    string SecondaryActionLabel = "Cancel",
    string Size = "md");

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

public sealed record IM1ShellNavigationModel(
    string ProductName,
    IReadOnlyCollection<IM1ShellNavigationItemModel> Items,
    string? CurrentController = null,
    string? CurrentAction = null);

public sealed record IM1ShellNavigationItemModel(
    string Label,
    string Controller,
    string Action,
    string? Icon = null,
    IReadOnlyCollection<IM1ShellNavigationItemModel>? Children = null);
