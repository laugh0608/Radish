using System;
using System.Text.Json;
using Radish.Model;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Contracts;

public sealed class UserCredentialProjectionTest
{
    [Fact]
    public void UserVo_ShouldNotExposeLoginPassword()
    {
        Assert.DoesNotContain(
            typeof(UserVo).GetProperties(),
            property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UserCredentialSnapshot_ShouldIgnorePasswordHashDuringSerialization()
    {
        var credential = new UserCredentialSnapshot(
            1001,
            "Tester",
            "Tester#1000",
            "tester@radish.test",
            "argon2id-secret-hash",
            0);

        var json = JsonSerializer.Serialize(credential);

        Assert.DoesNotContain("PasswordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("argon2id-secret-hash", json, StringComparison.Ordinal);
    }
}
