using iM1os.Application.Common;
using Microsoft.AspNetCore.DataProtection;

namespace iM1os.Infrastructure.Security;

public sealed class DataProtectionSecretProtector(IDataProtectionProvider dataProtectionProvider) : ISecretProtector
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector(
        "iM1os.FinancialServices.Secrets.v1");

    public string Protect(string plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            throw new ArgumentException("A secret value is required.", nameof(plaintext));
        }

        return protector.Protect(plaintext);
    }

    public string Unprotect(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            throw new ArgumentException("A protected secret value is required.", nameof(protectedValue));
        }

        return protector.Unprotect(protectedValue);
    }
}
