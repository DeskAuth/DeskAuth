using System.Text;
using DeskAuth.Core;

namespace DeskAuth.Core.Tests;

public class SecretProtectorTests
{
    [Fact]
    public void DecryptsLegacyPasswordSequence()
    {
        string encrypted = Fixtures.ReadText("legacy-sequence.txt");
        string plain = SecretProtector.Decrypt(encrypted, ProtectionTypes.Password, "password123");
        Assert.Equal("hello world", Encoding.UTF8.GetString(Convert.FromHexString(plain)));
    }
}
