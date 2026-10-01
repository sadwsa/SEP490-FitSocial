namespace FitSocial.Application.Interfaces;

public interface IEncryptionService
{
    /// <summary>
    /// Encrypts a string into ciphertext bytes.
    /// If deterministic is true, the same plainText produces identical bytes (used for DB lookup).
    /// </summary>
    byte[] Encrypt(string plainText, bool deterministic = false);

    /// <summary>
    /// Decrypts ciphertext bytes back into the original plainText string.
    /// </summary>
    string? Decrypt(byte[]? cipherBytes);

    /// <summary>
    /// Encrypts raw bytes.
    /// </summary>
    byte[] EncryptBytes(byte[] data, bool deterministic = false);

    /// <summary>
    /// Decrypts raw bytes.
    /// </summary>
    byte[]? DecryptBytes(byte[]? cipherData);
}
