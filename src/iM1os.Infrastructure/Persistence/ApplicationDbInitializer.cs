using iM1os.Domain.Configuration;
using iM1os.Domain.Identity;
using iM1os.Domain.Marketing;
using iM1os.Domain.Platform;
using iM1os.Domain.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iM1os.Infrastructure.Persistence;

public sealed class ApplicationDbInitializer(
    ApplicationDbContext dbContext,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IPasswordHasher<PlatformUser> platformPasswordHasher,
    ILogger<ApplicationDbInitializer> logger) : IApplicationDbInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        await SeedMarketingAsync(cancellationToken);

        if (await dbContext.PlatformUsers.AnyAsync(cancellationToken))
        {
            return;
        }

        logger.LogInformation("Seeding initial iM1 OS platform data.");

        var platformUser = new PlatformUser
        {
            Email = "admin@im1os.com",
            NormalizedEmail = "admin@im1os.com".ToUpperInvariant(),
            DisplayName = "Platform Administrator",
            PasswordHash = string.Empty,
            Role = "Platform Administrator"
        };
        platformUser.PasswordHash = platformPasswordHasher.HashPassword(platformUser, "ChangeMe!12345");

        dbContext.PlatformUsers.Add(platformUser);

        if (await dbContext.Organizations.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var organization = new Organization { Name = "iM1 OS", Slug = "im1os" };
        var permissions = new[]
        {
            new Permission { Key = "platform.manage", Description = "Manage platform configuration." },
            new Permission { Key = "users.manage", Description = "Manage users and access." },
            new Permission { Key = "organizations.manage", Description = "Manage organizations." }
        };

        var adminRole = new Role
        {
            OrganizationId = organization.Id,
            Name = "Platform Administrator",
            NormalizedName = "PLATFORM ADMINISTRATOR",
            IsSystemRole = true
        };

        foreach (var permission in permissions)
        {
            adminRole.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionId = permission.Id });
        }

        var user = new ApplicationUser
        {
            OrganizationId = organization.Id,
            Email = "admin@im1os.com",
            NormalizedEmail = "admin@im1os.com".ToUpperInvariant(),
            DisplayName = "Platform Administrator",
            PasswordHash = string.Empty
        };
        user.PasswordHash = passwordHasher.HashPassword(user, "ChangeMe!12345");
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });

        dbContext.Organizations.Add(organization);
        dbContext.Permissions.AddRange(permissions);
        dbContext.Roles.Add(adminRole);
        dbContext.Users.Add(user);
        dbContext.FeatureFlags.Add(new FeatureFlag { Key = "dashboard.shell", IsEnabled = true, Description = "Enables the initial dashboard shell." });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedMarketingAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.MarketingPages.AnyAsync(cancellationToken))
        {
            return;
        }

        var home = new MarketingPage
        {
            Slug = "home",
            Title = "The Operating System for Modern Powersports Dealers",
            NavigationLabel = "Home",
            MetaDescription = "iM1 OS is a modern cloud operating system for powersports dealerships, connecting operations, suppliers, ecommerce, service, customers, marketing, and growth.",
            OpenGraphTitle = "iM1 OS",
            OpenGraphDescription = "The operating system for modern powersports dealers.",
            CanonicalUrl = "https://im1os.com/",
            IsPublished = true,
            SortOrder = 0
        };

        var blocks = new[]
        {
            Block("hero", 0, "iM1 OS", "The Operating System for Modern Powersports Dealers", "One platform to manage inventory, suppliers, ecommerce, service, customers, marketing, and growth.", "Request Demo", "#contact", "Learn More", "#platform"),
            Block("platform", 10, "Platform", "Built for modern powersports operations", "iM1 OS is cloud native, modular, scalable, secure, and designed specifically for powersports dealerships that need connected operations without outdated software friction.", null, null, null, null,
                """[{"title":"Cloud native","body":"Work from anywhere with a browser-based platform built for modern teams."},{"title":"Modular","body":"Start with the capabilities you need and add more over time."},{"title":"Scalable","body":"Support growing teams, product lines, and multi-location operations."},{"title":"Secure","body":"Role-based access, managed updates, and reliable cloud hosting."}]"""),
            Block("products", 20, "Products", "A growing business application suite", "Add dealership applications over time while keeping a consistent operating experience.", null, null, null, null,
                """[{"title":"Dealer Management","status":"Coming Soon"},{"title":"Inventory","status":"Coming Soon"},{"title":"Service","status":"Coming Soon"},{"title":"CRM","status":"Coming Soon"},{"title":"Marketing","status":"Coming Soon"},{"title":"Supplier Connect","status":"Coming Soon"},{"title":"eCommerce","status":"Coming Soon"},{"title":"Accounting","status":"Coming Soon"},{"title":"Analytics","status":"Coming Soon"},{"title":"Point of Sale","status":"Coming Soon"},{"title":"Customer Portal","status":"Coming Soon"}]"""),
            Block("integrations", 30, "Supplier Integrations", "Connect the suppliers dealers rely on", "Bring supplier availability and purchasing into everyday dealership work so teams spend less time switching systems.", null, null, null, null,
                """[{"title":"WPS","body":"Supported supplier connection planning."},{"title":"Turn14","body":"Performance and aftermarket supplier connectivity."},{"title":"Parts Unlimited","body":"Powersports supplier support planning."},{"title":"More coming soon","body":"The platform is built to grow with supplier relationships."}]"""),
            Block("ecommerce", 40, "eCommerce", "Keep online selling connected", "Connect storefront operations for products, inventory, pricing, orders, and tracking across Shopify, WooCommerce, Wix, and BigCommerce.", null, null, null, null,
                """[{"title":"Shopify"},{"title":"WooCommerce"},{"title":"Wix"},{"title":"BigCommerce"}]"""),
            Block("comparison", 50, "Why iM1 OS", "Traditional software vs iM1 OS", "iM1 OS brings a modern cloud experience, automatic updates, open integrations, real-time data, multi-location visibility, scalability, and dealer-focused workflows to everyday operations.", null, null, null, null,
                """[{"traditional":"Disconnected tools","im1":"One connected operating system"},{"traditional":"Manual updates","im1":"Automatic improvements"},{"traditional":"Limited integrations","im1":"Open partner ecosystem"},{"traditional":"Old interfaces","im1":"Modern, consistent UI"},{"traditional":"Location silos","im1":"Multi-location visibility"}]"""),
            Block("security", 60, "Security", "Trusted cloud operations", "iM1 OS is cloud hosted with encrypted connections, role-based permissions, automatic updates, and backup-ready operations.", null, null, null, null),
            Block("testimonials", 70, "Dealer Perspective", "Built around daily dealership work", "iM1 OS is shaped around the operational pressure dealers face every day: inventory accuracy, service throughput, customer follow-up, supplier coordination, and online growth.", null, null, null, null,
                """[{"quote":"We need fewer disconnected systems and better visibility across the dealership.","name":"Powersports operator"},{"quote":"The value is having inventory, customers, and selling channels working from the same picture.","name":"Dealership leadership"}]"""),
            Block("faq", 80, "FAQ", "Common questions", "Straightforward answers for dealerships evaluating iM1 OS.", null, null, null, null,
                """[{"question":"Who is iM1 OS for?","answer":"Powersports dealerships that want a modern, connected way to manage daily operations and growth."},{"question":"Can modules be added over time?","answer":"Yes. The platform is designed so dealerships can add business applications as needs expand."},{"question":"Does iM1 OS support multiple locations?","answer":"Yes. The experience is designed for both single-location and growing multi-location dealership groups."}]"""),
            Block("pricing", 90, "Pricing", "Simple plans for growing dealers", "Pricing will be matched to dealership size, selected products, and operational needs. Request a demo to discuss the right starting point.", null, null, null, null,
                """[{"title":"Launch","body":"Core platform access for dealerships getting started.","status":"Contact Sales"},{"title":"Growth","body":"Expanded operational modules and connected workflows.","status":"Contact Sales"},{"title":"Enterprise","body":"Advanced support for larger and multi-location dealer groups.","status":"Contact Sales"}]"""),
            Block("announcements", 100, "Announcements", "iM1 OS is opening for dealer conversations", "We are working with powersports dealers who want to modernize operations and shape the future of the platform.", null, null, null, null,
                """[{"title":"Dealer discovery now open","body":"Request a demo to talk through your dealership operations and priorities."},{"title":"Supplier and ecommerce focus","body":"Early platform content highlights connected suppliers, inventory, online selling, and operational visibility."}]"""),
            Block("vision", 110, "Future Vision", "A platform that grows with your dealership", "iM1 OS is designed as a growing business platform. Additional applications can be added over time while preserving the same familiar operating experience.", null, null, null, null),
            Block("contact", 120, "Contact", "Request a demo", "See how iM1 OS can help modernize dealership operations.", "Request Demo", "#contact-form", null, null)
        };

        foreach (var block in blocks)
        {
            home.Blocks.Add(block);
        }

        dbContext.MarketingPages.Add(home);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static MarketingContentBlock Block(
        string type,
        int sortOrder,
        string eyebrow,
        string heading,
        string body,
        string? primaryLabel,
        string? primaryUrl,
        string? secondaryLabel,
        string? secondaryUrl,
        string? itemsJson = null)
    {
        return new MarketingContentBlock
        {
            BlockType = type,
            SortOrder = sortOrder,
            Eyebrow = eyebrow,
            Heading = heading,
            Body = body,
            PrimaryActionLabel = primaryLabel,
            PrimaryActionUrl = primaryUrl,
            SecondaryActionLabel = secondaryLabel,
            SecondaryActionUrl = secondaryUrl,
            ItemsJson = itemsJson,
            IsPublished = true
        };
    }
}
