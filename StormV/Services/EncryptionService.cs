using System.Security.Cryptography;

namespace StormV.Services;

/// <summary>
/// Шифрует данные через Windows DPAPI.
/// Ключ привязан к учётной записи пользователя и машине —
/// расшифровать можно только на том же PC тем же пользователем.
/// </summary>
public static class EncryptionService
{
    // Дополнительная энтропия — усиливает защиту DPAPI
    private static readonly byte[] _entropy =
        [0x53, 0x74, 0x6F, 0x72, 0x6D, 0x56, 0x5F, 0x53, 0x65, 0x63];

    public static string Encrypt(string plainText)
    {
        var data = System.Text.Encoding.UTF8.GetBytes(plainText);
        var encrypted = ProtectedData.Protect(data, _entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    public static string Decrypt(string cipherBase64)
    {
        var data = Convert.FromBase64String(cipherBase64);
        var decrypted = ProtectedData.Unprotect(data, _entropy, DataProtectionScope.CurrentUser);
        return System.Text.Encoding.UTF8.GetString(decrypted);
    }

    /// <summary>
    /// Маскирует IP-адрес: 185.199.108.153 → 185.***.***.***, uuid → 8a3f****-****
    /// </summary>
    public static string MaskSensitive(string text)
    {
        // IPv4
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"\b(\d{1,3})\.\d{1,3}\.\d{1,3}\.\d{1,3}\b",
            m => $"{m.Groups[1].Value}.***.***.*"
        );

        // IPv6 (упрощённо)
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"\b([0-9a-fA-F]{1,4}):([0-9a-fA-F:]{3,})\b",
            m => $"{m.Groups[1].Value}:****:****"
        );

        // UUID
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"\b([0-9a-fA-F]{4})[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
            m => $"{m.Groups[1].Value}****-****-****-****-**********"
        );

        return text;
    }
}
