using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FitSocial.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FitSocial.Infrastructure.Services;

public class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    public AesEncryptionService(IConfiguration configuration)
    {
        var configuredKey = configuration["Encryption:Key"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            configuredKey = "FitSocial_Default_Secret_Encryption_Key_2026_Secure!";
        }

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
    }

    public byte[] Encrypt(string plainText, bool deterministic = false)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return Array.Empty<byte>();
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        return EncryptBytes(plainBytes, deterministic);
    }

    public string? Decrypt(byte[]? cipherBytes)
    {
        if (cipherBytes == null || cipherBytes.Length == 0)
        {
            return null;
        }

        var decryptedBytes = DecryptBytes(cipherBytes);
        if (decryptedBytes == null || decryptedBytes.Length == 0)
        {
            return null;
        }

        return Encoding.UTF8.GetString(decryptedBytes);
    }

    public byte[] EncryptBytes(byte[] data, bool deterministic = false)
    {
        if (data == null || data.Length == 0)
        {
            return Array.Empty<byte>();
        }

        byte[] iv;
        if (deterministic)
        {
            // Deterministic IV: HMAC of the plaintext using the key ensures identical output for identical input
            using var hmac = new HMACSHA256(_key);
            var hash = hmac.ComputeHash(data);
            iv = new byte[16];
            Array.Copy(hash, iv, 16);
        }
        else
        {
            iv = RandomNumberGenerator.GetBytes(16);
        }

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var ms = new MemoryStream();
        // Write IV first
        ms.Write(iv, 0, iv.Length);

        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            cs.Write(data, 0, data.Length);
            cs.FlushFinalBlock();
        }

        return ms.ToArray();
    }

    public byte[]? DecryptBytes(byte[]? cipherData)
    {
        if (cipherData == null || cipherData.Length <= 16)
        {
            return null;
        }

        try
        {
            var iv = new byte[16];
            Array.Copy(cipherData, 0, iv, 0, 16);

            var cipherPayloadLength = cipherData.Length - 16;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var msInput = new MemoryStream(cipherData, 16, cipherPayloadLength);
            using var msOutput = new MemoryStream();
            using var cs = new CryptoStream(msInput, aes.CreateDecryptor(), CryptoStreamMode.Read);
            cs.CopyTo(msOutput);

            return msOutput.ToArray();
        }
        catch
        {
            // Return null if ciphertext is corrupted or decryption fails
            return null;
        }
    }
}
