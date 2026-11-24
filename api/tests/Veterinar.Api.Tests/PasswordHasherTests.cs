using Veterinar.Infrastructure.Security;

namespace Veterinar.Api.Tests;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Missing_Identity_Never_Verifies_And_Current_Hashes_Still_Work()
    {
        var hasher = new PasswordHasher();
        Assert.False(hasher.Verify("wrong-password", null));
        var hash = hasher.Hash("test-password");
        Assert.StartsWith("pbkdf2-sha256$210000$", hash);
        Assert.True(hasher.Verify("test-password", hash));
        Assert.False(hasher.Verify("wrong-password", hash));
    }
}
