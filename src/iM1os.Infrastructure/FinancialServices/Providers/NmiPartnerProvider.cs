using System.Text;
using System.Text.Json;
using iM1os.Application.FinancialServices.Providers;
using iM1os.Infrastructure.Configuration;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace iM1os.Infrastructure.FinancialServices.Providers;

public sealed class NmiPartnerProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<NmiPaymentOptions> options) : IPartnerProvider
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

    public async Task<PartnerMerchantCreateResult> CreateMerchantAsync(
        PartnerMerchantCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(paymentOptions.PartnerOAuthClientId) ||
            string.IsNullOrWhiteSpace(paymentOptions.PartnerOAuthClientSecret))
        {
            throw new InvalidOperationException("NMI Sign-Up OAuth client credentials are not configured.");
        }

        var accessToken = await GetSignupAccessTokenAsync(cancellationToken);
        var applicationPayload = new Dictionary<string, object?>
        {
            ["package_id"] = string.IsNullOrWhiteSpace(paymentOptions.SignUpPackageId)
                ? "pkg_merrick_tsys"
                : paymentOptions.SignUpPackageId,
            ["fields"] = BuildSignupFields(request),
            ["collections"] = BuildSignupCollections(request)
        };

        var signupClient = httpClientFactory.CreateClient("NmiSignup");
        var idempotencyKey = IdempotencyKey("im1-create", request, applicationPayload);
        using var createMessage = new HttpRequestMessage(HttpMethod.Post, "applications")
        {
            Content = JsonContent(applicationPayload)
        };
        createMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        createMessage.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        createMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var createResponse = await signupClient.SendAsync(createMessage, cancellationToken);
        var createResponseJson = await createResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!createResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                NmiErrorMessage(createResponseJson) ?? $"NMI Sign-Up application creation failed with HTTP {(int)createResponse.StatusCode}.",
                null,
                createResponse.StatusCode);
        }

        using var createDocument = JsonDocument.Parse(createResponseJson);
        var applicationId = JsonValue(createDocument.RootElement, "id");
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            throw new InvalidOperationException("NMI Sign-Up application response did not include an application id.");
        }

        var legalConsentUrl = await GetLegalConsentUrlAsync(signupClient, accessToken, applicationId, cancellationToken);
        var rawResponse = JsonSerializer.Serialize(new
        {
            Application = JsonDocument.Parse(createResponseJson).RootElement,
            LegalConsentUrl = legalConsentUrl
        });

        return new PartnerMerchantCreateResult(
            ProviderCode,
            string.Empty,
            null,
            null,
            "LegalConsentRequired",
            rawResponse,
            applicationId);
    }

    public async Task<PartnerMerchantCreateResult> SubmitMerchantApplicationAsync(
        string providerReference,
        PartnerMerchantCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerReference))
        {
            throw new InvalidOperationException("NMI Sign-Up application reference is required.");
        }

        var accessToken = await GetSignupAccessTokenAsync(cancellationToken);
        var signupClient = httpClientFactory.CreateClient("NmiSignup");
        using var submitMessage = new HttpRequestMessage(
            HttpMethod.Post,
            $"applications/{Uri.EscapeDataString(providerReference)}/submit?skip_merchant_email=true")
        {
            Content = JsonContent(new { })
        };
        submitMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        submitMessage.Headers.TryAddWithoutValidation("Idempotency-Key", NewIdempotencyKey("im1-submit", providerReference));
        submitMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var submitResponse = await signupClient.SendAsync(submitMessage, cancellationToken);
        var submitResponseJson = await submitResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!submitResponse.IsSuccessStatusCode)
        {
            var errorMessage = NmiErrorMessage(submitResponseJson);
            if (!string.IsNullOrWhiteSpace(errorMessage) &&
                errorMessage.Contains("already been submitted", StringComparison.OrdinalIgnoreCase))
            {
                return new PartnerMerchantCreateResult(
                    ProviderCode,
                    string.Empty,
                    null,
                    null,
                    "UnderReview",
                    submitResponseJson,
                    providerReference);
            }

            throw new HttpRequestException(
                $"NMI Sign-Up application {providerReference}: {errorMessage ?? $"submit failed with HTTP {(int)submitResponse.StatusCode}."}",
                null,
                submitResponse.StatusCode);
        }

        return ToMerchantCreateResult(providerReference, submitResponseJson);
    }

    public async Task<PartnerMerchantCreateResult> GetMerchantApplicationStatusAsync(
        string providerReference,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerReference))
        {
            throw new InvalidOperationException("NMI Sign-Up application reference is required.");
        }

        var accessToken = await GetSignupAccessTokenAsync(cancellationToken);
        var signupClient = httpClientFactory.CreateClient("NmiSignup");
        using var message = new HttpRequestMessage(
            HttpMethod.Get,
            $"applications/{Uri.EscapeDataString(providerReference)}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await signupClient.SendAsync(message, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"NMI Sign-Up application {providerReference}: {NmiErrorMessage(responseJson) ?? $"status refresh failed with HTTP {(int)response.StatusCode}."}",
                null,
                response.StatusCode);
        }

        return ToMerchantCreateResult(providerReference, responseJson);
    }

    public async Task<PartnerMerchantCredentialResult> CreateMerchantCredentialAsync(
        PartnerMerchantCredentialRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(paymentOptions.PartnerApiKey))
        {
            throw new InvalidOperationException("NMI partner API key is not configured.");
        }

        var payload = new Dictionary<string, object?>
        {
            ["description"] = request.Description,
            ["permissions"] = request.Permissions
        };
        var client = httpClientFactory.CreateClient("NmiPartner");
        using var message = new HttpRequestMessage(HttpMethod.Post, $"merchants/{Uri.EscapeDataString(request.ProviderMerchantId)}/security_keys")
        {
            Content = JsonContent(payload)
        };
        message.Headers.TryAddWithoutValidation("Authorization", paymentOptions.PartnerApiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(message, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                NmiErrorMessage(responseJson) ?? $"NMI merchant key creation failed with HTTP {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;
        var rawKey = JsonValue(root, "key") ??
            JsonValue(root, "api_key") ??
            JsonValue(root, "security_key") ??
            JsonValue(root, "value");
        return new PartnerMerchantCredentialResult(
            JsonValue(root, "id") ?? JsonValue(root, "key_id"),
            JsonValue(root, "private_key") ?? JsonValue(root, "privateKey") ?? rawKey,
            JsonValue(root, "public_key") ?? JsonValue(root, "publicKey") ?? rawKey,
            JsonValue(root, "description") ?? request.Description,
            responseJson);
    }

    private async Task<string> GetSignupAccessTokenAsync(CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = paymentOptions.PartnerOAuthClientId,
            ["client_secret"] = paymentOptions.PartnerOAuthClientSecret,
            ["scope"] = new[] { "*" }
        };
        var client = httpClientFactory.CreateClient("NmiSignup");
        using var message = new HttpRequestMessage(HttpMethod.Post, "oauth/token")
        {
            Content = JsonContent(payload)
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(message, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                NmiErrorMessage(responseJson) ?? $"NMI Sign-Up OAuth token request failed with HTTP {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        using var document = JsonDocument.Parse(responseJson);
        return JsonValue(document.RootElement, "access_token") ??
            JsonValue(document.RootElement, "token") ??
            throw new InvalidOperationException("NMI Sign-Up OAuth response did not include an access token.");
    }

    private static PartnerMerchantCreateResult ToMerchantCreateResult(string applicationId, string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;
        var providerMerchantId =
            JsonValueRecursive(root, "gateway_id") ??
            JsonValueRecursive(root, "gatewayId") ??
            JsonValueRecursive(root, "merchantId") ??
            JsonValueRecursive(root, "merchant_id") ??
            JsonValueRecursive(root, "gateway_merchant_id") ??
            JsonValueRecursive(root, "gatewayMerchantId") ??
            string.Empty;

        return new PartnerMerchantCreateResult(
            "NMI",
            providerMerchantId,
            JsonValueRecursive(root, "username") ?? JsonValueRecursive(root, "gateway_username"),
            JsonValueRecursive(root, "password") ?? JsonValueRecursive(root, "gateway_password"),
            ProviderStatusFor(JsonValue(root, "status")),
            responseJson,
            applicationId);
    }

    private static string ProviderStatusFor(string? nmiStatus)
    {
        return nmiStatus?.Trim().ToLowerInvariant() switch
        {
            "approved" or "boarded" => "Active",
            "rejected" => "Rejected",
            "underwriter_requested_information" or "requested_information_sent" => "UnderReview",
            "submitted" => "UnderReview",
            "draft" => "Draft",
            _ => "UnderReview"
        };
    }

    private static async Task<string?> GetLegalConsentUrlAsync(
        HttpClient signupClient,
        string accessToken,
        string applicationId,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Get,
            $"applications/{Uri.EscapeDataString(applicationId)}/legal-consent");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await signupClient.SendAsync(message, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"NMI Sign-Up application {applicationId}: {NmiErrorMessage(responseJson) ?? $"legal consent URL request failed with HTTP {(int)response.StatusCode}."}",
                null,
                response.StatusCode);
        }

        using var document = JsonDocument.Parse(responseJson);
        return JsonValue(document.RootElement, "url") ??
            JsonValue(document.RootElement, "consent_url") ??
            JsonValue(document.RootElement, "legal_consent_url") ??
            JsonValue(document.RootElement, "legalConsentUrl") ??
            responseJson;
    }

    private static IReadOnlyCollection<object> BuildSignupFields(PartnerMerchantCreateRequest request)
    {
        var firstName = Clean(request.FirstName) ?? "Merchant";
        var lastName = Clean(request.LastName) ?? "Owner";
        var address = Truncate(Required(request.AddressLine1, "Address"), 32);
        var city = Truncate(Required(request.City, "City"), 13);
        var state = Clean(request.Region) ?? "TX";
        var postalCode = Clean(request.PostalCode) ?? "75001";
        var businessName = Required(request.LegalBusinessName, "Business name");
        var phone = FormatPhone(Clean(request.Phone)) ?? "2145551212";
        var email = Clean(request.Email) ?? "merchant@example.test";

        var fields = new List<object>();
        AddField(fields, "fld_pricing_type", "Flat Rate");
        AddField(fields, "fld_qual_rate", 2.75m);
        AddField(fields, "fld_qual_rate_per_auth", 0.10m);
        AddField(fields, "fld_average_ticket", 100);
        AddField(fields, "fld_high_ticket", 250);
        AddField(fields, "fld_monthly_volume", 5000);
        AddField(fields, "fld_percent_of_swiped", 100);
        AddField(fields, "fld_percent_key_entered", 0);
        AddField(fields, "fld_percent_of_ecommerce", 0);
        AddField(fields, "fld_percent_of_moto", 0);
        AddField(fields, "fld_years_in_business", 1);
        AddField(fields, "fld_business_nature", "Retail");
        AddField(fields, "fld_type_business", "LLC");
        AddField(fields, "fld_state_incorporated", state);
        AddField(fields, "fld_type_of_goods_sold", "Powersports service and parts");
        AddField(fields, "fld_mcc", "5533");
        AddField(fields, "fld_federal_tax_id", "821234567");
        AddField(fields, "fld_contact_name", $"{firstName} {lastName}".Trim());
        AddField(fields, "fld_legal_name", businessName);
        AddField(fields, "fld_legal_address", address);
        AddField(fields, "fld_legal_city", city);
        AddField(fields, "fld_legal_state", state);
        AddField(fields, "fld_legal_postal_code", postalCode);
        AddField(fields, "fld_legal_country", Clean(request.Country) ?? "US");
        AddField(fields, "fld_legal_phone_number", phone);
        AddField(fields, "fld_dba_name", businessName);
        AddField(fields, "fld_dba_address", address);
        AddField(fields, "fld_dba_city", city);
        AddField(fields, "fld_dba_state", state);
        AddField(fields, "fld_dba_postal_code", postalCode);
        AddField(fields, "fld_dba_country", Clean(request.Country) ?? "US");
        AddField(fields, "fld_dba_phone_number", phone);
        AddField(fields, "fld_dba_email", email);
        AddField(fields, "fld_business_website", Clean(request.Website));
        AddField(fields, "fld_individual_with_control_first_name", firstName);
        AddField(fields, "fld_individual_with_control_last_name", lastName);
        AddField(fields, "fld_individual_with_control_title", "Owner");
        AddField(fields, "fld_individual_with_control_address", address);
        AddField(fields, "fld_individual_with_control_city", city);
        AddField(fields, "fld_individual_with_control_state", state);
        AddField(fields, "fld_individual_with_control_postal_code", postalCode);
        AddField(fields, "fld_individual_with_control_country", Clean(request.Country) ?? "US");
        AddField(fields, "fld_individual_with_control_email", email);
        AddField(fields, "fld_individual_with_control_phone", phone);
        AddField(fields, "fld_individual_with_control_dob", "1980-01-01");
        AddField(fields, "fld_individual_with_control_ssn", "111223333");
        AddField(fields, "fld_individual_with_control_ownership_percentage", 100);

        return fields;
    }

    private static IReadOnlyCollection<object> BuildSignupCollections(PartnerMerchantCreateRequest request)
    {
        var firstName = Clean(request.FirstName) ?? "Merchant";
        var lastName = Clean(request.LastName) ?? "Owner";
        var address = Truncate(Required(request.AddressLine1, "Address"), 32);
        var city = Truncate(Required(request.City, "City"), 13);
        var state = Clean(request.Region) ?? "TX";
        var postalCode = Clean(request.PostalCode) ?? "75001";
        var country = Clean(request.Country) ?? "US";
        var businessName = Required(request.LegalBusinessName, "Business name");
        var email = Clean(request.Email) ?? "merchant@example.test";
        var phone = FormatOwnerPhone(Clean(request.Phone)) ?? "(214) 555-1212";

        var ownerFields = new List<object>();
        AddField(ownerFields, "fld_owner_ownership_percentage", 100);
        AddField(ownerFields, "fld_owner_address", address);
        AddField(ownerFields, "fld_owner_city", city);
        AddField(ownerFields, "fld_owner_country", country);
        AddField(ownerFields, "fld_owner_postal_code", postalCode);
        AddField(ownerFields, "fld_owner_state", state);
        AddField(ownerFields, "fld_owner_dob", "1980-01-01");
        AddField(ownerFields, "fld_owner_title", "Owner");
        AddField(ownerFields, "fld_owner_ssn", "111223333");
        AddField(ownerFields, "fld_owner_email", email);
        AddField(ownerFields, "fld_owner_phone_num", phone);
        AddField(ownerFields, "fld_owner_first_name", firstName);
        AddField(ownerFields, "fld_owner_last_name", lastName);

        var bankFields = new List<object>();
        AddField(bankFields, "fld_dda_account_routing_number", "111000025");
        AddField(bankFields, "fld_dda_name_on_account", businessName);
        AddField(bankFields, "fld_dda_account_number", "1234567890");

        return new object[]
        {
            Collection("col_owners", ownerFields),
            Collection("col_bank_info", bankFields)
        };
    }

    private static object Collection(string id, IReadOnlyCollection<object> fields)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = id,
            ["records"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = $"{id}_1",
                    ["fields"] = fields
                }
            }
        };
    }

    private static void AddField(ICollection<object> fields, string id, object? value)
    {
        if (value is null || (value is string text && string.IsNullOrWhiteSpace(text)))
        {
            return;
        }

        fields.Add(new Dictionary<string, object?>
        {
            ["id"] = id,
            ["value"] = value
        });
    }

    private static HttpContent JsonContent(object value)
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private static string IdempotencyKey(string prefix, PartnerMerchantCreateRequest request, object scope)
    {
        var source = Clean(request.ExternalIdentifier) ?? request.OrganizationId.ToString("N");
        var clean = new string(source.Where(char.IsLetterOrDigit).ToArray());
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(scope))))
            .ToLowerInvariant()[..16];
        var key = $"{prefix}-{clean}-{hash}";
        return key[..Math.Min(100, key.Length)];
    }

    private static string NewIdempotencyKey(string prefix, string scope)
    {
        var clean = new string((scope ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
        var key = $"{prefix}-{clean}-{Guid.NewGuid():N}";
        return key[..Math.Min(100, key.Length)];
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? FormatPhone(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 10
            ? digits
            : Clean(value);
    }

    private static string? FormatOwnerPhone(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 10
            ? $"({digits[..3]}) {digits[3..6]}-{digits[6..]}"
            : Clean(value);
    }

    private static string Required(string? value, string label)
    {
        return Clean(value) ?? throw new InvalidOperationException($"{label} is required.");
    }

    private static string BuildMerchantUsername(PartnerMerchantCreateRequest request)
    {
        var localPart = (Clean(request.Email)?.Split('@')[0] ?? request.OrganizationId.ToString("N"))
            .Where(char.IsLetterOrDigit)
            .Take(24)
            .ToArray();
        var prefix = localPart.Length == 0 ? "merchant" : new string(localPart);
        return $"{prefix}{request.OrganizationId:N}"[..32].ToLowerInvariant();
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

    private static string? NmiErrorMessage(string responseJson)
    {
        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var message = JsonValue(document.RootElement, "message") ??
                JsonValue(document.RootElement, "error") ??
                JsonValue(document.RootElement, "response_text");
            if (document.RootElement.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array)
            {
                var details = errors
                    .EnumerateArray()
                    .Select(error => error.ValueKind == JsonValueKind.String ? error.GetString() : error.ToString())
                    .Where(error => !string.IsNullOrWhiteSpace(error))
                    .Take(3)
                    .ToArray();
                if (details.Length > 0)
                {
                    return string.IsNullOrWhiteSpace(message)
                        ? string.Join("; ", details)
                        : $"{message}: {string.Join("; ", details)}";
                }
            }

            return message;
        }
        catch (JsonException)
        {
            return string.IsNullOrWhiteSpace(responseJson) ? null : responseJson;
        }
    }

    private static string? JsonValue(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                ? property.ToString()
                : null;
    }

    private static string? JsonValueRecursive(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(propertyName, out var property) &&
                property.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                return property.ToString();
            }

            foreach (var child in element.EnumerateObject())
            {
                var value = JsonValueRecursive(child.Value, propertyName);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                var value = JsonValueRecursive(child, propertyName);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
