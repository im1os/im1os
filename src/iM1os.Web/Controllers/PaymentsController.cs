using System.Security.Claims;
using iM1os.Application.FinancialServices.Payments;
using iM1os.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

[Authorize]
public sealed class PaymentsController(IPaymentService paymentsService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return RedirectToMissingOrganizationContext();
        }

        ViewBag.OrganizationId = organizationId;
        return View(await paymentsService.GetWorkspaceAsync(organizationId, cancellationToken));
    }

    [HttpGet]
    public Task<IActionResult> TestPayment(CancellationToken cancellationToken)
    {
        return Index(cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSale(PaymentSaleForm form, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        try
        {
            var result = await paymentsService.CreateSaleAsync(
                organizationId,
                UserId(),
                new PaymentSaleRequest(
                    form.PaymentToken,
                    form.Amount,
                    form.Currency,
                    form.LocationId,
                    form.OrderId,
                    form.ReferenceType,
                    form.ReferenceId,
                    form.Description,
                    form.FirstName,
                    form.LastName,
                    form.Email,
                    form.Phone,
                    form.AddressLine1,
                    form.City,
                    form.Region,
                    form.PostalCode,
                    form.Country,
                    form.CardBrand,
                    form.CardLastFour),
                cancellationToken);

            TempData[result.Success ? "PaymentsStatus" : "PaymentsError"] = result.Success
                ? $"Payment approved. Transaction {result.GatewayTransactionId}."
                : $"Payment was not approved. {result.ResponseText ?? result.Status}";
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            TempData["PaymentsError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { organizationId });
    }

    private Guid OrganizationId()
    {
        return TryOrganizationId(out var organizationId)
            ? organizationId
            : throw new UnauthorizedAccessException("An organization context is required.");
    }

    private bool TryOrganizationId(out Guid organizationId)
    {
        var value = User.FindFirstValue("organization_id")
            ?? Request.Query["organizationId"].FirstOrDefault()
            ?? (Request.HasFormContentType ? Request.Form["organizationId"].FirstOrDefault() : null);

        return Guid.TryParse(value, out organizationId);
    }

    private Guid UserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("platform_user_id");

        return Guid.TryParse(userId, out var parsed)
            ? parsed
            : throw new UnauthorizedAccessException("A user context is required.");
    }

    private IActionResult RedirectToMissingOrganizationContext()
    {
        return User.FindFirstValue("platform_user_id") is not null
            ? RedirectToAction("Tenants", "Platform")
            : RedirectToAction("Login", "Account", new { returnUrl = Request.Path + Request.QueryString });
    }
}

public sealed class PaymentSaleForm
{
    public string PaymentToken { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? Currency { get; set; }

    public Guid? LocationId { get; set; }

    public string? OrderId { get; set; }

    public string? ReferenceType { get; set; }

    public string? ReferenceId { get; set; }

    public string? Description { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? AddressLine1 { get; set; }

    public string? City { get; set; }

    public string? Region { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    public string? CardBrand { get; set; }

    public string? CardLastFour { get; set; }
}
