using System.Net;
using System.Text;
using iM1os.Application.FinancialServices.Providers;
using iM1os.Infrastructure.Configuration;
using iM1os.Infrastructure.FinancialServices.Providers;
using Microsoft.Extensions.Options;

namespace iM1os.Tests;

public sealed class NmiPartnerProviderValidationTests
{
    [Fact]
    public async Task CreateMerchantAsync_CapturesOnlyAllowlistedValidationData()
    {
        const string response = """
            {
              "message": "Validation failed for NMI Sandbox Motorcycle Supply LLC",
              "code": "validation_failed",
              "errors": [
                {
                  "field": "fld_federal_tax_id",
                  "code": "invalid_format",
                  "description": "Value format is invalid."
                },
                {
                  "field_id": "fld_owner_ssn",
                  "validation_code": "invalid_value",
                  "message": "Submitted SSN 111223333 is invalid."
                },
                {
                  "attribute": "col_bank_info",
                  "error_code": "required",
                  "detail": "Bank account 1234567890 is invalid."
                },
                {
                  "field": "fld_business_nature",
                  "code": "too_long",
                  "description": "Description exceeds the allowed length."
                },
                {
                  "error": "Bearer token oauth-secret must not be retained."
                }
              ]
            }
            """;
        var provider = Provider(response);

        var exception = await Assert.ThrowsAsync<NmiValidationException>(() =>
            provider.CreateMerchantAsync(Request(), "stable-create-key", CancellationToken.None));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, exception.StatusCode);
        Assert.Equal(
            ["col_bank_info", "fld_business_nature", "fld_federal_tax_id", "fld_owner_ssn"],
            exception.FieldIdentifiers);
        Assert.Equal(
            ["invalid_format", "invalid_value", "required", "too_long", "validation_failed"],
            exception.ValidationCodes);
        Assert.Contains("Value format is invalid.", exception.Descriptions);
        Assert.Contains("Description exceeds the allowed length.", exception.Descriptions);
        Assert.DoesNotContain("NMI Sandbox Motorcycle Supply LLC", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("111223333", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("1234567890", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("oauth-secret", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer token", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"errors\"", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMerchantAsync_DiscardsMalformedRawErrorResponse()
    {
        const string response = "not-json tax=123456789 ssn=111223333 token=oauth-secret";
        var provider = Provider(response);

        var exception = await Assert.ThrowsAsync<NmiValidationException>(() =>
            provider.CreateMerchantAsync(Request(), "stable-create-key", CancellationToken.None));

        Assert.Equal("NMI validation failed (HTTP 422).", exception.Message);
        Assert.Empty(exception.FieldIdentifiers);
        Assert.Empty(exception.ValidationCodes);
        Assert.Empty(exception.Descriptions);
    }

    private static NmiPartnerProvider Provider(string validationResponse)
    {
        return new NmiPartnerProvider(
            new TestHttpClientFactory(validationResponse),
            Options.Create(new NmiPaymentOptions
            {
                PartnerOAuthClientId = "oauth-client",
                PartnerOAuthClientSecret = "oauth-secret",
                SignUpPackageId = "pkg_merrick_tsys"
            }));
    }

    private static PartnerMerchantCreateRequest Request()
    {
        return new PartnerMerchantCreateRequest(
            Guid.Parse("4dc0ce9e-8ff9-41eb-ac41-97b7e629c60a"),
            "NMI Sandbox Motorcycle Supply LLC",
            "US",
            "100 Sandbox Way",
            null,
            "Austin",
            "TX",
            "78701",
            "America/Chicago",
            "NMI Sandbox",
            "Signer",
            "nmi-sandbox-signer@example.com",
            "5125550199",
            "https://example.com",
            null,
            "NMI Sandbox Moto Supply",
            "123456789",
            "LLC",
            "Motorcycle parts, accessories, and service for NMI sandbox acceptance testing.",
            10,
            "Owner",
            100m,
            "1985-01-15",
            "111223333",
            "NMI Sandbox Test Bank",
            "111000025",
            "1234567890",
            25000m,
            250m,
            1500m,
            70m,
            10m,
            15m,
            5m,
            "5533");
    }

    private sealed class TestHttpClientFactory(string validationResponse) : IHttpClientFactory
    {
        private readonly HttpClient client = new(new TestHandler(validationResponse))
        {
            BaseAddress = new Uri("https://sandbox.signup.nmi.com/api/v1/")
        };

        public HttpClient CreateClient(string name) => client;
    }

    private sealed class TestHandler(string validationResponse) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/oauth/token", StringComparison.Ordinal) == true)
            {
                return Task.FromResult(Response(HttpStatusCode.OK, "{\"access_token\":\"oauth-secret\"}"));
            }

            Assert.Equal("stable-create-key", request.Headers.GetValues("Idempotency-Key").Single());
            return Task.FromResult(Response(HttpStatusCode.UnprocessableEntity, validationResponse));
        }

        private static HttpResponseMessage Response(HttpStatusCode status, string body)
        {
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
