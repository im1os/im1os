using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.Platform;
using iM1os.Domain.Configuration;
using iM1os.Domain.Identity;
using iM1os.Domain.Platform;
using iM1os.Domain.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class TenantProvisioningService(
    IApplicationDbContext dbContext,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IWelcomeEmailSender welcomeEmailSender,
    IDateTimeProvider dateTimeProvider) : ITenantProvisioningService
{
    public async Task<ProvisionTenantResult> ProvisionAsync(ProvisionTenantRequest request, string? platformUserId, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N");
        var slug = Slugify(request.BusinessName);
        var normalizedOwnerEmail = request.OwnerEmail.Trim().ToUpperInvariant();

        if (await dbContext.Organizations.IgnoreQueryFilters().AnyAsync(x => x.Slug == slug, cancellationToken))
        {
            throw new InvalidOperationException("An organization with this business name already exists.");
        }

        if (await dbContext.Users.IgnoreQueryFilters().AnyAsync(x => x.NormalizedEmail == normalizedOwnerEmail, cancellationToken))
        {
            throw new InvalidOperationException("An owner user with this email already exists.");
        }

        AddPlatformEvent(null, platformUserId, "TenantProvisioningStarted", now, correlationId, new
        {
            request.BusinessName,
            request.OwnerEmail,
            request.SubscriptionPlan
        });

        var organization = new Organization
        {
            Name = Required(request.BusinessName, "Business name"),
            Slug = slug
        };

        var location = new Location
        {
            OrganizationId = organization.Id,
            Name = "Main",
            Code = "MAIN",
            Phone = Required(request.Phone, "Phone"),
            AddressLine1 = Required(request.AddressLine1, "Address line 1"),
            AddressLine2 = Clean(request.AddressLine2),
            City = Required(request.City, "City"),
            Region = Required(request.Region, "Region"),
            PostalCode = Required(request.PostalCode, "Postal code")
        };

        var ownerRole = new Role
        {
            OrganizationId = organization.Id,
            Name = "Owner",
            NormalizedName = "OWNER",
            IsSystemRole = true
        };

        var owner = new ApplicationUser
        {
            OrganizationId = organization.Id,
            Email = Required(request.OwnerEmail, "Owner email"),
            NormalizedEmail = normalizedOwnerEmail,
            DisplayName = Required(request.OwnerName, "Owner name"),
            PasswordHash = string.Empty
        };
        owner.PasswordHash = passwordHasher.HashPassword(owner, "ChangeMe!12345");
        owner.UserRoles.Add(new UserRole { UserId = owner.Id, RoleId = ownerRole.Id });
        owner.OrganizationMemberships.Add(new OrganizationMembership
        {
            OrganizationId = organization.Id,
            UserId = owner.Id,
            DisplayName = owner.DisplayName
        });

        var trialExpiresAt = request.IsTrial ? now.AddDays(14) : null as DateTimeOffset?;
        var billingStatus = request.IsTrial ? "Trial" : "Active";
        var tenantStatus = request.IsTrial ? "Trial" : "Active";

        var platformTenant = new PlatformTenant
        {
            OrganizationId = organization.Id,
            OrganizationName = organization.Name,
            Slug = organization.Slug,
            Status = tenantStatus,
            SubscriptionPlan = Required(request.SubscriptionPlan, "Subscription plan"),
            CurrentVersion = "v0.1.0",
            HealthStatus = "Healthy",
            ActiveUsers = 1,
            Locations = 1,
            TrialExpiresAtUtc = trialExpiresAt,
            BillingStatus = billingStatus,
            ProvisioningStatus = "Provisioned"
        };

        var subscription = new PlatformSubscription
        {
            OrganizationId = organization.Id,
            Plan = platformTenant.SubscriptionPlan,
            BillingStatus = billingStatus,
            IsTrial = request.IsTrial,
            TrialExpiresAtUtc = trialExpiresAt
        };

        dbContext.Organizations.Add(organization);
        dbContext.Locations.Add(location);
        dbContext.Roles.Add(ownerRole);
        dbContext.Users.Add(owner);
        dbContext.PlatformTenants.Add(platformTenant);
        dbContext.PlatformSubscriptions.Add(subscription);

        dbContext.ApplicationSettings.AddRange(
            new ApplicationSetting { OrganizationId = organization.Id, Key = "tenant.language", Value = Required(request.DefaultLanguage, "Default language") },
            new ApplicationSetting { OrganizationId = organization.Id, Key = "tenant.currency", Value = Required(request.DefaultCurrency, "Default currency") },
            new ApplicationSetting { OrganizationId = organization.Id, Key = "tenant.timezone", Value = Required(request.TimeZone, "Time zone") },
            new ApplicationSetting { OrganizationId = organization.Id, Key = "tenant.country", Value = Required(request.Country, "Country") },
            new ApplicationSetting { OrganizationId = organization.Id, Key = "tenant.businessEmail", Value = Required(request.BusinessEmail, "Business email") });

        foreach (var module in request.DefaultModules.Select(Clean).Where(x => x is not null).Select(x => x!).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            dbContext.TenantModuleEntitlements.Add(new TenantModuleEntitlement
            {
                OrganizationId = organization.Id,
                ModuleKey = module,
                IsEnabled = true,
                EnabledAtUtc = now,
                EnabledByPlatformUserId = platformUserId
            });

            AddPlatformEvent(organization.Id, platformUserId, "FeatureEnabled", now, correlationId, new { ModuleKey = module });
        }

        AddPlatformEvent(organization.Id, platformUserId, "SubscriptionCreated", now, correlationId, new
        {
            subscription.Plan,
            subscription.BillingStatus,
            subscription.IsTrial,
            subscription.TrialExpiresAtUtc
        });
        AddPlatformEvent(organization.Id, platformUserId, "OwnerCreated", now, correlationId, new { owner.Email, owner.DisplayName });
        AddPlatformEvent(organization.Id, platformUserId, "TenantProvisioned", now, correlationId, new { organization.Name, organization.Slug });
        AddAudit(platformUserId, organization.Id, "TenantProvisioned", now, null, new
        {
            organization.Name,
            organization.Slug,
            subscription.Plan,
            OwnerEmail = owner.Email
        });

        var welcomeEmail = new WelcomeEmail
        {
            OrganizationId = organization.Id,
            RecipientEmail = owner.Email,
            RecipientName = owner.DisplayName,
            Subject = "Welcome to IM1OS",
            Body = $"Welcome {owner.DisplayName}. Your IM1OS instance for {organization.Name} is ready. Temporary password: ChangeMe!12345",
            CreatedAtUtc = now
        };

        await welcomeEmailSender.SendAsync(welcomeEmail.RecipientEmail, welcomeEmail.Subject, welcomeEmail.Body, cancellationToken);
        welcomeEmail.SentAtUtc = now;
        dbContext.WelcomeEmails.Add(welcomeEmail);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ProvisionTenantResult(organization.Id, owner.Id, location.Id, platformTenant.Id);
    }

    private void AddPlatformEvent(Guid? targetOrganizationId, string? platformUserId, string eventType, DateTimeOffset now, string correlationId, object payload)
    {
        dbContext.PlatformEvents.Add(new PlatformEvent
        {
            TargetOrganizationId = targetOrganizationId,
            ActorPlatformUserId = platformUserId,
            EventType = eventType,
            OccurredAtUtc = now,
            PayloadJson = JsonSerializer.Serialize(payload),
            CorrelationId = correlationId
        });
    }

    private void AddAudit(string? platformUserId, Guid targetOrganizationId, string action, DateTimeOffset now, object? previousValues, object newValues)
    {
        dbContext.PlatformAuditEvents.Add(new PlatformAuditEvent
        {
            ActorPlatformUserId = platformUserId,
            TargetOrganizationId = targetOrganizationId,
            Action = action,
            OccurredAtUtc = now,
            PreviousValuesJson = previousValues is null ? null : JsonSerializer.Serialize(previousValues),
            NewValuesJson = JsonSerializer.Serialize(newValues)
        });
    }

    private static string Slugify(string value)
    {
        var cleaned = new string(value.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
        while (cleaned.Contains("--", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
        }

        return cleaned.Trim('-');
    }

    private static string Required(string value, string fieldName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{fieldName} is required.")
            : value.Trim();
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
