using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using iM1os.Application.FinancialServices.Providers;

namespace iM1os.Infrastructure.FinancialServices.Providers;

public sealed class NmiValidationException : HttpRequestException
{
    private static readonly Regex FieldIdentifierPattern = new(
        @"\b(?:fld|col)_[a-z0-9_]{1,80}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex SafeCodePattern = new(
        @"^[a-z0-9][a-z0-9_.:-]{0,79}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex EmailPattern = new(
        @"\b[^\s@]+@[^\s@]+\.[^\s@]+\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex DatePattern = new(
        @"\b\d{4}-\d{2}-\d{2}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CredentialTermPattern = new(
        @"\b(?:secret|password|token|credential|authorization|bearer|private[ _-]?key|api[ _-]?key)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> FieldPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "field", "field_id", "fieldId", "attribute", "path"
    };
    private static readonly HashSet<string> CodePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "code", "error_code", "errorCode", "validation_code", "validationCode", "type"
    };
    private static readonly HashSet<string> DescriptionPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "message", "description", "detail"
    };

    public NmiValidationException(
        HttpStatusCode statusCode,
        IReadOnlyCollection<string> fieldIdentifiers,
        IReadOnlyCollection<string> validationCodes,
        IReadOnlyCollection<string> descriptions)
        : base(BuildMessage(statusCode, fieldIdentifiers, validationCodes, descriptions), null, statusCode)
    {
        FieldIdentifiers = fieldIdentifiers;
        ValidationCodes = validationCodes;
        Descriptions = descriptions;
    }

    public IReadOnlyCollection<string> FieldIdentifiers { get; }

    public IReadOnlyCollection<string> ValidationCodes { get; }

    public IReadOnlyCollection<string> Descriptions { get; }

    public static NmiValidationException FromResponse(
        HttpStatusCode statusCode,
        string responseJson,
        PartnerMerchantCreateRequest submittedRequest)
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var descriptions = new HashSet<string>(StringComparer.Ordinal);
        var submittedValues = SubmittedValues(submittedRequest);

        try
        {
            using var document = JsonDocument.Parse(responseJson);
            Collect(document.RootElement, null, false, submittedValues, fields, codes, descriptions);
        }
        catch (JsonException)
        {
            // Raw or malformed provider responses are intentionally discarded.
        }

        return new NmiValidationException(
            statusCode,
            fields.Order(StringComparer.OrdinalIgnoreCase).Take(10).ToArray(),
            codes.Order(StringComparer.OrdinalIgnoreCase).Take(10).ToArray(),
            descriptions.Take(10).ToArray());
    }

    private static void Collect(
        JsonElement element,
        string? propertyName,
        bool withinErrors,
        IReadOnlyCollection<string> submittedValues,
        ISet<string> fields,
        ISet<string> codes,
        ISet<string> descriptions)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    ExtractFieldIdentifiers(property.Name, fields);
                    var childWithinErrors = withinErrors ||
                        string.Equals(property.Name, "errors", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(property.Name, "validation_errors", StringComparison.OrdinalIgnoreCase);
                    Collect(
                        property.Value,
                        property.Name,
                        childWithinErrors,
                        submittedValues,
                        fields,
                        codes,
                        descriptions);
                }
                break;
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                {
                    Collect(child, propertyName, withinErrors, submittedValues, fields, codes, descriptions);
                }
                break;
            case JsonValueKind.String:
                var value = element.GetString();
                if (string.IsNullOrWhiteSpace(value))
                {
                    break;
                }

                ExtractFieldIdentifiers(value, fields);
                if ((propertyName is not null && FieldPropertyNames.Contains(propertyName)))
                {
                    ExtractFieldIdentifiers(value, fields);
                }

                if ((propertyName is not null && CodePropertyNames.Contains(propertyName)) ||
                    (withinErrors && SafeCodePattern.IsMatch(value)))
                {
                    AddCode(value, codes);
                }

                if (propertyName is not null && withinErrors &&
                    (DescriptionPropertyNames.Contains(propertyName) ||
                     string.Equals(propertyName, "error", StringComparison.OrdinalIgnoreCase)))
                {
                    if (SafeCodePattern.IsMatch(value))
                    {
                        AddCode(value, codes);
                    }
                    else
                    {
                        AddDescription(value, submittedValues, descriptions);
                    }
                }
                else if (withinErrors && !SafeCodePattern.IsMatch(value))
                {
                    AddDescription(value, submittedValues, descriptions);
                }
                break;
        }
    }

    private static void ExtractFieldIdentifiers(string value, ISet<string> fields)
    {
        foreach (Match match in FieldIdentifierPattern.Matches(value))
        {
            fields.Add(match.Value.ToLowerInvariant());
        }
    }

    private static void AddCode(string value, ISet<string> codes)
    {
        var clean = value.Trim();
        if (SafeCodePattern.IsMatch(clean) && !FieldIdentifierPattern.IsMatch(clean))
        {
            codes.Add(clean);
        }
    }

    private static void AddDescription(
        string value,
        IReadOnlyCollection<string> submittedValues,
        ISet<string> descriptions)
    {
        var clean = Regex.Replace(value, @"\s+", " ").Trim();
        if (clean.Length is 0 or > 240 ||
            clean.Any(char.IsControl) ||
            EmailPattern.IsMatch(clean) ||
            DatePattern.IsMatch(clean) ||
            CredentialTermPattern.IsMatch(clean) ||
            clean.Count(char.IsDigit) >= 7 ||
            submittedValues.Any(submitted => ContainsSubmittedValue(clean, submitted)))
        {
            return;
        }

        descriptions.Add(clean);
    }

    private static bool ContainsSubmittedValue(string description, string submittedValue)
    {
        if (submittedValue.Length <= 3)
        {
            return Regex.IsMatch(
                description,
                $@"(?<![a-z0-9]){Regex.Escape(submittedValue)}(?![a-z0-9])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return description.Contains(submittedValue, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyCollection<string> SubmittedValues(PartnerMerchantCreateRequest request)
    {
        return request.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.GetValue(request))
            .Where(value => value is not null)
            .Select(value => value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value!.ToString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildMessage(
        HttpStatusCode statusCode,
        IReadOnlyCollection<string> fieldIdentifiers,
        IReadOnlyCollection<string> validationCodes,
        IReadOnlyCollection<string> descriptions)
    {
        var parts = new List<string>
        {
            $"NMI validation failed (HTTP {(int)statusCode})."
        };
        if (fieldIdentifiers.Count > 0)
        {
            parts.Add($"Fields: {string.Join(", ", fieldIdentifiers)}.");
        }
        if (validationCodes.Count > 0)
        {
            parts.Add($"Codes: {string.Join(", ", validationCodes)}.");
        }
        if (descriptions.Count > 0)
        {
            parts.Add($"Details: {string.Join("; ", descriptions)}");
        }

        var message = string.Join(' ', parts);
        return message.Length <= 950 ? message : $"{message[..947]}...";
    }
}
