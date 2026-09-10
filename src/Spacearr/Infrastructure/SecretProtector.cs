using System.Security.Cryptography;
using System.Text;

namespace Spacearr.Infrastructure;

public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}

public sealed class SecretProtector : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private readonly byte[] _key;

    public SecretProtector(ConfigPaths paths)
    {
        _key = LoadOrCreateKey(paths.SecretKeyPath);
    }

    private static byte[] LoadOrCreateKey(string path)
    {
        if (File.Exists(path))
        {
            var existing = File.ReadAllBytes(path);
            if (existing.Length == KeySize) return existing;
            throw new InvalidOperationException($"Secret key file {path} is corrupt (expected {KeySize} bytes).");
        }
        var key = RandomNumberGenerator.GetBytes(KeySize);
        File.WriteAllBytes(path, key);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        return key;
    }

    public string Protect(string plaintext)
    {
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);
        var output = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(output, 0);
        tag.CopyTo(output, NonceSize);
        cipher.CopyTo(output, NonceSize + TagSize);
        return Convert.ToBase64String(output);
    }

    public string Unprotect(string ciphertext)
    {
        var input = Convert.FromBase64String(ciphertext);
        var nonce = input.AsSpan(0, NonceSize);
        var tag = input.AsSpan(NonceSize, TagSize);
        var cipher = input.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
