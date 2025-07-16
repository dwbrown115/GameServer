using System.Security.Cryptography;

namespace GameServer.Utilities;

public static class EncryptionUtility
{
    public static byte[] GenerateEncryptionKey()
    {
        return RandomNumberGenerator.GetBytes(32); // AES 256-bit key
    }

    public static string GenerateSecureToken()
    {
        return Guid.NewGuid().ToString("N"); // 32-char hex string
    }
    
    public static string Encrypt(string plainText, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        aes.Padding = PaddingMode.PKCS7;
        
        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length); // prepend IV

        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        using var sw = new StreamWriter(cs);
        sw.Write(plainText);
        sw.Flush();              // ✅ Flush writer
        cs.FlushFinalBlock();    // ✅ Finalize encryption

        return Convert.ToBase64String(ms.ToArray());
    }

    public static string Decrypt(string cipherText, byte[] key)
    {
        var fullCipher = Convert.FromBase64String(cipherText);
        using var aes = Aes.Create();
        aes.Key = key;

        var iv = new byte[aes.BlockSize / 8];
        Array.Copy(fullCipher, iv, iv.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        return sr.ReadToEnd();
    }
}
