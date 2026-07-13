namespace iM1os.Tests;

public sealed class NmiSandboxPaymentViewTests
{
    [Fact]
    public void Payment_form_allows_Nmi_sandbox_decline_amounts_below_one_dollar()
    {
        var viewSource = File.ReadAllText(FindRepositoryFile(
            "src",
            "iM1os.Web",
            "Views",
            "Payments",
            "Index.cshtml"));

        Assert.Contains(
            "id=\"im1PaymentAmount\" name=\"Amount\" type=\"number\" min=\"0.01\" step=\"0.01\" value=\"1.00\"",
            viewSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("min=\"1.00\"", viewSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Payment_history_formats_USD_with_an_explicit_dollar_currency_culture()
    {
        var viewSource = File.ReadAllText(FindRepositoryFile(
            "src",
            "iM1os.Web",
            "Views",
            "Payments",
            "Index.cshtml"));

        Assert.Contains(
            "value.ToString(\"C2\", System.Globalization.CultureInfo.GetCultureInfo(\"en-US\"))",
            viewSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("? value.ToString(\"C2\")", viewSource, StringComparison.Ordinal);
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
