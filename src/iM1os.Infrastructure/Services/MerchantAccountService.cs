using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using iM1os.Application.Common;
using iM1os.Application.FinancialServices.Merchant;
using iM1os.Application.FinancialServices.Providers;
using iM1os.Domain.FinancialServices.Events;
using iM1os.Domain.FinancialServices.Merchant;
using iM1os.Infrastructure.FinancialServices.Providers;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class MerchantAccountService(
    IApplicationDbContext dbContext,
    IEnumerable<IPartnerProvider> partnerProviders,
    IDomainEventRecorder domainEventRecorder,
    IDateTimeProvider dateTimeProvider,
    ISecretProtector secretProtector) : IMerchantAccountService
{
    private const string ApplicationCreateMetadataProperty = "ApplicationCreateOperation";
    private const string ApplicationUpdateMetadataProperty = "ApplicationUpdateOperation";
    private const string DefinitiveValidationFailure = "DefinitiveValidation";
    private const string AmbiguousFailure = "Ambiguous";
    private static readonly IReadOnlySet<string> ApplicationStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        MerchantAccountStatuses.Submitted,
        MerchantAccountStatuses.UnderReview,
        MerchantAccountStatuses.Approved,
        MerchantAccountStatuses.LegalConsentRequired,
        MerchantAccountStatuses.CredentialProvisioning,
        MerchantAccountStatuses.Rejected
    };

    private static readonly IReadOnlySet<string> PortfolioStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        MerchantAccountStatuses.Active,
        MerchantAccountStatuses.Suspended,
        MerchantAccountStatuses.Closed
    };

    public async Task<CompanyMerchantAccountWorkspace> GetCompanyMerchantAccountAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var merchantAccount = await dbContext.MerchantAccounts.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
        if (merchantAccount is null)
        {
            return new CompanyMerchantAccountWorkspace(
                organizationId,
                null,
                EmptyApplication(organizationId),
                Array.Empty<MerchantStatusHistoryRow>(),
                new[] { "Submit a merchant account application." },
                false,
                new MerchantLegalConsentModel(false, null, null, false));
        }

        var relationship = await GetPrimaryRelationshipAsync(merchantAccount, cancellationToken);
        var history = await GetHistoryRowsAsync(merchantAccount.Id, cancellationToken);
        var summary = ToSummary(merchantAccount, relationship);

        return new CompanyMerchantAccountWorkspace(
            organizationId,
            summary,
            ToApplicationForm(merchantAccount),
            history,
            RequiredNextSteps(summary),
            summary.IsProcessingReady,
            ToLegalConsent(merchantAccount, relationship));
    }

    public async Task<PlatformMerchantApplicationsWorkspace> GetPlatformMerchantApplicationsAsync(CancellationToken cancellationToken)
    {
        var rows = await GetPlatformRowsAsync(ApplicationStatuses, cancellationToken);
        return new PlatformMerchantApplicationsWorkspace(
            rows,
            rows.Count(x => string.Equals(x.Status, MerchantAccountStatuses.Submitted, StringComparison.OrdinalIgnoreCase)),
            rows.Count(x => string.Equals(x.Status, MerchantAccountStatuses.UnderReview, StringComparison.OrdinalIgnoreCase)),
            rows.Count(x => string.Equals(x.Status, MerchantAccountStatuses.Approved, StringComparison.OrdinalIgnoreCase)),
            rows.Count(x => string.Equals(x.Status, MerchantAccountStatuses.Rejected, StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<PlatformMerchantApplicationDetail> GetPlatformMerchantApplicationAsync(
        Guid organizationId,
        Guid merchantAccountId,
        CancellationToken cancellationToken)
    {
        var merchantAccount = await dbContext.MerchantAccounts.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                x => x.OrganizationId == organizationId && x.Id == merchantAccountId,
                cancellationToken)
            ?? throw new InvalidOperationException("Merchant application was not found.");
        var relationship = await GetPrimaryRelationshipAsync(merchantAccount, cancellationToken);
        var history = await GetHistoryRowsAsync(merchantAccount.Id, cancellationToken);
        var companyName = await dbContext.Organizations.IgnoreQueryFilters()
            .Where(x => x.Id == organizationId)
            .Select(x => x.Name)
            .SingleOrDefaultAsync(cancellationToken);

        return new PlatformMerchantApplicationDetail(
            ToPlatformRow(merchantAccount, relationship, companyName, history),
            ToApplicationForm(merchantAccount));
    }

    public async Task<PlatformActiveMerchantsWorkspace> GetPlatformActiveMerchantsAsync(CancellationToken cancellationToken)
    {
        var rows = await GetPlatformRowsAsync(PortfolioStatuses, cancellationToken);
        return new PlatformActiveMerchantsWorkspace(
            rows,
            rows.Count(x => string.Equals(x.Status, MerchantAccountStatuses.Active, StringComparison.OrdinalIgnoreCase)),
            rows.Count(x => string.Equals(x.Status, MerchantAccountStatuses.Suspended, StringComparison.OrdinalIgnoreCase)),
            rows.Count(x => string.Equals(x.Status, MerchantAccountStatuses.Closed, StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<MerchantAccountResult> OnboardMerchantAsync(
        Guid organizationId,
        Guid actorUserId,
        MerchantOnboardingRequest request,
        CancellationToken cancellationToken)
    {
        return await SaveDraftAsync(
            organizationId,
            actorUserId,
            new MerchantApplicationRequest(
                BusinessName: request.LegalBusinessName,
                Dba: null,
                Ein: null,
                TaxId: null,
                BusinessType: null,
                BusinessDescription: null,
                YearsInBusiness: null,
                PhysicalAddressLine1: request.AddressLine1,
                PhysicalAddressLine2: request.AddressLine2,
                PhysicalCity: request.City,
                PhysicalRegion: request.Region,
                PhysicalPostalCode: request.PostalCode,
                PhysicalCountry: request.Country,
                MailingAddressLine1: null,
                MailingAddressLine2: null,
                MailingCity: null,
                MailingRegion: null,
                MailingPostalCode: null,
                MailingCountry: null,
                OwnerName: $"{request.FirstName} {request.LastName}".Trim(),
                OwnerEmail: request.Email,
                OwnerPhone: request.Phone,
                OwnerTitle: null,
                OwnerOwnershipPercentage: null,
                OwnerDateOfBirth: null,
                OwnerSsn: null,
                BankName: null,
                BankRoutingNumber: null,
                BankAccountNumber: null,
                ExpectedMonthlyVolume: null,
                AverageTicket: null,
                HighTicket: null,
                CardPresentPercentage: null,
                KeyEnteredPercentage: null,
                EcommercePercentage: null,
                MotoPercentage: null,
                Website: request.Website,
                Mcc: null,
                ProviderCode: request.ProviderCode),
            cancellationToken);
    }

    public async Task<MerchantAccountResult> SaveDraftAsync(
        Guid organizationId,
        Guid actorUserId,
        MerchantApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var provider = ResolveProvider(request.ProviderCode);
        var merchantAccount = await dbContext.MerchantAccounts.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
        if (merchantAccount is null)
        {
            merchantAccount = await GetOrCreateMerchantAccountAsync(
                organizationId,
                actorUserId,
                provider.ProviderCode,
                MerchantAccountStatuses.Draft,
                cancellationToken);
        }

        EnsureApplicationEditable(merchantAccount);
        ApplyApplication(merchantAccount, request, provider.ProviderCode);
        var relationship = await GetPrimaryRelationshipAsync(merchantAccount, cancellationToken);
        if (merchantAccount.Status is MerchantAccountStatuses.Draft or MerchantAccountStatuses.Rejected)
        {
            await TransitionAsync(
                merchantAccount,
                relationship,
                actorUserId,
                MerchantAccountStatuses.Draft,
                "Merchant application draft saved.",
                cancellationToken);
        }
        else
        {
            merchantAccount.UpdatedAtUtc = dateTimeProvider.UtcNow;
            merchantAccount.UpdatedByUserId = actorUserId.ToString();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToResult(merchantAccount, relationship);
    }

    public async Task<MerchantAccountResult> SubmitApplicationAsync(
        Guid organizationId,
        Guid actorUserId,
        MerchantApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var provider = ResolveProvider(request.ProviderCode);
        var merchantAccount = await dbContext.MerchantAccounts.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
        if (merchantAccount is null)
        {
            merchantAccount = await GetOrCreateMerchantAccountAsync(
                organizationId,
                actorUserId,
                provider.ProviderCode,
                MerchantAccountStatuses.Draft,
                cancellationToken);
        }

        EnsureApplicationSubmittable(merchantAccount);
        ApplyApplication(merchantAccount, request, provider.ProviderCode);
        ValidateApplication(merchantAccount);
        var relationship = await GetPrimaryRelationshipAsync(merchantAccount, cancellationToken);
        if (string.Equals(merchantAccount.Status, MerchantAccountStatuses.Submitted, StringComparison.OrdinalIgnoreCase))
        {
            merchantAccount.UpdatedAtUtc = dateTimeProvider.UtcNow;
            merchantAccount.UpdatedByUserId = actorUserId.ToString();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await TransitionAsync(
                merchantAccount,
                relationship,
                actorUserId,
                MerchantAccountStatuses.Submitted,
                "Merchant application submitted.",
                cancellationToken);
        }

        return ToResult(merchantAccount, relationship);
    }

    public async Task<MerchantAccountResult> ApproveApplicationAsync(
        Guid organizationId,
        Guid merchantAccountId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var merchantAccount = await dbContext.MerchantAccounts.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == merchantAccountId, cancellationToken)
            ?? throw new InvalidOperationException("Merchant application was not found.");
        if (string.Equals(merchantAccount.Status, MerchantAccountStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A rejected merchant application cannot be approved without company resubmission.");
        }
        if (merchantAccount.Status is not MerchantAccountStatuses.Submitted and
            not MerchantAccountStatuses.Approved and
            not MerchantAccountStatuses.LegalConsentRequired)
        {
            throw new InvalidOperationException($"A merchant application in {merchantAccount.Status} status cannot be approved.");
        }

        ValidateApplication(merchantAccount);
        var provider = ResolveProvider(merchantAccount.PrimaryProviderCode);
        var relationship = await GetOrCreateRelationshipAsync(merchantAccount, provider.ProviderCode, cancellationToken);
        if (merchantAccount.Status == MerchantAccountStatuses.Submitted)
        {
            await TransitionAsync(
                merchantAccount,
                relationship,
                actorUserId,
                MerchantAccountStatuses.Approved,
                "Merchant application approved by Platform review.",
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(relationship.ProviderReference))
        {
            return ToResult(merchantAccount, relationship);
        }

        var providerRequest = ToProviderRequest(merchantAccount);
        var payloadFingerprint = provider.GetMerchantApplicationPayloadFingerprint(providerRequest);
        var createMetadata = ReadApplicationCreateMetadata(relationship.CapabilitiesJson);
        if (string.IsNullOrWhiteSpace(relationship.ApplicationCreateIdempotencyKey))
        {
            relationship.ApplicationCreateIdempotencyKey = StableIdempotencyKey("nmi-application-create-v1", merchantAccount.Id);
            createMetadata = NewApplicationCreateMetadata(payloadFingerprint, dateTimeProvider.UtcNow);
            relationship.CapabilitiesJson = SetApplicationCreateMetadata(relationship.CapabilitiesJson, createMetadata);
        }
        else if (createMetadata is null)
        {
            if (TryLegacyDefinitiveValidationReason(relationship.LastProviderError, out var legacyReasonCode))
            {
                createMetadata = new ApplicationCreateOperationMetadata(
                    1,
                    "LEGACY_UNKNOWN",
                    relationship.CreatedAtUtc,
                    null,
                    null,
                    DefinitiveValidationFailure,
                    legacyReasonCode,
                    relationship.UpdatedAtUtc,
                    null);
            }
            else
            {
                createMetadata = NewApplicationCreateMetadata(payloadFingerprint, relationship.CreatedAtUtc);
                relationship.CapabilitiesJson = SetApplicationCreateMetadata(relationship.CapabilitiesJson, createMetadata);
            }
        }

        if (!string.Equals(createMetadata.PayloadFingerprint, payloadFingerprint, StringComparison.Ordinal) ||
            RequiresLegacyPayloadBoundRotation(
                relationship.ApplicationCreateIdempotencyKey,
                createMetadata,
                payloadFingerprint))
        {
            createMetadata = await RotateApplicationCreateKeyAsync(
                merchantAccount,
                relationship,
                provider,
                providerRequest,
                payloadFingerprint,
                createMetadata,
                cancellationToken);
        }

        relationship.LastProviderError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        try
        {
            var providerResult = await provider.CreateMerchantAsync(
                providerRequest,
                relationship.ApplicationCreateIdempotencyKey,
                cancellationToken);
            return await ApplyProviderMerchantResultAsync(
                merchantAccount,
                relationship,
                provider,
                providerResult,
                actorUserId,
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException or TaskCanceledException)
        {
            var failure = ClassifyApplicationCreateFailure(ex, cancellationToken);
            createMetadata = createMetadata with
            {
                LastFailureClassification = failure.Classification,
                LastFailureReasonCode = failure.ReasonCode,
                LastFailureAtUtc = dateTimeProvider.UtcNow
            };
            relationship.CapabilitiesJson = SetApplicationCreateMetadata(relationship.CapabilitiesJson, createMetadata);
            relationship.LastProviderError = failure.Classification == AmbiguousFailure
                ? "NMI application creation outcome is ambiguous. Retry only with the same payload and idempotency key."
                : SafeProviderError(ex, "NMI application creation failed.");
            await dbContext.SaveChangesAsync(cancellationToken);
            if (failure.Classification == AmbiguousFailure)
            {
                throw new InvalidOperationException(relationship.LastProviderError, ex);
            }
            throw;
        }
    }

    public async Task<MerchantAccountResult> CompleteLegalConsentAsync(
        Guid organizationId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var merchantAccount = await dbContext.MerchantAccounts.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken)
            ?? throw new InvalidOperationException("Merchant application was not found.");
        if (merchantAccount.Status != MerchantAccountStatuses.LegalConsentRequired)
        {
            throw new InvalidOperationException("Legal consent is not currently required for this merchant application.");
        }

        var provider = ResolveProvider(merchantAccount.PrimaryProviderCode);
        var relationship = await GetPrimaryRelationshipAsync(merchantAccount, cancellationToken)
            ?? throw new InvalidOperationException("Provider relationship was not found.");
        if (string.IsNullOrWhiteSpace(relationship.ProviderReference))
        {
            throw new InvalidOperationException("NMI application reference is required before legal consent can be completed.");
        }

        relationship.LegalConsentCompletedAtUtc ??= dateTimeProvider.UtcNow;
        relationship.ApplicationSubmitIdempotencyKey ??= StableIdempotencyKey("nmi-application-submit", merchantAccount.Id);
        var providerRequest = ToProviderRequest(merchantAccount);
        var payloadFingerprint = provider.GetMerchantApplicationPayloadFingerprint(providerRequest);
        var updateMetadata = ReadApplicationUpdateMetadata(relationship.CapabilitiesJson);
        if (updateMetadata is null)
        {
            updateMetadata = new ApplicationUpdateOperationMetadata(
                1,
                payloadFingerprint,
                StableIdempotencyKey("nmi-application-update-v1", merchantAccount.Id),
                dateTimeProvider.UtcNow,
                null,
                null,
                null);
        }
        else if (!string.Equals(updateMetadata.PayloadFingerprint, payloadFingerprint, StringComparison.Ordinal))
        {
            if (!TryLegacyDefinitiveValidationReason(relationship.LastProviderError, out var reasonCode))
            {
                throw new InvalidOperationException(
                    "NMI application update key rotation is allowed only after a definitive validation failure.");
            }

            var nextVersion = checked(updateMetadata.KeyVersion + 1);
            var rotatedAtUtc = dateTimeProvider.UtcNow;
            updateMetadata = updateMetadata with
            {
                KeyVersion = nextVersion,
                PayloadFingerprint = payloadFingerprint,
                IdempotencyKey = StableIdempotencyKey($"nmi-application-update-v{nextVersion}", merchantAccount.Id),
                RotatedAtUtc = rotatedAtUtc,
                RotationReasonCode = SanitizeReasonCode(reasonCode) ?? "VALIDATION_ERROR",
                Rotations = (updateMetadata.Rotations ?? [])
                    .Append(new ApplicationUpdateRotationAudit(
                        nextVersion,
                        payloadFingerprint,
                        rotatedAtUtc,
                        SanitizeReasonCode(reasonCode) ?? "VALIDATION_ERROR"))
                    .ToArray()
            };
        }

        relationship.CapabilitiesJson = SetApplicationUpdateMetadata(relationship.CapabilitiesJson, updateMetadata);
        relationship.LastProviderError = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var providerResult = await provider.SubmitMerchantApplicationAsync(
                relationship.ProviderReference,
                providerRequest,
                updateMetadata.IdempotencyKey,
                relationship.ApplicationSubmitIdempotencyKey,
                cancellationToken);
            relationship.ProviderApplicationSubmittedAtUtc ??= dateTimeProvider.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return await ApplyProviderMerchantResultAsync(
                merchantAccount,
                relationship,
                provider,
                providerResult,
                actorUserId,
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException)
        {
            if (ex.Message.Contains("legal consent", StringComparison.OrdinalIgnoreCase))
            {
                relationship.LegalConsentCompletedAtUtc = null;
            }
            relationship.LastProviderError = SafeProviderError(ex, "NMI application submission failed.");
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<MerchantAccountResult> RefreshProviderStatusAsync(
        Guid organizationId,
        Guid merchantAccountId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var merchantAccount = await dbContext.MerchantAccounts.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == merchantAccountId, cancellationToken)
            ?? throw new InvalidOperationException("Merchant application was not found.");
        if (merchantAccount.Status == MerchantAccountStatuses.Active)
        {
            return ToResult(merchantAccount, await GetPrimaryRelationshipAsync(merchantAccount, cancellationToken));
        }
        if (merchantAccount.Status is not MerchantAccountStatuses.UnderReview and
            not MerchantAccountStatuses.CredentialProvisioning)
        {
            throw new InvalidOperationException($"NMI status cannot be refreshed while the merchant is {merchantAccount.Status}.");
        }
        var provider = ResolveProvider(merchantAccount.PrimaryProviderCode);
        var relationship = await GetPrimaryRelationshipAsync(merchantAccount, cancellationToken)
            ?? throw new InvalidOperationException("Provider relationship was not found.");
        if (string.IsNullOrWhiteSpace(relationship.ProviderReference))
        {
            throw new InvalidOperationException("Provider application reference is required before refreshing status.");
        }

        try
        {
            var providerResult = await provider.GetMerchantApplicationStatusAsync(
                relationship.ProviderReference,
                cancellationToken);
            relationship.ProviderStatusRefreshedAtUtc = dateTimeProvider.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return await ApplyProviderMerchantResultAsync(
                merchantAccount,
                relationship,
                provider,
                providerResult,
                actorUserId,
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException)
        {
            relationship.LastProviderError = SafeProviderError(ex, "NMI status refresh failed.");
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task<MerchantAccountResult> ApplyProviderMerchantResultAsync(
        MerchantAccount merchantAccount,
        MerchantProviderRelationship relationship,
        IPartnerProvider provider,
        PartnerMerchantCreateResult providerResult,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(providerResult.ProviderMerchantId))
        {
            relationship.ProviderMerchantId = providerResult.ProviderMerchantId;
        }
        relationship.ProviderReference = providerResult.ProviderReference ?? relationship.ProviderReference ?? providerResult.ProviderMerchantId;
        relationship.GatewayUsername = providerResult.GatewayUsername;
        if (!string.IsNullOrWhiteSpace(providerResult.GatewayPassword))
        {
            relationship.GatewayPasswordProtected = secretProtector.Protect(providerResult.GatewayPassword);
        }
        relationship.CapabilitiesJson = SetProviderCapabilities(
            relationship.CapabilitiesJson,
            providerResult.Status,
            relationship.ProviderReference,
            Clean(providerResult.ProviderMerchantId));
        relationship.LastProviderError = null;
        relationship.SupportNotes = null;
        if (!string.IsNullOrWhiteSpace(providerResult.ProviderReference))
        {
            relationship.ProviderApplicationCreatedAtUtc ??= dateTimeProvider.UtcNow;
        }
        if (!string.IsNullOrWhiteSpace(providerResult.LegalConsentUrl))
        {
            relationship.LegalConsentUrlProtected = secretProtector.Protect(
                HttpsUrl(providerResult.LegalConsentUrl, "NMI legal consent URL"));
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        if (string.Equals(providerResult.Status, MerchantAccountStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            merchantAccount.PaymentsEnabled = false;
            await TransitionAsync(
                merchantAccount,
                relationship,
                actorUserId,
                MerchantAccountStatuses.Rejected,
                "NMI Sign-Up application rejected.",
                cancellationToken);

            return ToResult(merchantAccount, relationship);
        }

        if (string.Equals(providerResult.Status, MerchantAccountStatuses.LegalConsentRequired, StringComparison.OrdinalIgnoreCase))
        {
            await TransitionAsync(
                merchantAccount,
                relationship,
                actorUserId,
                MerchantAccountStatuses.LegalConsentRequired,
                "NMI application created. Company authorized signer legal consent is required before submission.",
                cancellationToken);

            return ToResult(merchantAccount, relationship);
        }

        if (!string.Equals(providerResult.Status, MerchantAccountStatuses.Active, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(providerResult.ProviderMerchantId))
        {
            await TransitionAsync(
                merchantAccount,
                relationship,
                actorUserId,
                MerchantAccountStatuses.UnderReview,
                "NMI application submitted and under review.",
                cancellationToken);

            return ToResult(merchantAccount, relationship);
        }

        relationship.ProviderApprovedAtUtc ??= dateTimeProvider.UtcNow;
        relationship.PaymentCredentialIdempotencyKey ??= StableIdempotencyKey("nmi-payment-credential", merchantAccount.Id);
        relationship.TokenizationCredentialIdempotencyKey ??= StableIdempotencyKey("nmi-tokenization-credential", merchantAccount.Id);
        if (relationship.CredentialProvisioningStatus == "ReconciliationRequired")
        {
            throw new InvalidOperationException("NMI credential provisioning requires support reconciliation before retry.");
        }
        if (relationship.CredentialProvisioningStatus == "Provisioning")
        {
            await RequireCredentialReconciliationAsync(relationship, cancellationToken);
            throw new InvalidOperationException(relationship.LastProviderError);
        }

        relationship.CredentialProvisioningStatus = "Provisioning";
        merchantAccount.PaymentsEnabled = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        await TransitionAsync(
            merchantAccount,
            relationship,
            actorUserId,
            MerchantAccountStatuses.CredentialProvisioning,
            "NMI approved the merchant. Secure payment credentials are being provisioned.",
            cancellationToken);

        try
        {
            if (string.IsNullOrWhiteSpace(relationship.PaymentApiKeyProtected))
            {
                var paymentKey = await provider.CreateMerchantCredentialAsync(
                    new PartnerMerchantCredentialRequest(
                        providerResult.ProviderMerchantId,
                        "iM1 Payments transaction key",
                        new[] { "transaction" }),
                    relationship.PaymentCredentialIdempotencyKey,
                    cancellationToken);
                var privateKey = paymentKey.PrivateKey ?? paymentKey.PublicKey
                    ?? throw new InvalidOperationException("NMI payment credential response did not include a key.");
                relationship.PaymentApiKeyProtected = secretProtector.Protect(privateKey);
                relationship.CredentialMetadataJson = SafeCredentialMetadata(
                    relationship.CredentialMetadataJson,
                    "PaymentKeyId",
                    paymentKey.KeyId,
                    paymentKey.Description);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(relationship.PublicTokenizationKeyProtected))
            {
                var tokenizationKey = await provider.CreateMerchantCredentialAsync(
                    new PartnerMerchantCredentialRequest(
                        providerResult.ProviderMerchantId,
                        "iM1 Payments Collect.js tokenization key",
                        new[] { "tokenization" }),
                    relationship.TokenizationCredentialIdempotencyKey,
                    cancellationToken);
                var publicKey = tokenizationKey.PublicKey ?? tokenizationKey.PrivateKey
                    ?? throw new InvalidOperationException("NMI tokenization credential response did not include a key.");
                relationship.PublicTokenizationKeyProtected = secretProtector.Protect(publicKey);
                relationship.CredentialMetadataJson = SafeCredentialMetadata(
                    relationship.CredentialMetadataJson,
                    "TokenizationKeyId",
                    tokenizationKey.KeyId,
                    tokenizationKey.Description);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (
            ex is TaskCanceledException or JsonException or InvalidOperationException or DbUpdateException ||
            ex is HttpRequestException httpException &&
            (httpException.StatusCode is null || (int)httpException.StatusCode >= 500))
        {
            await RequireCredentialReconciliationAsync(relationship, CancellationToken.None);
            throw new InvalidOperationException(relationship.LastProviderError, ex);
        }
        catch (HttpRequestException)
        {
            relationship.CredentialProvisioningStatus = "Failed";
            relationship.LastProviderError = "NMI credential provisioning failed before activation.";
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        relationship.LastProviderError = null;
        relationship.SupportNotes = null;
        relationship.CredentialProvisioningStatus = "Complete";
        relationship.CredentialsProvisionedAtUtc ??= dateTimeProvider.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await TransitionAsync(
            merchantAccount,
            relationship,
            actorUserId,
            MerchantAccountStatuses.Active,
            "NMI merchant approved, gateway credentials configured, and payments enabled.",
            cancellationToken);

        return ToResult(merchantAccount, relationship);
    }

    private async Task RequireCredentialReconciliationAsync(
        MerchantProviderRelationship relationship,
        CancellationToken cancellationToken)
    {
        relationship.CredentialProvisioningStatus = "ReconciliationRequired";
        relationship.LastProviderError =
            "NMI credential provisioning had an ambiguous result and requires support reconciliation before retry.";
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<MerchantAccountResult> RejectApplicationAsync(
        Guid organizationId,
        Guid merchantAccountId,
        Guid actorUserId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var merchantAccount = await dbContext.MerchantAccounts.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == merchantAccountId, cancellationToken)
            ?? throw new InvalidOperationException("Merchant application was not found.");
        if (merchantAccount.Status != MerchantAccountStatuses.Submitted)
        {
            throw new InvalidOperationException("Only a submitted application awaiting Platform review can be rejected.");
        }
        var relationship = await GetPrimaryRelationshipAsync(merchantAccount, cancellationToken);
        merchantAccount.PaymentsEnabled = false;
        await TransitionAsync(
            merchantAccount,
            relationship,
            actorUserId,
            MerchantAccountStatuses.Rejected,
            reason ?? "Merchant application rejected.",
            cancellationToken);

        return ToResult(merchantAccount, relationship);
    }

    public async Task<MerchantAccountResult> ChangeStatusAsync(
        Guid organizationId,
        Guid merchantAccountId,
        Guid actorUserId,
        string newStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = NormalizeStatus(newStatus);
        var merchantAccount = await dbContext.MerchantAccounts.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == merchantAccountId, cancellationToken)
            ?? throw new InvalidOperationException("Merchant account was not found.");
        var relationship = await GetPrimaryRelationshipAsync(merchantAccount, cancellationToken);
        if (normalizedStatus == MerchantAccountStatuses.Active &&
            (relationship is null ||
             string.IsNullOrWhiteSpace(relationship.ProviderMerchantId) ||
             string.IsNullOrWhiteSpace(relationship.PaymentApiKeyProtected) ||
             string.IsNullOrWhiteSpace(relationship.PublicTokenizationKeyProtected) ||
             !string.Equals(relationship.CredentialProvisioningStatus, "Complete", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A fully provisioned provider merchant relationship is required before activation.");
        }

        await TransitionAsync(
            merchantAccount,
            relationship,
            actorUserId,
            normalizedStatus,
            reason,
            cancellationToken);

        return ToResult(merchantAccount, relationship);
    }

    private async Task TransitionAsync(
        MerchantAccount merchantAccount,
        MerchantProviderRelationship? relationship,
        Guid actorUserId,
        string newStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = NormalizeStatus(newStatus);
        var oldStatus = merchantAccount.Status;
        if (string.Equals(oldStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase))
        {
            merchantAccount.UpdatedAtUtc = dateTimeProvider.UtcNow;
            merchantAccount.UpdatedByUserId = actorUserId.ToString();
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        merchantAccount.Status = normalizedStatus;
        merchantAccount.UnderwritingStatus = UnderwritingStatusFor(normalizedStatus);
        if (normalizedStatus == MerchantAccountStatuses.Submitted)
        {
            merchantAccount.SubmittedAtUtc ??= dateTimeProvider.UtcNow;
        }
        if (normalizedStatus == MerchantAccountStatuses.Approved)
        {
            merchantAccount.ApprovedAtUtc ??= dateTimeProvider.UtcNow;
        }
        if (normalizedStatus == MerchantAccountStatuses.Active)
        {
            merchantAccount.ActivatedAtUtc ??= dateTimeProvider.UtcNow;
            merchantAccount.PaymentsEnabled = true;
        }
        if (normalizedStatus == MerchantAccountStatuses.Rejected)
        {
            merchantAccount.RejectedAtUtc ??= dateTimeProvider.UtcNow;
            merchantAccount.PaymentsEnabled = false;
        }
        merchantAccount.UpdatedAtUtc = dateTimeProvider.UtcNow;
        merchantAccount.UpdatedByUserId = actorUserId.ToString();

        if (relationship is not null)
        {
            relationship.Status = normalizedStatus;
            relationship.UpdatedAtUtc = dateTimeProvider.UtcNow;
            relationship.UpdatedByUserId = actorUserId.ToString();
        }

        AddStatusHistory(
            merchantAccount,
            oldStatus,
            normalizedStatus,
            reason,
            relationship?.ProviderCode ?? merchantAccount.PrimaryProviderCode,
            relationship?.ProviderReference ?? relationship?.ProviderMerchantId,
            actorUserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordStatusEventAsync(merchantAccount, relationship, oldStatus, normalizedStatus, reason, cancellationToken);
    }

    private async Task<IReadOnlyCollection<PlatformMerchantAccountRow>> GetPlatformRowsAsync(
        IReadOnlySet<string> statuses,
        CancellationToken cancellationToken)
    {
        var accounts = await dbContext.MerchantAccounts.IgnoreQueryFilters()
            .Where(x => statuses.Contains(x.Status))
            .OrderByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var accountIds = accounts.Select(x => x.Id).ToArray();
        var organizationIds = accounts.Select(x => x.OrganizationId).Distinct().ToArray();
        var relationships = await dbContext.MerchantProviderRelationships.IgnoreQueryFilters()
            .Where(x => accountIds.Contains(x.MerchantAccountId))
            .ToListAsync(cancellationToken);
        var histories = await dbContext.MerchantAccountStatusHistories.IgnoreQueryFilters()
            .Where(x => accountIds.Contains(x.MerchantAccountId))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var organizations = await dbContext.Organizations.IgnoreQueryFilters()
            .Where(x => organizationIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return accounts
            .Select(account =>
            {
                var relationship = relationships.FirstOrDefault(x =>
                    x.MerchantAccountId == account.Id &&
                    string.Equals(x.ProviderCode, account.PrimaryProviderCode, StringComparison.OrdinalIgnoreCase)) ??
                    relationships.FirstOrDefault(x => x.MerchantAccountId == account.Id);
                return ToPlatformRow(
                    account,
                    relationship,
                    organizations.TryGetValue(account.OrganizationId, out var name) ? name : null,
                    histories.Where(x => x.MerchantAccountId == account.Id).Select(ToHistoryRow).ToArray());
            })
            .ToArray();
    }

    private async Task<MerchantAccount> GetOrCreateMerchantAccountAsync(
        Guid organizationId,
        Guid actorUserId,
        string providerCode,
        string status,
        CancellationToken cancellationToken)
    {
        var merchantAccount = await dbContext.MerchantAccounts.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
        if (merchantAccount is not null)
        {
            return merchantAccount;
        }

        merchantAccount = new MerchantAccount
        {
            OrganizationId = organizationId,
            Status = status,
            UnderwritingStatus = UnderwritingStatusFor(status),
            PrimaryProviderCode = providerCode,
            CreatedByUserId = actorUserId.ToString()
        };
        dbContext.MerchantAccounts.Add(merchantAccount);
        await dbContext.SaveChangesAsync(cancellationToken);
        AddStatusHistory(
            merchantAccount,
            null,
            status,
            "Merchant application created.",
            providerCode,
            null,
            actorUserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return merchantAccount;
    }

    private async Task<MerchantProviderRelationship> GetOrCreateRelationshipAsync(
        MerchantAccount merchantAccount,
        string providerCode,
        CancellationToken cancellationToken)
    {
        var relationship = await dbContext.MerchantProviderRelationships.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                x => x.OrganizationId == merchantAccount.OrganizationId && x.ProviderCode == providerCode,
                cancellationToken);
        if (relationship is not null)
        {
            return relationship;
        }

        relationship = new MerchantProviderRelationship
        {
            OrganizationId = merchantAccount.OrganizationId,
            MerchantAccountId = merchantAccount.Id,
            ProviderCode = providerCode,
            ProviderMerchantId = string.Empty,
            Status = merchantAccount.Status
        };
        dbContext.MerchantProviderRelationships.Add(relationship);
        await dbContext.SaveChangesAsync(cancellationToken);
        return relationship;
    }

    private async Task<MerchantProviderRelationship?> GetPrimaryRelationshipAsync(
        MerchantAccount merchantAccount,
        CancellationToken cancellationToken)
    {
        var relationships = dbContext.MerchantProviderRelationships.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == merchantAccount.OrganizationId && x.MerchantAccountId == merchantAccount.Id);
        return !string.IsNullOrWhiteSpace(merchantAccount.PrimaryProviderCode)
            ? await relationships.SingleOrDefaultAsync(x => x.ProviderCode == merchantAccount.PrimaryProviderCode, cancellationToken)
            : await relationships.FirstOrDefaultAsync(cancellationToken);
    }

    private void ApplyApplication(MerchantAccount merchantAccount, MerchantApplicationRequest request, string providerCode)
    {
        merchantAccount.LegalBusinessName = Clean(request.BusinessName);
        merchantAccount.Dba = Clean(request.Dba);
        var taxIdentifier = NewSecretValue(request.TaxId) ?? NewSecretValue(request.Ein);
        if (taxIdentifier is not null)
        {
            merchantAccount.Ein = Mask(taxIdentifier);
            merchantAccount.TaxIdentifierLastFour = LastFour(taxIdentifier);
            merchantAccount.TaxIdentifierProtected = secretProtector.Protect(taxIdentifier);
        }
        merchantAccount.BusinessType = Clean(request.BusinessType);
        merchantAccount.BusinessDescription = Clean(request.BusinessDescription);
        merchantAccount.YearsInBusiness = request.YearsInBusiness;
        merchantAccount.PhysicalAddressLine1 = Clean(request.PhysicalAddressLine1);
        merchantAccount.PhysicalAddressLine2 = Clean(request.PhysicalAddressLine2);
        merchantAccount.PhysicalCity = Clean(request.PhysicalCity);
        merchantAccount.PhysicalRegion = Clean(request.PhysicalRegion);
        merchantAccount.PhysicalPostalCode = Clean(request.PhysicalPostalCode);
        merchantAccount.PhysicalCountry = Clean(request.PhysicalCountry) ?? "US";
        merchantAccount.MailingAddressLine1 = Clean(request.MailingAddressLine1);
        merchantAccount.MailingAddressLine2 = Clean(request.MailingAddressLine2);
        merchantAccount.MailingCity = Clean(request.MailingCity);
        merchantAccount.MailingRegion = Clean(request.MailingRegion);
        merchantAccount.MailingPostalCode = Clean(request.MailingPostalCode);
        merchantAccount.MailingCountry = Clean(request.MailingCountry);
        merchantAccount.OwnerName = Clean(request.OwnerName);
        merchantAccount.OwnerEmail = Clean(request.OwnerEmail);
        merchantAccount.OwnerPhone = Clean(request.OwnerPhone);
        merchantAccount.OwnerTitle = Clean(request.OwnerTitle);
        merchantAccount.OwnerOwnershipPercentage = request.OwnerOwnershipPercentage;
        var ownerDateOfBirth = NewSecretValue(request.OwnerDateOfBirth);
        if (ownerDateOfBirth is not null)
        {
            merchantAccount.OwnerDateOfBirthProtected = secretProtector.Protect(ownerDateOfBirth);
        }
        var ownerSsn = NewSecretValue(request.OwnerSsn);
        if (ownerSsn is not null)
        {
            merchantAccount.OwnerSsnLastFour = LastFour(ownerSsn);
            merchantAccount.OwnerSsnProtected = secretProtector.Protect(ownerSsn);
        }
        merchantAccount.BankName = Clean(request.BankName);
        var routingNumber = NewSecretValue(request.BankRoutingNumber);
        if (routingNumber is not null)
        {
            merchantAccount.BankRoutingLastFour = LastFour(routingNumber);
            merchantAccount.BankRoutingNumberProtected = secretProtector.Protect(routingNumber);
        }
        var accountNumber = NewSecretValue(request.BankAccountNumber);
        if (accountNumber is not null)
        {
            merchantAccount.BankAccountLastFour = LastFour(accountNumber);
            merchantAccount.BankAccountNumberProtected = secretProtector.Protect(accountNumber);
        }
        merchantAccount.ExpectedMonthlyVolume = request.ExpectedMonthlyVolume;
        merchantAccount.AverageTicket = request.AverageTicket;
        merchantAccount.HighTicket = request.HighTicket;
        merchantAccount.CardPresentPercentage = request.CardPresentPercentage;
        merchantAccount.KeyEnteredPercentage = request.KeyEnteredPercentage;
        merchantAccount.EcommercePercentage = request.EcommercePercentage;
        merchantAccount.MotoPercentage = request.MotoPercentage;
        merchantAccount.Website = Clean(request.Website);
        merchantAccount.Mcc = Clean(request.Mcc);
        merchantAccount.PrimaryProviderCode = providerCode;
    }

    private static void ValidateApplication(MerchantAccount merchantAccount)
    {
        Required(merchantAccount.LegalBusinessName, "Business name");
        Required(merchantAccount.BusinessType, "Business type");
        Required(merchantAccount.BusinessDescription, "Business description");
        if (merchantAccount.YearsInBusiness is null or < 0)
        {
            throw new InvalidOperationException("Years in business is required.");
        }
        Required(merchantAccount.PhysicalAddressLine1, "Physical address");
        Required(merchantAccount.PhysicalCity, "City");
        Required(merchantAccount.PhysicalRegion, "State/region");
        Required(merchantAccount.PhysicalPostalCode, "Postal code");
        Required(merchantAccount.OwnerName, "Owner name");
        Required(merchantAccount.OwnerEmail, "Owner email");
        Required(merchantAccount.OwnerPhone, "Owner phone");
        Required(merchantAccount.OwnerTitle, "Owner title");
        Required(merchantAccount.TaxIdentifierProtected, "Tax identifier");
        Required(merchantAccount.OwnerDateOfBirthProtected, "Owner date of birth");
        Required(merchantAccount.OwnerSsnProtected, "Owner SSN");
        Required(merchantAccount.BankRoutingNumberProtected, "Bank routing number");
        Required(merchantAccount.BankAccountNumberProtected, "Bank account number");
        if (merchantAccount.OwnerOwnershipPercentage is null or <= 0 or > 100)
        {
            throw new InvalidOperationException("Owner ownership percentage must be between 0 and 100.");
        }
        if (merchantAccount.ExpectedMonthlyVolume is null or <= 0 ||
            merchantAccount.AverageTicket is null or <= 0 ||
            merchantAccount.HighTicket is null or <= 0)
        {
            throw new InvalidOperationException("Expected monthly volume, average ticket, and high ticket are required.");
        }
        var processingMix = new[]
        {
            merchantAccount.CardPresentPercentage,
            merchantAccount.KeyEnteredPercentage,
            merchantAccount.EcommercePercentage,
            merchantAccount.MotoPercentage
        };
        if (processingMix.Any(x => x is null or < 0) || processingMix.Sum(x => x ?? 0) != 100m)
        {
            throw new InvalidOperationException("Card-present, keyed, ecommerce, and MOTO percentages must total 100.");
        }
    }

    private static void EnsureApplicationEditable(MerchantAccount merchantAccount)
    {
        if (!CanEditApplication(merchantAccount.Status))
        {
            throw new InvalidOperationException($"Merchant applications in {merchantAccount.Status} status cannot be edited.");
        }
    }

    private static void EnsureApplicationSubmittable(MerchantAccount merchantAccount)
    {
        if (!CanSubmitApplication(merchantAccount.Status))
        {
            throw new InvalidOperationException($"Merchant applications in {merchantAccount.Status} status cannot be submitted.");
        }
    }

    private static bool CanEditApplication(string status)
    {
        return status is MerchantAccountStatuses.Draft
            or MerchantAccountStatuses.Rejected
            or MerchantAccountStatuses.Submitted
            or MerchantAccountStatuses.Approved;
    }

    private static bool CanSubmitApplication(string status)
    {
        return status is MerchantAccountStatuses.Draft
            or MerchantAccountStatuses.Rejected
            or MerchantAccountStatuses.Submitted;
    }

    private async Task<IReadOnlyCollection<MerchantStatusHistoryRow>> GetHistoryRowsAsync(
        Guid merchantAccountId,
        CancellationToken cancellationToken)
    {
        var history = await dbContext.MerchantAccountStatusHistories.IgnoreQueryFilters()
            .Where(x => x.MerchantAccountId == merchantAccountId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return history.Select(ToHistoryRow).ToArray();
    }

    private void AddStatusHistory(
        MerchantAccount merchantAccount,
        string? oldStatus,
        string newStatus,
        string? reason,
        string? providerCode,
        string? providerReference,
        Guid actorUserId)
    {
        dbContext.MerchantAccountStatusHistories.Add(new MerchantAccountStatusHistory
        {
            OrganizationId = merchantAccount.OrganizationId,
            MerchantAccountId = merchantAccount.Id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Reason = Clean(reason),
            ProviderCode = Clean(providerCode),
            ProviderReference = Clean(providerReference),
            CreatedByUserId = actorUserId.ToString(),
            CreatedAtUtc = dateTimeProvider.UtcNow
        });
    }

    private async Task RecordStatusEventAsync(
        MerchantAccount merchantAccount,
        MerchantProviderRelationship? relationship,
        string? oldStatus,
        string newStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        var eventType = EventTypeFor(newStatus);
        if (eventType is null)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            merchantAccount.Id,
            merchantAccount.OrganizationId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Reason = reason,
            relationship?.ProviderCode,
            relationship?.ProviderMerchantId,
            relationship?.ProviderReference
        });
        await domainEventRecorder.RecordAsync(
            new DomainEventRecordRequest(
                merchantAccount.OrganizationId,
                null,
                nameof(MerchantAccount),
                merchantAccount.Id.ToString(),
                eventType,
                payload,
                Guid.NewGuid().ToString("N"),
                "FinancialServices"),
            cancellationToken);
    }

    private IPartnerProvider ResolveProvider(string? providerCode)
    {
        var normalizedProviderCode = string.IsNullOrWhiteSpace(providerCode) ? "NMI" : providerCode.Trim();
        return partnerProviders.SingleOrDefault(x => string.Equals(x.ProviderCode, normalizedProviderCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Financial provider '{normalizedProviderCode}' is not registered.");
    }

    private PartnerMerchantCreateRequest ToProviderRequest(MerchantAccount merchantAccount)
    {
        var ownerNames = (merchantAccount.OwnerName ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var firstName = ownerNames.FirstOrDefault() ?? "Merchant";
        var lastName = ownerNames.Length > 1 ? string.Join(" ", ownerNames.Skip(1)) : "Owner";
        return new PartnerMerchantCreateRequest(
            merchantAccount.OrganizationId,
            Required(merchantAccount.LegalBusinessName, "Business name"),
            Required(merchantAccount.PhysicalCountry, "Country"),
            Required(merchantAccount.PhysicalAddressLine1, "Physical address"),
            Clean(merchantAccount.PhysicalAddressLine2),
            Required(merchantAccount.PhysicalCity, "City"),
            Required(merchantAccount.PhysicalRegion, "State/region"),
            Required(merchantAccount.PhysicalPostalCode, "Postal code"),
            "America/Chicago",
            firstName,
            lastName,
            Required(merchantAccount.OwnerEmail, "Owner email"),
            Required(merchantAccount.OwnerPhone, "Owner phone"),
            Clean(merchantAccount.Website),
            merchantAccount.Id.ToString(),
            Clean(merchantAccount.Dba),
            UnprotectRequired(merchantAccount.TaxIdentifierProtected, "Tax identifier"),
            Clean(merchantAccount.BusinessType),
            Clean(merchantAccount.BusinessDescription),
            merchantAccount.YearsInBusiness,
            Clean(merchantAccount.OwnerTitle),
            merchantAccount.OwnerOwnershipPercentage,
            UnprotectRequired(merchantAccount.OwnerDateOfBirthProtected, "Owner date of birth"),
            UnprotectRequired(merchantAccount.OwnerSsnProtected, "Owner SSN"),
            Clean(merchantAccount.BankName),
            UnprotectRequired(merchantAccount.BankRoutingNumberProtected, "Bank routing number"),
            UnprotectRequired(merchantAccount.BankAccountNumberProtected, "Bank account number"),
            merchantAccount.ExpectedMonthlyVolume,
            merchantAccount.AverageTicket,
            merchantAccount.HighTicket,
            merchantAccount.CardPresentPercentage,
            merchantAccount.KeyEnteredPercentage,
            merchantAccount.EcommercePercentage,
            merchantAccount.MotoPercentage,
            Clean(merchantAccount.Mcc));
    }

    private static MerchantApplicationFormModel EmptyApplication(Guid organizationId)
    {
        return new MerchantApplicationFormModel(
            OrganizationId: organizationId,
            MerchantAccountId: null,
            Status: MerchantAccountStatuses.NotStarted,
            BusinessName: string.Empty,
            Dba: null,
            Ein: null,
            TaxId: null,
            BusinessType: null,
            BusinessDescription: null,
            YearsInBusiness: null,
            PhysicalAddressLine1: string.Empty,
            PhysicalAddressLine2: null,
            PhysicalCity: string.Empty,
            PhysicalRegion: string.Empty,
            PhysicalPostalCode: string.Empty,
            PhysicalCountry: "US",
            MailingAddressLine1: null,
            MailingAddressLine2: null,
            MailingCity: null,
            MailingRegion: null,
            MailingPostalCode: null,
            MailingCountry: "US",
            OwnerName: string.Empty,
            OwnerEmail: string.Empty,
            OwnerPhone: string.Empty,
            OwnerTitle: null,
            OwnerOwnershipPercentage: null,
            OwnerDateOfBirthMasked: null,
            OwnerSsnLastFour: null,
            BankName: null,
            BankRoutingLastFour: null,
            BankAccountLastFour: null,
            ExpectedMonthlyVolume: null,
            AverageTicket: null,
            HighTicket: null,
            CardPresentPercentage: null,
            KeyEnteredPercentage: null,
            EcommercePercentage: null,
            MotoPercentage: null,
            Website: null,
            Mcc: null,
            CanEdit: true,
            CanSubmit: true);
    }

    private static MerchantApplicationFormModel ToApplicationForm(MerchantAccount merchantAccount)
    {
        var canEdit = CanEditApplication(merchantAccount.Status);
        var canSubmit = CanSubmitApplication(merchantAccount.Status);
        return new MerchantApplicationFormModel(
            OrganizationId: merchantAccount.OrganizationId,
            MerchantAccountId: merchantAccount.Id,
            Status: merchantAccount.Status,
            BusinessName: merchantAccount.LegalBusinessName ?? string.Empty,
            Dba: merchantAccount.Dba,
            Ein: merchantAccount.Ein,
            TaxId: MaskLastFour(merchantAccount.TaxIdentifierLastFour),
            BusinessType: merchantAccount.BusinessType,
            BusinessDescription: merchantAccount.BusinessDescription,
            YearsInBusiness: merchantAccount.YearsInBusiness,
            PhysicalAddressLine1: merchantAccount.PhysicalAddressLine1 ?? string.Empty,
            PhysicalAddressLine2: merchantAccount.PhysicalAddressLine2,
            PhysicalCity: merchantAccount.PhysicalCity ?? string.Empty,
            PhysicalRegion: merchantAccount.PhysicalRegion ?? string.Empty,
            PhysicalPostalCode: merchantAccount.PhysicalPostalCode ?? string.Empty,
            PhysicalCountry: merchantAccount.PhysicalCountry ?? "US",
            MailingAddressLine1: merchantAccount.MailingAddressLine1,
            MailingAddressLine2: merchantAccount.MailingAddressLine2,
            MailingCity: merchantAccount.MailingCity,
            MailingRegion: merchantAccount.MailingRegion,
            MailingPostalCode: merchantAccount.MailingPostalCode,
            MailingCountry: merchantAccount.MailingCountry,
            OwnerName: merchantAccount.OwnerName ?? string.Empty,
            OwnerEmail: merchantAccount.OwnerEmail ?? string.Empty,
            OwnerPhone: merchantAccount.OwnerPhone ?? string.Empty,
            OwnerTitle: merchantAccount.OwnerTitle,
            OwnerOwnershipPercentage: merchantAccount.OwnerOwnershipPercentage,
            OwnerDateOfBirthMasked: string.IsNullOrWhiteSpace(merchantAccount.OwnerDateOfBirthProtected) ? null : "Configured",
            OwnerSsnLastFour: MaskLastFour(merchantAccount.OwnerSsnLastFour),
            BankName: merchantAccount.BankName,
            BankRoutingLastFour: MaskLastFour(merchantAccount.BankRoutingLastFour),
            BankAccountLastFour: MaskLastFour(merchantAccount.BankAccountLastFour),
            ExpectedMonthlyVolume: merchantAccount.ExpectedMonthlyVolume,
            AverageTicket: merchantAccount.AverageTicket,
            HighTicket: merchantAccount.HighTicket,
            CardPresentPercentage: merchantAccount.CardPresentPercentage,
            KeyEnteredPercentage: merchantAccount.KeyEnteredPercentage,
            EcommercePercentage: merchantAccount.EcommercePercentage,
            MotoPercentage: merchantAccount.MotoPercentage,
            Website: merchantAccount.Website,
            Mcc: merchantAccount.Mcc,
            CanEdit: canEdit,
            CanSubmit: canSubmit);
    }

    private static MerchantAccountSummary ToSummary(MerchantAccount account, MerchantProviderRelationship? relationship)
    {
        var hasProviderMerchant = relationship is not null && !string.IsNullOrWhiteSpace(relationship.ProviderMerchantId);
        var isReady =
            string.Equals(account.Status, MerchantAccountStatuses.Active, StringComparison.OrdinalIgnoreCase) &&
            relationship is not null &&
            string.Equals(relationship.Status, MerchantAccountStatuses.Active, StringComparison.OrdinalIgnoreCase) &&
            hasProviderMerchant &&
            !string.IsNullOrWhiteSpace(relationship.PaymentApiKeyProtected) &&
            !string.IsNullOrWhiteSpace(relationship.PublicTokenizationKeyProtected) &&
            string.Equals(relationship.CredentialProvisioningStatus, "Complete", StringComparison.OrdinalIgnoreCase);
        return new MerchantAccountSummary(
            account.Id,
            account.OrganizationId,
            account.Status,
            account.UnderwritingStatus,
            account.LegalBusinessName,
            relationship?.ProviderCode ?? account.PrimaryProviderCode,
            ProviderDisplayName(relationship?.ProviderCode ?? account.PrimaryProviderCode),
            relationship?.Status,
            account.PaymentsEnabled,
            hasProviderMerchant,
            isReady && account.PaymentsEnabled);
    }

    private MerchantLegalConsentModel ToLegalConsent(
        MerchantAccount account,
        MerchantProviderRelationship? relationship)
    {
        var isRequired = account.Status == MerchantAccountStatuses.LegalConsentRequired;
        var legalConsentUrl = isRequired && !string.IsNullOrWhiteSpace(relationship?.LegalConsentUrlProtected)
            ? secretProtector.Unprotect(relationship.LegalConsentUrlProtected)
            : null;
        return new MerchantLegalConsentModel(
            isRequired,
            legalConsentUrl,
            relationship?.LegalConsentCompletedAtUtc,
            isRequired && !string.IsNullOrWhiteSpace(legalConsentUrl));
    }

    private static PlatformMerchantAccountRow ToPlatformRow(
        MerchantAccount account,
        MerchantProviderRelationship? relationship,
        string? companyName,
        IReadOnlyCollection<MerchantStatusHistoryRow> history)
    {
        return new PlatformMerchantAccountRow(
            CompanyId: account.OrganizationId,
            MerchantAccountId: account.Id,
            CompanyName: companyName,
            Status: account.Status,
            UnderwritingStatus: account.UnderwritingStatus,
            LegalBusinessName: account.LegalBusinessName,
            OwnerName: account.OwnerName,
            OwnerEmail: account.OwnerEmail,
            ExpectedMonthlyVolume: account.ExpectedMonthlyVolume,
            ProviderCode: relationship?.ProviderCode ?? account.PrimaryProviderCode,
            ProviderMerchantId: relationship?.ProviderMerchantId,
            GatewayUsername: relationship?.GatewayUsername,
            HasPaymentApiKey: !string.IsNullOrWhiteSpace(relationship?.PaymentApiKeyProtected),
            HasPublicTokenizationKey: !string.IsNullOrWhiteSpace(relationship?.PublicTokenizationKeyProtected),
            ProviderReference: relationship?.ProviderReference,
            ProviderRelationshipStatus: relationship?.Status,
            CredentialProvisioningStatus: relationship?.CredentialProvisioningStatus,
            LastProviderError: relationship?.LastProviderError,
            SupportNotes: relationship?.SupportNotes,
            ProviderApplicationCreatedAtUtc: relationship?.ProviderApplicationCreatedAtUtc,
            LegalConsentCompletedAtUtc: relationship?.LegalConsentCompletedAtUtc,
            ProviderApplicationSubmittedAtUtc: relationship?.ProviderApplicationSubmittedAtUtc,
            ProviderStatusRefreshedAtUtc: relationship?.ProviderStatusRefreshedAtUtc,
            ProviderApprovedAtUtc: relationship?.ProviderApprovedAtUtc,
            CredentialsProvisionedAtUtc: relationship?.CredentialsProvisionedAtUtc,
            CreatedAtUtc: account.CreatedAtUtc,
            UpdatedAtUtc: account.UpdatedAtUtc,
            StatusHistory: history);
    }

    private static MerchantStatusHistoryRow ToHistoryRow(MerchantAccountStatusHistory history)
    {
        return new MerchantStatusHistoryRow(
            history.OrganizationId,
            history.MerchantAccountId,
            history.OldStatus,
            history.NewStatus,
            history.Reason,
            history.ProviderCode,
            history.ProviderReference,
            history.CreatedByUserId,
            history.CreatedAtUtc);
    }

    private static IReadOnlyCollection<string> RequiredNextSteps(MerchantAccountSummary summary)
    {
        return summary.Status switch
        {
            MerchantAccountStatuses.Draft => new[] { "Complete and submit the merchant application." },
            MerchantAccountStatuses.Submitted => new[] { "iM1 Financial Services is preparing the application for review." },
            MerchantAccountStatuses.Approved => new[] { "Platform approval is complete. The NMI application is being created." },
            MerchantAccountStatuses.LegalConsentRequired => new[] { "An authorized company signer must complete NMI legal consent before submission." },
            MerchantAccountStatuses.UnderReview => new[] { "The merchant application has been submitted to NMI for review." },
            MerchantAccountStatuses.CredentialProvisioning => new[] { "NMI approved the merchant. Secure payment credentials are being provisioned." },
            MerchantAccountStatuses.Active when !summary.IsProcessingReady => new[] { "Contact support to repair the provider merchant relationship." },
            MerchantAccountStatuses.Active => Array.Empty<string>(),
            MerchantAccountStatuses.Rejected => new[] { "Contact iM1 support for rejection details or resubmission options." },
            MerchantAccountStatuses.Suspended => new[] { "Contact iM1 support to resolve the suspension." },
            MerchantAccountStatuses.Closed => new[] { "This merchant account is closed." },
            _ => new[] { "Contact iM1 support for merchant account status." }
        };
    }

    private static string NormalizeStatus(string status)
    {
        var normalized = Required(status, "Merchant account status");
        return MerchantAccountStatuses.All.FirstOrDefault(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Merchant account status '{status}' is not supported.");
    }

    private static string UnderwritingStatusFor(string status)
    {
        return status switch
        {
            MerchantAccountStatuses.Draft => "NotSubmitted",
            MerchantAccountStatuses.Submitted => "Submitted",
            MerchantAccountStatuses.UnderReview => "UnderReview",
            MerchantAccountStatuses.Approved => "Approved",
            MerchantAccountStatuses.LegalConsentRequired => "LegalConsentRequired",
            MerchantAccountStatuses.CredentialProvisioning => "Approved",
            MerchantAccountStatuses.Active => "Approved",
            MerchantAccountStatuses.Rejected => "Rejected",
            MerchantAccountStatuses.Suspended => "Suspended",
            MerchantAccountStatuses.Closed => "Closed",
            _ => status
        };
    }

    private static string? EventTypeFor(string status)
    {
        return status switch
        {
            MerchantAccountStatuses.Submitted => FinancialEventTypes.MerchantApplicationSubmitted,
            MerchantAccountStatuses.Approved => FinancialEventTypes.MerchantApproved,
            MerchantAccountStatuses.Active => FinancialEventTypes.MerchantActivated,
            MerchantAccountStatuses.Rejected => FinancialEventTypes.MerchantRejected,
            MerchantAccountStatuses.Suspended => FinancialEventTypes.MerchantSuspended,
            MerchantAccountStatuses.Closed => FinancialEventTypes.MerchantClosed,
            _ => null
        };
    }

    private static string? ProviderDisplayName(string? providerCode)
    {
        return string.Equals(providerCode, "NMI", StringComparison.OrdinalIgnoreCase) ? "NMI" : providerCode;
    }

    private static MerchantAccountResult ToResult(MerchantAccount merchantAccount, MerchantProviderRelationship? relationship)
    {
        return new MerchantAccountResult(
            merchantAccount.Id,
            merchantAccount.OrganizationId,
            merchantAccount.Status,
            merchantAccount.UnderwritingStatus,
            relationship?.ProviderCode ?? merchantAccount.PrimaryProviderCode ?? string.Empty,
            relationship?.ProviderMerchantId ?? string.Empty);
    }

    private static string Required(string? value, string label)
    {
        return Clean(value) ?? throw new InvalidOperationException($"{label} is required.");
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string HttpsUrl(string value, string label)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{label} must be an absolute HTTPS URL.");
        }

        return uri.AbsoluteUri;
    }

    private static string? LastFour(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? digits[^4..] : null;
    }

    private string UnprotectRequired(string? protectedValue, string label)
    {
        return secretProtector.Unprotect(Required(protectedValue, label));
    }

    private async Task<ApplicationCreateOperationMetadata> RotateApplicationCreateKeyAsync(
        MerchantAccount merchantAccount,
        MerchantProviderRelationship relationship,
        IPartnerProvider provider,
        PartnerMerchantCreateRequest providerRequest,
        string payloadFingerprint,
        ApplicationCreateOperationMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(relationship.ProviderReference))
        {
            throw new InvalidOperationException("NMI application key rotation is blocked because a provider reference exists.");
        }
        if (!string.Equals(metadata.LastFailureClassification, DefinitiveValidationFailure, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "NMI application key rotation is allowed only after a definitive validation failure.");
        }

        var matchingApplicationExists = await provider.HasMatchingMerchantApplicationAsync(
            providerRequest,
            cancellationToken);
        if (matchingApplicationExists)
        {
            throw new InvalidOperationException(
                "NMI application key rotation is blocked because reconciliation found a matching application.");
        }

        var nextVersion = checked(metadata.KeyVersion + 1);
        var now = dateTimeProvider.UtcNow;
        var rotated = metadata with
        {
            KeyVersion = nextVersion,
            PayloadFingerprint = payloadFingerprint,
            RotatedAtUtc = now,
            RotationReasonCode = SanitizeReasonCode(metadata.LastFailureReasonCode) ?? "VALIDATION_ERROR",
            LastFailureClassification = null,
            LastFailureReasonCode = null,
            LastFailureAtUtc = null,
            Rotations = (metadata.Rotations ?? [])
                .Append(new ApplicationCreateRotationAudit(
                    nextVersion,
                    payloadFingerprint,
                    now,
                    SanitizeReasonCode(metadata.LastFailureReasonCode) ?? "VALIDATION_ERROR"))
                .ToArray()
        };
        relationship.ApplicationCreateIdempotencyKey = StableIdempotencyKey(
            $"nmi-application-create-v{nextVersion}",
            merchantAccount.Id);
        relationship.CapabilitiesJson = SetApplicationCreateMetadata(relationship.CapabilitiesJson, rotated);
        relationship.LastProviderError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return rotated;
    }

    private static ApplicationCreateFailure ClassifyApplicationCreateFailure(
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is NmiValidationException validationException &&
            IsDefinitiveValidationFailure(validationException))
        {
            return new ApplicationCreateFailure(
                DefinitiveValidationFailure,
                SanitizeReasonCode(validationException.ValidationCodes.FirstOrDefault()) ??
                    $"HTTP_{(int)(validationException.StatusCode ?? System.Net.HttpStatusCode.UnprocessableEntity)}");
        }
        if ((exception is TaskCanceledException && !cancellationToken.IsCancellationRequested) ||
            exception is JsonException ||
            exception is HttpRequestException { StatusCode: null } ||
            (exception is HttpRequestException httpException &&
             httpException.StatusCode is not null &&
             (int)httpException.StatusCode.Value >= 500))
        {
            return new ApplicationCreateFailure(AmbiguousFailure, "AMBIGUOUS_OUTCOME");
        }

        return new ApplicationCreateFailure("NonValidationFailure", "NON_VALIDATION_FAILURE");
    }

    private static bool IsDefinitiveValidationFailure(NmiValidationException exception)
    {
        if (exception.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            return true;
        }

        return exception.StatusCode == System.Net.HttpStatusCode.BadRequest &&
            exception.ValidationCodes.Any(code =>
                string.Equals(code, "VALIDATION_ERROR", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(code, "VALIDATION_FAILED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(code, "IDEMPOTENCY_KEY_BAD_REQUEST", StringComparison.OrdinalIgnoreCase));
    }

    private static bool RequiresLegacyPayloadBoundRotation(
        string? idempotencyKey,
        ApplicationCreateOperationMetadata metadata,
        string payloadFingerprint)
    {
        return !string.IsNullOrWhiteSpace(idempotencyKey) &&
            idempotencyKey.StartsWith("nmi-application-create-", StringComparison.Ordinal) &&
            !idempotencyKey.StartsWith("nmi-application-create-v", StringComparison.Ordinal) &&
            string.Equals(metadata.PayloadFingerprint, payloadFingerprint, StringComparison.Ordinal) &&
            string.Equals(metadata.LastFailureClassification, DefinitiveValidationFailure, StringComparison.Ordinal) &&
            string.Equals(
                metadata.LastFailureReasonCode,
                "IDEMPOTENCY_KEY_BAD_REQUEST",
                StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationCreateOperationMetadata NewApplicationCreateMetadata(
        string payloadFingerprint,
        DateTimeOffset operationStartedAtUtc)
    {
        return new ApplicationCreateOperationMetadata(
            1,
            payloadFingerprint,
            operationStartedAtUtc,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    private static ApplicationCreateOperationMetadata? ReadApplicationCreateMetadata(string? capabilitiesJson)
    {
        if (string.IsNullOrWhiteSpace(capabilitiesJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(capabilitiesJson);
            return document.RootElement.TryGetProperty(ApplicationCreateMetadataProperty, out var metadata)
                ? metadata.Deserialize<ApplicationCreateOperationMetadata>()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string SetApplicationCreateMetadata(
        string? capabilitiesJson,
        ApplicationCreateOperationMetadata metadata)
    {
        var root = SafeJsonObject(capabilitiesJson);
        root[ApplicationCreateMetadataProperty] = JsonSerializer.SerializeToNode(metadata);
        return root.ToJsonString();
    }

    private static ApplicationUpdateOperationMetadata? ReadApplicationUpdateMetadata(string? capabilitiesJson)
    {
        if (string.IsNullOrWhiteSpace(capabilitiesJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(capabilitiesJson);
            return document.RootElement.TryGetProperty(ApplicationUpdateMetadataProperty, out var metadata)
                ? metadata.Deserialize<ApplicationUpdateOperationMetadata>()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string SetApplicationUpdateMetadata(
        string? capabilitiesJson,
        ApplicationUpdateOperationMetadata metadata)
    {
        var root = SafeJsonObject(capabilitiesJson);
        root[ApplicationUpdateMetadataProperty] = JsonSerializer.SerializeToNode(metadata);
        return root.ToJsonString();
    }

    private static string SetProviderCapabilities(
        string? capabilitiesJson,
        string providerStatus,
        string? providerReference,
        string? providerMerchantId)
    {
        var root = SafeJsonObject(capabilitiesJson);
        root["ProviderStatus"] = providerStatus;
        root["ProviderReference"] = providerReference;
        root["ProviderMerchantId"] = providerMerchantId;
        return root.ToJsonString();
    }

    private static JsonObject SafeJsonObject(string? json)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                if (JsonNode.Parse(json) is JsonObject root)
                {
                    return root;
                }
            }
            catch (JsonException)
            {
                // Invalid legacy metadata is discarded rather than propagated.
            }
        }

        return new JsonObject();
    }

    private static bool TryLegacyDefinitiveValidationReason(string? safeError, out string reasonCode)
    {
        reasonCode = string.Empty;
        if (string.IsNullOrWhiteSpace(safeError))
        {
            return false;
        }

        var codeMatch = Regex.Match(
            safeError,
            @"Codes:\s*([A-Za-z0-9_.:-]+)",
            RegexOptions.CultureInvariant);
        reasonCode = SanitizeReasonCode(codeMatch.Success ? codeMatch.Groups[1].Value : null) ?? "VALIDATION_ERROR";
        if (Regex.IsMatch(safeError, @"HTTP 422", RegexOptions.CultureInvariant))
        {
            return true;
        }

        return Regex.IsMatch(safeError, @"HTTP 400", RegexOptions.CultureInvariant) &&
            reasonCode is "VALIDATION_ERROR" or "VALIDATION_FAILED" or "IDEMPOTENCY_KEY_BAD_REQUEST";
    }

    private static string? SanitizeReasonCode(string? value)
    {
        var clean = Clean(value)?.TrimEnd('.', ':', '-');
        return clean is not null && Regex.IsMatch(clean, @"^[A-Za-z0-9_.:-]{1,80}$", RegexOptions.CultureInvariant)
            ? clean.ToUpperInvariant()
            : null;
    }

    private static string StableIdempotencyKey(string operation, Guid merchantAccountId)
    {
        return $"{operation}-{merchantAccountId:N}";
    }

    private sealed record ApplicationCreateOperationMetadata(
        int KeyVersion,
        string PayloadFingerprint,
        DateTimeOffset OperationStartedAtUtc,
        DateTimeOffset? RotatedAtUtc,
        string? RotationReasonCode,
        string? LastFailureClassification,
        string? LastFailureReasonCode,
        DateTimeOffset? LastFailureAtUtc,
        IReadOnlyList<ApplicationCreateRotationAudit>? Rotations);

    private sealed record ApplicationCreateRotationAudit(
        int KeyVersion,
        string PayloadFingerprint,
        DateTimeOffset RotatedAtUtc,
        string ReasonCode);

    private sealed record ApplicationUpdateOperationMetadata(
        int KeyVersion,
        string PayloadFingerprint,
        string IdempotencyKey,
        DateTimeOffset PreparedAtUtc,
        DateTimeOffset? RotatedAtUtc,
        string? RotationReasonCode,
        IReadOnlyList<ApplicationUpdateRotationAudit>? Rotations);

    private sealed record ApplicationUpdateRotationAudit(
        int KeyVersion,
        string PayloadFingerprint,
        DateTimeOffset RotatedAtUtc,
        string ReasonCode);

    private sealed record ApplicationCreateFailure(string Classification, string ReasonCode);

    private static string SafeProviderError(Exception exception, string fallback)
    {
        if (exception is NmiValidationException ||
            exception.Message.Contains("legal consent", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("reconciliation", StringComparison.OrdinalIgnoreCase))
        {
            return exception.Message;
        }

        if (exception is HttpRequestException { StatusCode: not null } httpException)
        {
            return $"{fallback.TrimEnd('.')} (HTTP {(int)httpException.StatusCode.Value}).";
        }

        return fallback;
    }

    private static string SafeCredentialMetadata(
        string? existingJson,
        string keyName,
        string? keyId,
        string? description)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(existingJson))
        {
            try
            {
                using var document = JsonDocument.Parse(existingJson);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    values[property.Name] = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString()
                        : property.Value.ToString();
                }
            }
            catch (JsonException)
            {
                values.Clear();
            }
        }

        values[keyName] = Clean(keyId);
        values[$"{keyName}Description"] = Clean(description);
        return JsonSerializer.Serialize(values);
    }

    private static string? NewSecretValue(string? value)
    {
        var clean = Clean(value);
        return clean is null || clean.Contains('*') || string.Equals(clean, "Configured", StringComparison.OrdinalIgnoreCase)
            ? null
            : clean;
    }

    private static string Mask(string value)
    {
        return MaskLastFour(LastFour(value)) ?? "Configured";
    }

    private static string? MaskLastFour(string? lastFour)
    {
        return string.IsNullOrWhiteSpace(lastFour) ? null : $"****{lastFour}";
    }
}
