using CMS.API.Infrastructure;

namespace CMS.API.Tests.Infrastructure;

public class AppConfigJsonTests
{
    [Fact]
    public void ExtractDefaultPassword_ReturnsTheProperty()
    {
        const string json = """{ "siteName": "CMS", "defaultPassword": "Welcome123!", "other": 1 }""";

        Assert.Equal("Welcome123!", AppConfigJson.ExtractDefaultPassword(json));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractDefaultPassword_Throws_WhenConfigRowMissing(string? json)
    {
        var ex = Assert.Throws<AppConfigException>(() => AppConfigJson.ExtractDefaultPassword(json));

        Assert.Contains("appConfig", ex.Message);
    }

    [Fact]
    public void ExtractDefaultPassword_Throws_OnInvalidJson()
    {
        var ex = Assert.Throws<AppConfigException>(() => AppConfigJson.ExtractDefaultPassword("{ not json"));

        Assert.Contains("JSON", ex.Message);
    }

    [Theory]
    [InlineData("""{ "siteName": "CMS" }""")]
    [InlineData("""{ "defaultPassword": "" }""")]
    [InlineData("""{ "defaultPassword": 123 }""")]
    [InlineData("""[ "defaultPassword" ]""")]
    public void ExtractDefaultPassword_Throws_WhenPropertyMissingOrNotAString(string json)
    {
        var ex = Assert.Throws<AppConfigException>(() => AppConfigJson.ExtractDefaultPassword(json));

        Assert.Contains("defaultPassword", ex.Message);
    }

    // ---- symmetricSecurityKey (JWT signing secret) ----

    [Fact]
    public void ExtractSymmetricSecurityKey_ReturnsTheProperty()
    {
        const string json = """{ "defaultPassword": "Welcome123!", "symmetricSecurityKey": "s3cr3t-signing-key" }""";

        Assert.Equal("s3cr3t-signing-key", AppConfigJson.ExtractSymmetricSecurityKey(json));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ExtractSymmetricSecurityKey_Throws_WhenConfigRowMissing(string? json)
    {
        var ex = Assert.Throws<AppConfigException>(() => AppConfigJson.ExtractSymmetricSecurityKey(json));

        Assert.Contains("appConfig", ex.Message);
    }

    [Theory]
    [InlineData("""{ "defaultPassword": "Welcome123!" }""")]
    [InlineData("""{ "symmetricSecurityKey": "" }""")]
    [InlineData("""{ "symmetricSecurityKey": 42 }""")]
    public void ExtractSymmetricSecurityKey_Throws_WhenPropertyMissingOrNotAString(string json)
    {
        var ex = Assert.Throws<AppConfigException>(() => AppConfigJson.ExtractSymmetricSecurityKey(json));

        Assert.Contains("symmetricSecurityKey", ex.Message);
        Assert.DoesNotContain("defaultPassword", ex.Message);
    }
}
