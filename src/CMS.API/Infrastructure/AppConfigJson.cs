using System.Text.Json;

namespace CMS.API.Infrastructure;

/// <summary>
/// Reads well-known properties out of the <c>SysConfig</c> row whose <c>configKey</c> is <see cref="ConfigKey"/>.
/// The value is a JSON object, e.g. <c>{ "defaultPassword": "…" }</c>.
/// </summary>
public static class AppConfigJson
{
    public const string ConfigKey = "appConfig";
    public const string DefaultPasswordProperty = "defaultPassword";

    /// <summary>Returns the <c>defaultPassword</c> string, or throws <see cref="AppConfigException"/>.</summary>
    public static string ExtractDefaultPassword(string? configValue)
    {
        if (string.IsNullOrWhiteSpace(configValue))
        {
            throw new AppConfigException($"SysConfig 缺少「{ConfigKey}」設定，無法取得預設密碼。");
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
                && document.RootElement.TryGetProperty(DefaultPasswordProperty, out var property)
                && property.ValueKind == JsonValueKind.String)
            {
                var password = property.GetString();
                if (!string.IsNullOrEmpty(password))
                {
                    return password;
                }
            }
        }

        throw new AppConfigException($"SysConfig「{ConfigKey}」缺少「{DefaultPasswordProperty}」。");
    }
}
