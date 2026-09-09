using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WindowsDevApp.Services;

/// <summary>
/// Stores the GitHub token encrypted at rest with Windows DPAPI, scoped to the
/// current Windows user account. Only readable by the same user on the same machine.
/// </summary>
public static class SecretStore
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsDevApp");
    private static readonly string FilePath = Path.Combine(Dir, "github.token");
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WindowsDevApp.GitHub.Token.v1");

    public static void SaveToken(string token)
    {
        Directory.CreateDirectory(Dir);
        var plainBytes = Encoding.UTF8.GetBytes(token);
        var encrypted = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath, encrypted);
    }

    public static string? LoadToken()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var encrypted = File.ReadAllBytes(FilePath);
            var plainBytes = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return null;
        }
    }

    public static void ClearToken()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch
        {
            // best effort
        }
    }
}
