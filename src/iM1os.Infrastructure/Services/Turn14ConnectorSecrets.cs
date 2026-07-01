using System.Text.Json;
using iM1os.Domain.GlobalCatalog;

namespace iM1os.Infrastructure.Services;

internal sealed record Turn14ConnectorSecrets(
    string? WebUsername,
    string? WebPassword,
    string? ApiClientId,
    string? ApiClientSecret)
{
    public bool HasWebCredentials => !string.IsNullOrWhiteSpace(WebUsername) && !string.IsNullOrWhiteSpace(WebPassword);

    public bool HasApiCredentials => !string.IsNullOrWhiteSpace(ApiClientId) && !string.IsNullOrWhiteSpace(ApiClientSecret);

    public static Turn14ConnectorSecrets FromConfiguration(SupplierConnectorConfiguration configuration)
    {
        var stored = StoredTurn14Secrets.FromJson(configuration.ApiSecretProtected);
        var webUsername = Clean(Environment.GetEnvironmentVariable("TURN14_USERNAME")) ?? Clean(configuration.Username);
        var webPassword = Clean(Environment.GetEnvironmentVariable("TURN14_PASSWORD")) ?? Clean(stored.WebPassword);
        var apiClientId = Clean(Environment.GetEnvironmentVariable("TURN14_CLIENT_ID")) ?? Clean(configuration.ApiKey);
        var apiClientSecret = Clean(Environment.GetEnvironmentVariable("TURN14_CLIENT_SECRET")) ?? Clean(stored.ApiClientSecret);

        if (stored.IsLegacyPlainSecret && webPassword is null && apiClientSecret is null)
        {
            webPassword = Clean(configuration.ApiSecretProtected);
        }

        return new Turn14ConnectorSecrets(webUsername, webPassword, apiClientId, apiClientSecret);
    }

    public static Turn14ConnectorSecrets FromStoredConfiguration(SupplierConnectorConfiguration configuration)
    {
        var stored = StoredTurn14Secrets.FromJson(configuration.ApiSecretProtected);
        var webPassword = Clean(stored.WebPassword);
        var apiClientSecret = Clean(stored.ApiClientSecret);

        if (stored.IsLegacyPlainSecret && webPassword is null && apiClientSecret is null)
        {
            webPassword = Clean(configuration.ApiSecretProtected);
        }

        return new Turn14ConnectorSecrets(
            Clean(configuration.Username),
            webPassword,
            Clean(configuration.ApiKey),
            apiClientSecret);
    }

    public static string MergeSecretJson(string? existingSecretJson, string? webPassword, string? apiClientSecret)
    {
        var existing = StoredTurn14Secrets.FromJson(existingSecretJson);
        var merged = new StoredTurn14Secrets(
            Clean(webPassword) ?? existing.WebPassword,
            Clean(apiClientSecret) ?? existing.ApiClientSecret,
            IsLegacyPlainSecret: false);

        return JsonSerializer.Serialize(new
        {
            webPassword = merged.WebPassword,
            apiClientSecret = merged.ApiClientSecret
        });
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record StoredTurn14Secrets(string? WebPassword, string? ApiClientSecret, bool IsLegacyPlainSecret)
    {
        public static StoredTurn14Secrets FromJson(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new StoredTurn14Secrets(null, null, false);
            }

            try
            {
                using var document = JsonDocument.Parse(value);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return new StoredTurn14Secrets(null, null, true);
                }

                return new StoredTurn14Secrets(
                    ReadString(document.RootElement, "webPassword"),
                    ReadString(document.RootElement, "apiClientSecret"),
                    false);
            }
            catch (JsonException)
            {
                return new StoredTurn14Secrets(null, null, true);
            }
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? Clean(value.GetString())
                : null;
        }
    }
}
