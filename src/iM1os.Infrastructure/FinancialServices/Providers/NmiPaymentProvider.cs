using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using iM1os.Application.FinancialServices.Providers;
using iM1os.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace iM1os.Infrastructure.FinancialServices.Providers;

public sealed class NmiPaymentProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<NmiPaymentOptions> options) : IPaymentProvider
{
    private readonly NmiPaymentOptions paymentOptions = options.Value;

    public string ProviderCode => "NMI";

    public FinancialProviderConfiguration GetConfiguration()
    {
        var hasPaymentCredentials = !string.IsNullOrWhiteSpace(paymentOptions.MerchantPrivateKey);
        return new FinancialProviderConfiguration(
            hasPaymentCredentials && !string.IsNullOrWhiteSpace(paymentOptions.MerchantTokenizationKey),
            ProviderCode,
            paymentOptions.Environment,
            paymentOptions.MerchantTokenizationKey,
            paymentOptions.CollectJsUrl,
            hasPaymentCredentials,
            !string.IsNullOrWhiteSpace(paymentOptions.PartnerApiKey));
    }

    public async Task<ProviderPaymentResult> ProcessSaleAsync(ProviderPaymentSaleRequest request, CancellationToken cancellationToken)
    {
        var payload = RemoveNulls(new Dictionary<string, object?>
        {
            ["amount"] = request.Amount,
            ["currency"] = request.Currency,
            ["customer_receipt"] = !string.IsNullOrWhiteSpace(request.Email),
            ["industry"] = "retail",
            ["payment_details"] = new Dictionary<string, object?>
            {
                ["payment_token"] = request.PaymentToken
            },
            ["billing_address"] = new Dictionary<string, object?>
            {
                ["first_name"] = Clean(request.FirstName),
                ["last_name"] = Clean(request.LastName),
                ["email"] = Clean(request.Email),
                ["phone"] = Clean(request.Phone),
                ["address1"] = Clean(request.AddressLine1),
                ["city"] = Clean(request.City),
                ["state"] = Clean(request.Region),
                ["zip"] = Clean(request.PostalCode),
                ["country"] = Clean(request.Country) ?? "US"
            },
            ["order_details"] = new Dictionary<string, object?>
            {
                ["order_id"] = Clean(request.OrderId),
                ["description"] = Clean(request.Description)
            }
        });

        var client = httpClientFactory.CreateClient("NmiPayments");
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var message = new HttpRequestMessage(HttpMethod.Post, "payments/sale")
        {
            Content = content
        };
        message.Headers.TryAddWithoutValidation("Authorization", request.PaymentApiKey ?? paymentOptions.MerchantPrivateKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await client.SendAsync(message, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;
        if (!response.IsSuccessStatusCode)
        {
            var responseText = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity
                ? NmiValidationException.FromPaymentResponse(response.StatusCode, responseJson, request).Message
                : JsonValue(root, "message") ?? JsonValue(root, "response_text") ?? response.ReasonPhrase;
            return new ProviderPaymentResult(
                false,
                "Error",
                null,
                null,
                ((int)response.StatusCode).ToString(),
                responseText,
                null,
                null,
                null);
        }

        var responseCode = JsonValue(root, "response");
        var status = JsonValue(root, "status");
        var approved = responseCode == "1" || string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase);
        var normalizedStatus = approved
            ? "Approved"
            : string.Equals(responseCode, "2", StringComparison.OrdinalIgnoreCase)
                ? "Declined"
                : "Rejected";

        return new ProviderPaymentResult(
            approved,
            normalizedStatus,
            JsonValue(root, "id"),
            JsonValue(root, "auth_code"),
            responseCode,
            JsonValue(root, "response_text"),
            JsonValue(root, "card_type"),
            LastFour(JsonValue(root, "cc_number")),
            null);
    }

    private static IDictionary<string, object?> RemoveNulls(IDictionary<string, object?> values)
    {
        return values
            .Select(x => x.Value is IDictionary<string, object?> child
                ? new KeyValuePair<string, object?>(x.Key, RemoveNulls(child))
                : x)
            .Where(x => x.Value is not null && (x.Value is not IDictionary<string, object?> child || child.Count > 0))
            .ToDictionary(x => x.Key, x => x.Value);
    }

    private static string? JsonValue(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                ? property.ToString()
                : null;
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? LastFour(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? digits[^4..] : null;
    }
}
