namespace iM1os.Tests;

public sealed class NmiLegalConsentWidgetTests
{
    [Fact]
    public void Merchant_workspace_uses_Nmi_helper_and_requires_verified_acceptance()
    {
        var viewSource = File.ReadAllText(FindRepositoryFile(
            "src",
            "iM1os.Web",
            "Views",
            "FinancialServices",
            "MerchantAccount.cshtml"));

        Assert.Contains("consent-helper-1.0.0.umd.js", viewSource, StringComparison.Ordinal);
        Assert.Contains("window.LegalConsent.load", viewSource, StringComparison.Ordinal);
        Assert.Contains("onAccepted", viewSource, StringComparison.Ordinal);
        Assert.Contains("result?.accepted === true", viewSource, StringComparison.Ordinal);
        Assert.Contains("id=\"nmi-legal-consent-submit\"", viewSource, StringComparison.Ordinal);
        Assert.Contains("disabled", viewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("<iframe title=\"NMI legal consent\"", viewSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://sandbox.signup.nmi.com/build/consent-helper-1.0.0.umd.js")]
    [InlineData("https://signup.nmi.com/build/consent-helper-1.0.0.umd.js")]
    public void Merchant_workspace_allows_only_https_Nmi_signup_hosts(string expectedUrl)
    {
        var viewSource = File.ReadAllText(FindRepositoryFile(
            "src",
            "iM1os.Web",
            "Views",
            "FinancialServices",
            "MerchantAccount.cshtml"));

        Assert.Contains("legalConsentHelperUri.Scheme == Uri.UriSchemeHttps", viewSource, StringComparison.Ordinal);
        Assert.Contains(new Uri(expectedUrl).Host, viewSource, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] pathSegments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. pathSegments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Repository file was not found: {Path.Combine(pathSegments)}");
    }
}
