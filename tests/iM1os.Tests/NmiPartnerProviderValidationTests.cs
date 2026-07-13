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
        var factory = new TestHttpClientFactory(response);
        var provider = Provider(factory);

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
        using var payload = System.Text.Json.JsonDocument.Parse(factory.ApplicationPayload!);
        var fields = payload.RootElement.GetProperty("fields").EnumerateArray()
            .ToDictionary(
                field => field.GetProperty("id").GetString()!,
                field => field.GetProperty("value").ToString(),
                StringComparer.Ordinal);
        Assert.Equal("Retail", fields["fld_business_nature"]);
        Assert.Equal("Flat Rate", fields["fld_pricing_type"]);
        Assert.Equal("2.9", fields["fld_qual_rate"]);
        Assert.Equal("0.30", fields["fld_qual_rate_per_auth"]);
        Assert.Equal(50, fields["fld_type_of_goods_sold"].Length);
        Assert.Equal("NMI Sandbox Motorcycle Supply LL", fields["fld_legal_name"]);
        Assert.Equal("123456789", fields["fld_federal_tax_id"]);
    }

    [Fact]
    public async Task CreateMerchantAsync_DiscardsMalformedRawErrorResponse()
    {
        const string response = "not-json tax=123456789 ssn=111223333 token=oauth-secret";
        var provider = Provider(new TestHttpClientFactory(response));

        var exception = await Assert.ThrowsAsync<NmiValidationException>(() =>
            provider.CreateMerchantAsync(Request(), "stable-create-key", CancellationToken.None));

        Assert.Equal("NMI validation failed (HTTP 422).", exception.Message);
        Assert.Empty(exception.FieldIdentifiers);
        Assert.Empty(exception.ValidationCodes);
        Assert.Empty(exception.Descriptions);
    }

    [Fact]
    public void Payload_fingerprint_is_stable_for_same_payload_and_changes_with_corrected_payload()
    {
        var provider = Provider(new TestHttpClientFactory("{}"));
        var request = Request();

        var first = provider.GetMerchantApplicationPayloadFingerprint(request);
        var retry = provider.GetMerchantApplicationPayloadFingerprint(request);
        var corrected = provider.GetMerchantApplicationPayloadFingerprint(
            request with { LegalBusinessName = "Corrected Sandbox Company LLC" });

        Assert.Equal(64, first.Length);
        Assert.Equal(first, retry);
        Assert.NotEqual(first, corrected);
        Assert.Matches("^[A-F0-9]{64}$", first);
        Assert.DoesNotContain(request.TaxIdentifier!, first, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Submit_updates_existing_draft_with_configured_pricing_before_submission()
    {
        var factory = new SubmissionHttpClientFactory();
        var provider = Provider(factory);

        var result = await provider.SubmitMerchantApplicationAsync(
            "app_1234567890123456",
            Request(),
            "stable-update-key",
            "stable-submit-key",
            CancellationToken.None);

        Assert.Equal("UnderReview", result.Status);
        Assert.Equal([HttpMethod.Patch, HttpMethod.Post], factory.ApplicationRequests.Select(x => x.Method));
        Assert.Equal("stable-update-key", factory.ApplicationRequests[0].Headers.GetValues("Idempotency-Key").Single());
        Assert.Equal("stable-submit-key", factory.ApplicationRequests[1].Headers.GetValues("Idempotency-Key").Single());
        using var payload = System.Text.Json.JsonDocument.Parse(factory.UpdatePayload!);
        var pricing = payload.RootElement.GetProperty("fields").EnumerateArray()
            .Single(field => field.GetProperty("id").GetString() == "fld_pricing_type");
        Assert.Equal("Flat Rate", pricing.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Reconciliation_uses_read_only_list_and_detail_queries_and_finds_matching_application()
    {
        var factory = new ReconciliationHttpClientFactory();
        var provider = Provider(factory);

        var exists = await provider.HasMatchingMerchantApplicationAsync(Request(), CancellationToken.None);

        Assert.True(exists);
        Assert.Equal(2, factory.ApplicationRequests.Count);
        Assert.All(factory.ApplicationRequests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.Contains("per_page=100", factory.ApplicationRequests[0].RequestUri!.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("updated_from", factory.ApplicationRequests[0].RequestUri!.Query, StringComparison.Ordinal);
    }

    private static NmiPartnerProvider Provider(IHttpClientFactory factory)
    {
        return new NmiPartnerProvider(
            factory,
            Options.Create(new NmiPaymentOptions
            {
                PartnerOAuthClientId = "oauth-client",
                PartnerOAuthClientSecret = "oauth-secret",
                SignUpPackageId = "pkg_merrick_tsys",
                SignUpPricingType = "Flat Rate",
                SignUpQualifiedRate = 2.9m,
                SignUpQualifiedRatePerAuthorization = 0.30m
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

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly TestHandler handler;
        private readonly HttpClient client;

        public TestHttpClientFactory(string validationResponse)
        {
            handler = new TestHandler(validationResponse);
            client = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://sandbox.signup.nmi.com/api/v1/")
            };
        }

        public string? ApplicationPayload => handler.ApplicationPayload;

        public HttpClient CreateClient(string name) => client;
    }

    private sealed class ReconciliationHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient client;

        public ReconciliationHttpClientFactory()
        {
            var handler = new ReconciliationHandler();
            ApplicationRequests = handler.ApplicationRequests;
            client = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://sandbox.signup.nmi.com/api/v1/")
            };
        }

        public List<HttpRequestMessage> ApplicationRequests { get; }

        public HttpClient CreateClient(string name) => client;

        private sealed class ReconciliationHandler : HttpMessageHandler
        {
            public List<HttpRequestMessage> ApplicationRequests { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                if (request.RequestUri?.AbsolutePath.EndsWith("/oauth/token", StringComparison.Ordinal) == true)
                {
                    return Task.FromResult(Response(HttpStatusCode.OK, "{\"access_token\":\"oauth-secret\"}"));
                }

                ApplicationRequests.Add(request);
                if (request.RequestUri?.AbsolutePath.EndsWith("/applications", StringComparison.Ordinal) == true)
                {
                    return Task.FromResult(Response(HttpStatusCode.OK, """
                        {
                          "applications": [{ "id": "app_1234567890123456", "status": "draft" }],
                          "current_page": 1,
                          "last_page": 1,
                          "per_page": 100,
                          "total": 1
                        }
                        """));
                }

                return Task.FromResult(Response(HttpStatusCode.OK, """
                    {
                      "id": "app_1234567890123456",
                      "fields": [
                        { "id": "fld_legal_name", "value": "NMI Sandbox Motorcycle Supply LL" },
                        { "id": "fld_dba_name", "value": "NMI Sandbox Moto Supply" },
                        { "id": "fld_legal_address", "value": "normalized address" },
                        { "id": "fld_legal_city", "value": "Austin" },
                        { "id": "fld_legal_state", "value": "TX" },
                        { "id": "fld_legal_country", "value": "US" },
                        { "id": "fld_legal_postal_code", "value": "normalized postal" }
                      ]
                    }
                    """));
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

    private sealed class SubmissionHttpClientFactory : IHttpClientFactory
    {
        private readonly SubmissionHandler handler = new();
        private readonly HttpClient client;

        public SubmissionHttpClientFactory()
        {
            client = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://sandbox.signup.nmi.com/api/v1/")
            };
        }

        public List<HttpRequestMessage> ApplicationRequests => handler.ApplicationRequests;

        public string? UpdatePayload => handler.UpdatePayload;

        public HttpClient CreateClient(string name) => client;

        private sealed class SubmissionHandler : HttpMessageHandler
        {
            public List<HttpRequestMessage> ApplicationRequests { get; } = [];

            public string? UpdatePayload { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                if (request.RequestUri?.AbsolutePath.EndsWith("/oauth/token", StringComparison.Ordinal) == true)
                {
                    return Response(HttpStatusCode.OK, "{\"access_token\":\"oauth-secret\"}");
                }

                ApplicationRequests.Add(request);
                if (request.Method == HttpMethod.Patch)
                {
                    UpdatePayload = await request.Content!.ReadAsStringAsync(cancellationToken);
                    return Response(HttpStatusCode.OK, "{\"id\":\"app_1234567890123456\",\"status\":\"draft\"}");
                }

                return Response(HttpStatusCode.OK, "{\"id\":\"app_1234567890123456\",\"status\":\"submitted\"}");
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

    private sealed class TestHandler(string validationResponse) : HttpMessageHandler
    {
        public string? ApplicationPayload { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/oauth/token", StringComparison.Ordinal) == true)
            {
                return Response(HttpStatusCode.OK, "{\"access_token\":\"oauth-secret\"}");
            }

            Assert.Equal("stable-create-key", request.Headers.GetValues("Idempotency-Key").Single());
            ApplicationPayload = await request.Content!.ReadAsStringAsync(cancellationToken);
            return Response(HttpStatusCode.UnprocessableEntity, validationResponse);
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
