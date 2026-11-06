using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WorkAgent.Storage;

public static class CredentialStore
{
    private static string AppDataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorkAgent");

    private static string CredentialFilePath => Path.Combine(AppDataFolder, "cred.bin");
    private static string DeviceUuidFilePath => Path.Combine(AppDataFolder, "device.id");

    public static void SaveTokens(string refreshToken)
    {
        Directory.CreateDirectory(AppDataFolder);
        byte[] plainBytes = Encoding.UTF8.GetBytes(refreshToken);
        byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(CredentialFilePath, encryptedBytes);
    }

    public static string? LoadRefreshToken()
    {
        if (!File.Exists(CredentialFilePath)) return null;
        try
        {
            byte[] encryptedBytes = File.ReadAllBytes(CredentialFilePath);
            byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return null;
        }
    }

    public static void ClearCredentials()
    {
        if (File.Exists(CredentialFilePath))
        {
            File.Delete(CredentialFilePath);
        }
    }

    public static Guid GetOrCreateDeviceUuid()
    {
        Directory.CreateDirectory(AppDataFolder);
        if (File.Exists(DeviceUuidFilePath))
        {
            string text = File.ReadAllText(DeviceUuidFilePath).Trim();
            if (Guid.TryParse(text, out var existingGuid))
            {
                return existingGuid;
            }
        }

        var newGuid = Guid.NewGuid();
        File.WriteAllText(DeviceUuidFilePath, newGuid.ToString());
        return newGuid;
    }
}
