using iM1os.Web.Controllers;

namespace iM1os.Web.Development;

/// <summary>
/// Synthetic data for the NMI sandbox workflow. This fixture is reachable only
/// when the web application runs in the Development environment.
/// </summary>
public static class NmiSandboxMerchantApplicationFixture
{
    public static MerchantApplicationForm Create()
    {
        return new MerchantApplicationForm
        {
            BusinessName = "NMI Sandbox Motorcycle Supply LLC",
            Dba = "NMI Sandbox Moto Supply",
            Ein = "12-3456789",
            TaxId = "123456789",
            BusinessType = "LLC",
            BusinessDescription = "Motorcycle parts, accessories, and service for NMI sandbox acceptance testing.",
            YearsInBusiness = 10,
            PhysicalAddressLine1 = "100 Sandbox Way",
            PhysicalCity = "Austin",
            PhysicalRegion = "TX",
            PhysicalPostalCode = "78701",
            PhysicalCountry = "US",
            MailingAddressLine1 = "100 Sandbox Way",
            MailingCity = "Austin",
            MailingRegion = "TX",
            MailingPostalCode = "78701",
            MailingCountry = "US",
            OwnerName = "NMI Sandbox Signer",
            OwnerEmail = "nmi-sandbox-signer@example.com",
            OwnerPhone = "5125550199",
            OwnerTitle = "Owner",
            OwnerOwnershipPercentage = 100m,
            OwnerDateOfBirth = "1985-01-15",
            OwnerSsn = "111223333",
            BankName = "NMI Sandbox Test Bank",
            BankRoutingNumber = "111000025",
            BankAccountNumber = "1234567890",
            ExpectedMonthlyVolume = 25000m,
            AverageTicket = 250m,
            HighTicket = 1500m,
            CardPresentPercentage = 70m,
            KeyEnteredPercentage = 10m,
            EcommercePercentage = 15m,
            MotoPercentage = 5m,
            Website = "https://example.com",
            Mcc = "5533"
        };
    }
}
