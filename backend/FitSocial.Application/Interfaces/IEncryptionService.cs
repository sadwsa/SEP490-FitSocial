namespace FitSocial.Application.Interfaces;

public interface IEncryptionService
{
    byte[] Encrypt(string plainText, bool deterministic = false);

    string? Decrypt(byte[]? cipherBytes);

    byte[] EncryptBytes(byte[] data, bool deterministic = false);

    byte[]? DecryptBytes(byte[]? cipherData);
}
