using System.Text.Json;

namespace CMS.API.Infrastructure;

/// <summary>
/// Reads well-known properties out of the <c>SysConfig</c> row whose <c>configKey</c> is <see cref="ConfigKey"/>.
/// The value is a JSON object, e.g. <c>{ "defaultPassword": "…", "symmetricSecurityKey": "…" }</c>.
/// </summary>
public static class AppConfigJson
{
    public const string ConfigKey = "appConfig";
    public const string DefaultPasswordProperty = "defaultPassword";
    public const string SymmetricSecurityKeyProperty = "symmetricSecurityKey";

    /// <summary>Returns the <c>defaultPassword</c> string, or throws <see cref="AppConfigException"/>.</summary>
    public static string ExtractDefaultPassword(string? configValue) =>
        ExtractString(configValue, DefaultPasswordProperty, "無法取得預設密碼");

    /// <summary>Returns the <c>symmetricSecurityKey</c> string (JWT signing secret), or throws <see cref="AppConfigException"/>.</summary>
    public static string ExtractSymmetricSecurityKey(string? configValue) =>
        ExtractString(configValue, SymmetricSecurityKeyProperty, "無法簽發登入權杖");

    /// <summary>
    /// Returns the named non-empty string property of the <c>appConfig</c> JSON object.
    /// Throws <see cref="AppConfigException"/> when the row is missing, the JSON is invalid, or the property is absent / not a string / empty.
    /// </summary>
    public static string ExtractString(string? configValue, string propertyName, string purpose)
    {
        if (string.IsNullOrWhiteSpace(configValue))
        {
            throw new AppConfigException($"SysConfig 缺少「{ConfigKey}」設定，{purpose}。");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(configValue);
        }
        catch (JsonException ex)
        {
            throw new AppConfigException($"SysConfig「{ConfigKey}」不是有效的 JSON。", ex);
        }

        using (document)
        {
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
        }

        throw new AppConfigException($"SysConfig「{ConfigKey}」缺少「{propertyName}」，{purpose}。");
    }
}
