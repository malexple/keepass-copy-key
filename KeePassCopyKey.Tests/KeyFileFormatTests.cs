using System;
using System.IO;
using System.Security.Cryptography;
using KeePassCopyKey.IO;
using Xunit;

namespace KeePassCopyKey.Tests;

public class KeyFileFormatTests : IDisposable
{
    private readonly string _tempPath = Path.Combine(Path.GetTempPath(), $"kck-test-{Guid.NewGuid():N}.kck");

    public void Dispose()
    {
        if (File.Exists(_tempPath)) File.Delete(_tempPath);
    }

    private static readonly KeyEntryRecord[] SampleEntries =
    {
        new("GitHub", "simplecomplex", "hunter2", "https://github.com", "personal account"),
        new("Empty fields", "", "", "", ""),
        new("Юникод", "пользователь", "пароль123", "https://пример.рф", "заметка"),
    };

    [Fact]
    public void WriteRead_RoundTripsPayload()
    {
        byte[] payload = KeyFile.SerializeEntries(SampleEntries);

        KeyFile.Write(_tempPath, payload, "correct horse battery staple");
        byte[] readBack = KeyFile.Read(_tempPath, "correct horse battery staple");

        Assert.Equal(payload, readBack);
    }

    [Fact]
    public void Write_EmptyPassword_Throws()
    {
        byte[] payload = KeyFile.SerializeEntries(SampleEntries);
        Assert.Throws<ArgumentException>(() => KeyFile.Write(_tempPath, payload, ""));
    }

    [Fact]
    public void Write_NullPassword_Throws()
    {
        byte[] payload = KeyFile.SerializeEntries(SampleEntries);
        Assert.Throws<ArgumentException>(() => KeyFile.Write(_tempPath, payload, null!));
    }

    [Fact]
    public void Read_WrongPassword_ThrowsCryptographicException()
    {
        byte[] payload = KeyFile.SerializeEntries(SampleEntries);
        KeyFile.Write(_tempPath, payload, "correct password");

        Assert.Throws<CryptographicException>(() => KeyFile.Read(_tempPath, "wrong password"));
    }

    [Fact]
    public void Read_CorruptedMagic_ThrowsInvalidDataException()
    {
        File.WriteAllBytes(_tempPath, new byte[] { 1, 2, 3, 4, 5 });

        Assert.Throws<InvalidDataException>(() => KeyFile.Read(_tempPath, "any password"));
    }

    [Fact]
    public void Read_OldUnencryptedFormatMagic_ThrowsInvalidDataException()
    {
        // "KCK1" was the old, plaintext-capable magic - must not be
        // silently accepted by the new reader.
        File.WriteAllBytes(_tempPath, System.Text.Encoding.ASCII.GetBytes("KCK1"));

        Assert.Throws<InvalidDataException>(() => KeyFile.Read(_tempPath, "any password"));
    }

    [Fact]
    public void SerializeDeserializeEntries_RoundTripsAllFields()
    {
        byte[] payload = KeyFile.SerializeEntries(SampleEntries);
        KeyEntryRecord[] result = KeyFile.DeserializeEntries(payload);

        Assert.Equal(SampleEntries, result);
    }

    [Fact]
    public void SerializeDeserializeEntries_EmptyArray_RoundTrips()
    {
        byte[] payload = KeyFile.SerializeEntries(Array.Empty<KeyEntryRecord>());
        KeyEntryRecord[] result = KeyFile.DeserializeEntries(payload);

        Assert.Empty(result);
    }
}