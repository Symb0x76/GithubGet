using System.Security.Cryptography;
using System.Text;

namespace GithubGet.App.Services;

public static class TokenProtector
{
    public static string? Protect(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var bytes = Encoding.UTF8.GetBytes(token);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public static string? Unprotect(string? protectedToken)
    {
        if (string.IsNullOrWhiteSpace(protectedToken))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(protectedToken);
            var unprotected = ProtectedData.Unprotect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(unprotected);
        }
        catch
        {
            return null;
        }
    }
}
