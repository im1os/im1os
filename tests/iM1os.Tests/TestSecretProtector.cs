using System.Text;
using iM1os.Application.Common;

namespace iM1os.Tests;

internal sealed class TestSecretProtector : ISecretProtector
{
    private const string Prefix = "test-protected:";

    public string Protect(string plaintext)
    {
        return Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));
    }

    public string Unprotect(string protectedValue)
    {
        if (!protectedValue.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Value was not protected by the test protector.");
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(protectedValue[Prefix.Length..]));
    }
}
