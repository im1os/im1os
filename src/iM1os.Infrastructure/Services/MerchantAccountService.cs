using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.FinancialServices.Merchant;
using iM1os.Application.FinancialServices.Providers;
using iM1os.Domain.FinancialServices.Events;
using iM1os.Domain.FinancialServices.Merchant;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class MerchantAccountService(
    IApplicationDbContext dbContext,
    IEnumerable<IPartnerProvider> partnerProviders,
    IDomainEventRecorder domainEventRecorder,
    IDateTimeProvider dateTimeProvider) : IMerchantAccountService
{
    private static readonly IReadOnlySet<string> ApplicationStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        MerchantAccountStatuses.Submitted,
        MerchantAccountStatuses.UnderReview,
        MerchantAccountStatuses.Approved,
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
                false);
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
            summary.IsProcessingReady);
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
        return await SubmitApplicationAsync(
            organizationId,
            actorUserId,
            new MerchantApplicationRequest(
                request.LegalBusinessName,
                null,
                null,
                null,
                null,
                request.AddressLine1,
                request.AddressLine2,
                request.City,
                request.Region,
                request.PostalCode,
                request.Country,
                null,
                null,
                null,
                null,
                null,
                null,
                $"{request.FirstName} {request.LastName}".Trim(),
                request.Email,
                request.Phone,
                null,
                null,
                null,
                null,
                null,
                request.Website,
                null,
                request.ProviderCode),
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
        var provider = ResolveProvider(merchantAccount.PrimaryProviderCode);
        MerchantProviderRelationship? relationship = null;
        try
        {
            await TransitionAsync(
                merchantAccount,
                await GetPrimaryRelationshipAsync(merchantAccount, cancellationToken),
                actorUserId,
                MerchantAccountStatuses.Approved,
                "Merchant application approved.",
                cancellationToken);

            relationship = await GetOrCreateRelationshipAsync(merchantAccount, provider.ProviderCode, cancellationToken);
            var providerRequest = ToProviderRequest(merchantAccount);
            var hasPendingProviderApplication =
                !string.IsNullOrWhiteSpace(relationship.ProviderReference) &&
                string.IsNullOrWhiteSpace(relationship.ProviderMerchantId);
            var providerResult = hasPendingProviderApplication
                ? await provider.SubmitMerchantApplicationAsync(relationship.ProviderReference!, providerRequest, cancellationToken)
                : await provider.CreateMerchantAsync(providerRequest, cancellationToken);
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
            relationship ??= await GetOrCreateRelationshipAsync(merchantAccount, provider.ProviderCode, cancellationToken);
            relationship.LastProviderError = ex.Message;
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
            relationship.LastProviderError = ex.Message;
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
        relationship.ProviderMerchantId = providerResult.ProviderMerchantId;
        relationship.ProviderReference = providerResult.ProviderReference ?? relationship.ProviderReference ?? providerResult.ProviderMerchantId;
        relationship.GatewayUsername = providerResult.GatewayUsername;
        relationship.GatewayPasswordProtected = providerResult.GatewayPassword;
        relationship.CapabilitiesJson = providerResult.RawResponse;
        relationship.LastProviderError = null;
        relationship.SupportNotes = SupportNotesFor(providerResult);
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

        if (!string.Equals(providerResult.Status, MerchantAccountStatuses.Active, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(providerResult.ProviderMerchantId))
        {
            await TransitionAsync(
                merchantAccount,
                relationship,
                actorUserId,
                MerchantAccountStatuses.UnderReview,
                string.Equals(providerResult.Status, "LegalConsentRequired", StringComparison.OrdinalIgnoreCase)
                    ? "NMI Sign-Up application created. Legal consent is required before submission."
                    : "NMI Sign-Up application is under review.",
                cancellationToken);

            return ToResult(merchantAccount, relationship);
        }

        if (string.IsNullOrWhiteSpace(relationship.PaymentApiKeyProtected))
        {
            var paymentKey = await provider.CreateMerchantCredentialAsync(
                new PartnerMerchantCredentialRequest(
                    providerResult.ProviderMerchantId,
                    "iM1 Payments transaction key",
                    new[] { "transaction" }),
                cancellationToken);
            relationship.PaymentApiKeyProtected = paymentKey.PrivateKey ?? paymentKey.PublicKey;
            relationship.CredentialMetadataJson = JsonSerializer.Serialize(new
            {
                PaymentKey = paymentKey.RawResponse,
                Existing = relationship.CredentialMetadataJson
            });
        }

        if (string.IsNullOrWhiteSpace(relationship.PublicTokenizationKey))
        {
            var tokenizationKey = await provider.CreateMerchantCredentialAsync(
                new PartnerMerchantCredentialRequest(
                    providerResult.ProviderMerchantId,
                    "iM1 Payments Collect.js tokenization key",
                    new[] { "tokenization" }),
                cancellationToken);
            relationship.PublicTokenizationKey = tokenizationKey.PublicKey ?? tokenizationKey.PrivateKey;
            relationship.CredentialMetadataJson = JsonSerializer.Serialize(new
            {
                TokenizationKey = tokenizationKey.RawResponse,
                Existing = relationship.CredentialMetadataJson
            });
        }

        relationship.LastProviderError = null;
        relationship.SupportNotes = null;
        merchantAccount.PaymentsEnabled = true;
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
            (relationship is null || string.IsNullOrWhiteSpace(relationship.ProviderMerchantId)))
        {
            throw new InvalidOperationException("Provider merchant relationship is required before activation.");
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

    private static void ApplyApplication(MerchantAccount merchantAccount, MerchantApplicationRequest request, string providerCode)
    {
        merchantAccount.LegalBusinessName = Clean(request.BusinessName);
        merchantAccount.Dba = Clean(request.Dba);
        merchantAccount.Ein = Clean(request.Ein);
        merchantAccount.TaxIdentifierLastFour = LastFour(request.TaxId);
        merchantAccount.BusinessType = Clean(request.BusinessType);
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
        merchantAccount.BankName = Clean(request.BankName);
        merchantAccount.BankRoutingLastFour = LastFour(request.BankRoutingNumber);
        merchantAccount.BankAccountLastFour = LastFour(request.BankAccountNumber);
        merchantAccount.ExpectedMonthlyVolume = request.ExpectedMonthlyVolume;
        merchantAccount.AverageTicket = request.AverageTicket;
        merchantAccount.Website = Clean(request.Website);
        merchantAccount.Mcc = Clean(request.Mcc);
        merchantAccount.PrimaryProviderCode = providerCode;
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

    private static PartnerMerchantCreateRequest ToProviderRequest(MerchantAccount merchantAccount)
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
            merchantAccount.Id.ToString());
    }

    private static string? SupportNotesFor(PartnerMerchantCreateResult providerResult)
    {
        if (!string.Equals(providerResult.Status, "LegalConsentRequired", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(providerResult.RawResponse))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(providerResult.RawResponse);
            return JsonString(document.RootElement, "LegalConsentUrl") ??
                JsonString(document.RootElement, "legalConsentUrl") ??
                JsonString(document.RootElement, "url") ??
                "Legal consent is required before NMI Sign-Up application submission.";
        }
        catch (JsonException)
        {
            return providerResult.RawResponse;
        }
    }

    private static string? JsonString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                ? property.ToString()
                : null;
    }

    private static MerchantApplicationFormModel EmptyApplication(Guid organizationId)
    {
        return new MerchantApplicationFormModel(
            organizationId,
            null,
            MerchantAccountStatuses.NotStarted,
            "DEV Smoke Powersports",
            "DEV Smoke Powersports",
            "82-1234567",
            "821234567",
            "LLC",
            "100 Main St",
            "Suite 100",
            "Dallas",
            "TX",
            "75001",
            "US",
            "100 Main St",
            "Suite 100",
            "Dallas",
            "TX",
            "75001",
            "US",
            "Bradley Molen",
            "brad.molen+dev-smoke@example.com",
            "2145551212",
            "First National Test Bank",
            "6789",
            "7890",
            5000,
            100,
            "https://dev-smoke-powersports.example.com",
            "5533",
            true,
            true);
    }

    private static MerchantApplicationFormModel ToApplicationForm(MerchantAccount merchantAccount)
    {
        var canEdit = CanEditApplication(merchantAccount.Status);
        var canSubmit = CanSubmitApplication(merchantAccount.Status);
        return new MerchantApplicationFormModel(
            merchantAccount.OrganizationId,
            merchantAccount.Id,
            merchantAccount.Status,
            merchantAccount.LegalBusinessName ?? string.Empty,
            merchantAccount.Dba,
            merchantAccount.Ein,
            merchantAccount.TaxIdentifierLastFour,
            merchantAccount.BusinessType,
            merchantAccount.PhysicalAddressLine1 ?? string.Empty,
            merchantAccount.PhysicalAddressLine2,
            merchantAccount.PhysicalCity ?? string.Empty,
            merchantAccount.PhysicalRegion ?? string.Empty,
            merchantAccount.PhysicalPostalCode ?? string.Empty,
            merchantAccount.PhysicalCountry ?? "US",
            merchantAccount.MailingAddressLine1,
            merchantAccount.MailingAddressLine2,
            merchantAccount.MailingCity,
            merchantAccount.MailingRegion,
            merchantAccount.MailingPostalCode,
            merchantAccount.MailingCountry,
            merchantAccount.OwnerName ?? string.Empty,
            merchantAccount.OwnerEmail ?? string.Empty,
            merchantAccount.OwnerPhone ?? string.Empty,
            merchantAccount.BankName,
            merchantAccount.BankRoutingLastFour,
            merchantAccount.BankAccountLastFour,
            merchantAccount.ExpectedMonthlyVolume,
            merchantAccount.AverageTicket,
            merchantAccount.Website,
            merchantAccount.Mcc,
            canEdit,
            canSubmit);
    }

    private static MerchantAccountSummary ToSummary(MerchantAccount account, MerchantProviderRelationship? relationship)
    {
        var hasProviderMerchant = relationship is not null && !string.IsNullOrWhiteSpace(relationship.ProviderMerchantId);
        var isReady =
            string.Equals(account.Status, MerchantAccountStatuses.Active, StringComparison.OrdinalIgnoreCase) &&
            relationship is not null &&
            string.Equals(relationship.Status, MerchantAccountStatuses.Active, StringComparison.OrdinalIgnoreCase) &&
            hasProviderMerchant;
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

    private static PlatformMerchantAccountRow ToPlatformRow(
        MerchantAccount account,
        MerchantProviderRelationship? relationship,
        string? companyName,
        IReadOnlyCollection<MerchantStatusHistoryRow> history)
    {
        return new PlatformMerchantAccountRow(
            account.OrganizationId,
            account.Id,
            companyName,
            account.Status,
            account.UnderwritingStatus,
            account.LegalBusinessName,
            account.OwnerName,
            account.OwnerEmail,
            account.ExpectedMonthlyVolume,
            relationship?.ProviderCode ?? account.PrimaryProviderCode,
            relationship?.ProviderMerchantId,
            relationship?.GatewayUsername,
            !string.IsNullOrWhiteSpace(relationship?.PaymentApiKeyProtected),
            !string.IsNullOrWhiteSpace(relationship?.PublicTokenizationKey),
            relationship?.ProviderReference,
            relationship?.Status,
            relationship?.LastProviderError,
            relationship?.SupportNotes,
            account.CreatedAtUtc,
            account.UpdatedAtUtc,
            history);
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
            MerchantAccountStatuses.UnderReview => new[] { "The merchant application has been submitted to NMI for review." },
            MerchantAccountStatuses.Approved => new[] { "Platform approval is complete. Merchant activation is finishing." },
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

    private static string? LastFour(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? digits[^4..] : null;
    }
}
