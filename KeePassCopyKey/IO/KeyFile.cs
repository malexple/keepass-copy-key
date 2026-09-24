// The .kck file format and its (de)serialization. Works with the plain
// KeyEntryRecord type, not KeePassLib's PwEntry - see
// KeePassCopyKeyExt.cs for the PwEntry <-> KeyEntryRecord mapping.
//
// File format (little-endian) - ALWAYS encrypted, no plaintext escape
// hatch:
//
//   4 bytes   magic "KCK4"
//   1 byte    format version (1)
//   16 bytes  PBKDF2 salt
//   4 bytes   PBKDF2 iteration count (Int32)
//   24 bytes  XSalsa20Poly1305 nonce
//   N bytes   ciphertext + 16-byte Poly1305 tag
//
// Payload (before encryption):
//   4 bytes   entry count (Int32)
//   per entry, 5x [4-byte UTF8 byte length][UTF8 bytes]:
//     title, username, password, url, notes
//
// This is the fourth revision of this format's password semantics
// (KCK1: optional password with a "do not encrypt" checkbox; KCK2:
// mandatory password, silently auto-generated if left blank; KCK3:
// optional password, blank meant plaintext). All three earlier designs
// turned out to have a real problem: KCK1's checkbox could be forgotten,
// KCK2 silently replaced a would-be-empty password behind the user's
// back, and KCK3's "no password, but still encrypted" turned out to be
// cryptographically impossible without either a hardcoded key (fake
// security) or asymmetric encryption (a much bigger feature - see
// project discussion). The actual fix is enforcing a non-empty password
// at the UI layer (see UI/PasswordDialog.cs, which now blocks OK on an
// empty field instead of silently doing something on the user's behalf)
// - KeyFile itself just refuses to write/read without one, as a
// structural backstop regardless of what the UI does.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using KeePassCopyKey.Crypto;

namespace KeePassCopyKey.IO;

internal readonly record struct KeyEntryRecord(string Title, string UserName, string Password, string Url, string Notes);

internal static class KeyFile
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("KCK4");
    private const byte FormatVersion = 1;
    private const int NonceLength = 24;

    public static void Write(string path, byte[] payload, string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("password must not be empty", nameof(password));

        byte[] salt = KeyFileCrypto.RandomBytes(KeyFileCrypto.SaltLength);
        int iterations = KeyFileCrypto.DefaultIterations;
        byte[] nonce = KeyFileCrypto.RandomBytes(NonceLength);
        byte[] key = KeyFileCrypto.DeriveKey(password, salt, iterations);

        byte[] ciphertext;
        try
        {
            ciphertext = Chaos.NaCl.XSalsa20Poly1305.Encrypt(payload, key, nonce);
        }
        finally
        {
            KeyFileCrypto.Wipe(key);
        }

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);
        bw.Write(Magic);
        bw.Write(FormatVersion);
        bw.Write(salt);
        bw.Write(iterations);
        bw.Write(nonce);
        bw.Write(ciphertext);
    }

    public static byte[] Read(string path, string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("password must not be empty", nameof(password));

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);

        byte[] magic = br.ReadBytes(Magic.Length);
        if (!MagicMatches(magic))
            throw new InvalidDataException("not a keepass-copy-key file (or an older/incompatible format)");

        byte version = br.ReadByte();
        if (version != FormatVersion)
            throw new InvalidDataException($"unsupported keepass-copy-key file version: {version}");

        byte[] salt = br.ReadBytes(KeyFileCrypto.SaltLength);
        int iterations = br.ReadInt32();
        byte[] nonce = br.ReadBytes(NonceLength);
        byte[] ciphertext = br.ReadBytes((int)(fs.Length - fs.Position));

        byte[] key = KeyFileCrypto.DeriveKey(password, salt, iterations);
        try
        {
            byte[]? plaintext = Chaos.NaCl.XSalsa20Poly1305.TryDecrypt(ciphertext, key, nonce);
            if (plaintext is null)
                throw new CryptographicException("wrong password or corrupted file");
            return plaintext;
        }
        finally
        {
            KeyFileCrypto.Wipe(key);
        }
    }

    public static byte[] SerializeEntries(KeyEntryRecord[] entries)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(entries.Length);
        foreach (var entry in entries)
        {
            WriteString(bw, entry.Title);
            WriteString(bw, entry.UserName);
            WriteString(bw, entry.Password);
            WriteString(bw, entry.Url);
            WriteString(bw, entry.Notes);
        }
        return ms.ToArray();
    }

    public static KeyEntryRecord[] DeserializeEntries(byte[] payload)
    {
        using var ms = new MemoryStream(payload);
        using var br = new BinaryReader(ms);
        int count = br.ReadInt32();
        var result = new KeyEntryRecord[count];

        for (int i = 0; i < count; i++)
        {
            result[i] = new KeyEntryRecord(
                ReadString(br),
                ReadString(br),
                ReadString(br),
                ReadString(br),
                ReadString(br));
        }

        return result;
    }

    private static void WriteString(BinaryWriter bw, string s)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(s ?? string.Empty);
        bw.Write(bytes.Length);
        bw.Write(bytes);
    }

    private static string ReadString(BinaryReader br)
    {
        int length = br.ReadInt32();
        byte[] bytes = br.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    private static bool MagicMatches(byte[] candidate)
    {
        if (candidate.Length != Magic.Length) return false;
        for (int i = 0; i < Magic.Length; i++)
            if (candidate[i] != Magic[i]) return false;
        return true;
    }
}