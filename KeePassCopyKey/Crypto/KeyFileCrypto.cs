// Thin, self-contained password-based key derivation for KeyFile (see
// IO/KeyFile.cs). Also owns random-password generation, used whenever
// the user leaves the password field blank on export - see
// UI/PasswordDialog.cs and KeePassCopyKeyExt.cs.RunExport.

using System;
using System.Security.Cryptography;
using System.Text;

namespace KeePassCopyKey.Crypto;

internal static class KeyFileCrypto
{
    public const int SaltLength = 16;
    public const int KeyLength = 32;
    public const int DefaultIterations = 400_000;

    // Crockford Base32 - excludes 0/O/1/I/L to avoid ambiguity when read
    // aloud or typed by hand. Four groups of four = 16 chars from a
    // 32-symbol alphabet = 80 bits of raw entropy.
    private const string PasswordAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public static byte[] DeriveKey(string password, byte[] salt, int iterations)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeyLength);
    }

    public static byte[] RandomBytes(int length)
    {
        var buffer = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(buffer);
        return buffer;
    }

    public static string GeneratePassword(int groups = 4, int groupLength = 4)
    {
        var sb = new StringBuilder();
        for (int g = 0; g < groups; g++)
        {
            if (g > 0) sb.Append('-');
            for (int i = 0; i < groupLength; i++)
            {
                byte[] b = RandomBytes(1);
                sb.Append(PasswordAlphabet[b[0] % PasswordAlphabet.Length]);
            }
        }
        return sb.ToString();
    }

    // net48 has no CryptographicOperations.ZeroMemory (.NET Core 3.0+) -
    // a plain overwrite is enough here; this isn't defending against a
    // memory-inspecting adversary, just avoiding leaving key bytes in a
    // GC'd buffer longer than necessary.
    public static void Wipe(byte[] buffer) => Array.Clear(buffer, 0, buffer.Length);
}