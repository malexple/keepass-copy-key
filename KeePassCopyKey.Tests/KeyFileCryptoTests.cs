using System.Linq;
using KeePassCopyKey.Crypto;
using Xunit;

namespace KeePassCopyKey.Tests;

public class KeyFileCryptoTests
{
    [Fact]
    public void DeriveKey_SamePasswordSaltIterations_IsDeterministic()
    {
        byte[] salt = KeyFileCrypto.RandomBytes(KeyFileCrypto.SaltLength);

        byte[] key1 = KeyFileCrypto.DeriveKey("correct horse battery staple", salt, 10_000);
        byte[] key2 = KeyFileCrypto.DeriveKey("correct horse battery staple", salt, 10_000);

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void DeriveKey_DifferentSalt_ProducesDifferentKey()
    {
        byte[] saltA = KeyFileCrypto.RandomBytes(KeyFileCrypto.SaltLength);
        byte[] saltB = KeyFileCrypto.RandomBytes(KeyFileCrypto.SaltLength);

        byte[] keyA = KeyFileCrypto.DeriveKey("same password", saltA, 10_000);
        byte[] keyB = KeyFileCrypto.DeriveKey("same password", saltB, 10_000);

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void DeriveKey_ReturnsExpectedLength()
    {
        byte[] salt = KeyFileCrypto.RandomBytes(KeyFileCrypto.SaltLength);
        byte[] key = KeyFileCrypto.DeriveKey("password", salt, 10_000);

        Assert.Equal(KeyFileCrypto.KeyLength, key.Length);
    }

    [Fact]
    public void RandomBytes_TwoCalls_AreNotEqual()
    {
        byte[] a = KeyFileCrypto.RandomBytes(32);
        byte[] b = KeyFileCrypto.RandomBytes(32);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Wipe_ZeroesBuffer()
    {
        byte[] buffer = KeyFileCrypto.RandomBytes(16);
        KeyFileCrypto.Wipe(buffer);

        Assert.All(buffer, b => Assert.Equal(0, b));
    }

    [Fact]
    public void GeneratePassword_DefaultShape_IsFourGroupsOfFour()
    {
        string password = KeyFileCrypto.GeneratePassword();
        string[] groups = password.Split('-');

        Assert.Equal(4, groups.Length);
        Assert.All(groups, g => Assert.Equal(4, g.Length));
    }

    [Fact]
    public void GeneratePassword_UsesOnlyUnambiguousCharacters()
    {
        string password = KeyFileCrypto.GeneratePassword();
        Assert.DoesNotContain(password.Where(c => c != '-'), c => "0O1IL".Contains(c));
    }

    [Fact]
    public void GeneratePassword_TwoCalls_AreNotEqual()
    {
        string a = KeyFileCrypto.GeneratePassword();
        string b = KeyFileCrypto.GeneratePassword();

        Assert.NotEqual(a, b);
    }
}