using System.Text.RegularExpressions;
using iM1os.Domain.GlobalCatalog;

namespace iM1os.Infrastructure.Services;

internal sealed record CatalogTireAttributes(
    int? Width,
    int? AspectRatio,
    int? RimDiameter,
    string? Position,
    string? Construction,
    string? Type,
    string? ModelLine);

internal static partial class CatalogTireParser
{
    private static readonly Dictionary<string, int> AlphaWidthMillimeters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MH"] = 80,
        ["MJ"] = 90,
        ["MK"] = 100,
        ["ML"] = 110,
        ["MM"] = 120,
        ["MN"] = 130,
        ["MP"] = 140,
        ["MR"] = 150,
        ["MT"] = 160,
        ["MU"] = 170,
        ["MV"] = 180
    };

    public static CatalogTireAttributes Parse(params string?[] values)
    {
        var text = string.Join(' ', values.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(text) || !LooksLikeTireText(text))
        {
            return new CatalogTireAttributes(null, null, null, null, null, null, null);
        }

        var normalized = Normalize(text);
        var size = ResolveSize(normalized);

        return new CatalogTireAttributes(
            size?.Width,
            size?.Ratio,
            size?.Rim,
            ResolvePosition(normalized),
            ResolveConstruction(normalized),
            ResolveType(normalized),
            ResolveModelLine(normalized));
    }

    public static void Apply(GlobalProduct product, params string?[] values)
    {
        var attributes = Parse(values);
        product.TireWidth = attributes.Width;
        product.TireAspectRatio = attributes.AspectRatio;
        product.TireRimDiameter = attributes.RimDiameter;
        product.TirePosition = attributes.Position;
        product.TireConstruction = attributes.Construction;
        product.TireType = attributes.Type;
        product.TireModelLine = attributes.ModelLine;
    }

    private static string Normalize(string value)
    {
        return value
            .Replace('\\', ' ')
            .Replace('/', '/')
            .Replace('_', ' ')
            .ToUpperInvariant();
    }

    private static bool LooksLikeTireText(string text)
    {
        return TireWordRegex().IsMatch(text) ||
            MetricTireSizeRegex().IsMatch(text) ||
            DecimalTireSizeRegex().IsMatch(text) ||
            AtvTireSizeRegex().IsMatch(text) ||
            AlphaTireSizeRegex().IsMatch(text);
    }

    private static int? ParseInt(string value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static CatalogTireSize? ResolveSize(string text)
    {
        foreach (Match match in ParenthesizedTextRegex().Matches(text))
        {
            var parsed = ResolveSizeCandidate(match.Groups["value"].Value);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        return ResolveSizeCandidate(text);
    }

    private static CatalogTireSize? ResolveSizeCandidate(string text)
    {
        foreach (Match match in MetricTireSizeRegex().Matches(text))
        {
            var size = new CatalogTireSize(
                ParseInt(match.Groups["width"].Value),
                ParseInt(match.Groups["ratio"].Value),
                ParseInt(match.Groups["rim"].Value));

            if (IsPlausibleMetricSize(size.Width, size.Ratio, size.Rim))
            {
                return size;
            }
        }

        foreach (Match match in DecimalTireSizeRegex().Matches(text))
        {
            if (!decimal.TryParse(match.Groups["width"].Value, out var widthInches))
            {
                continue;
            }

            var size = new CatalogTireSize(
                (int)Math.Round(widthInches * 25.4m, MidpointRounding.AwayFromZero),
                100,
                ParseInt(match.Groups["rim"].Value));

            if (IsPlausibleMetricSize(size.Width, size.Ratio, size.Rim))
            {
                return size;
            }
        }

        foreach (Match match in AtvTireSizeRegex().Matches(text))
        {
            if (!decimal.TryParse(match.Groups["height"].Value, out var height) ||
                !decimal.TryParse(match.Groups["width"].Value, out var width) ||
                !decimal.TryParse(match.Groups["rim"].Value, out var rim) ||
                width <= 0)
            {
                continue;
            }

            var ratio = (int)Math.Round(((height - rim) / (width * 2)) * 100, MidpointRounding.AwayFromZero);
            var size = new CatalogTireSize((int)Math.Round(width, MidpointRounding.AwayFromZero), ratio, (int)Math.Round(rim, MidpointRounding.AwayFromZero));
            if (size.Width is >= 4 and <= 16 && size.Ratio is >= 20 and <= 120 && size.Rim is >= 6 and <= 30)
            {
                return size;
            }
        }

        foreach (Match match in AlphaTireSizeRegex().Matches(text))
        {
            var alphaCode = match.Groups["alpha"].Value;
            if (!AlphaWidthMillimeters.TryGetValue(alphaCode, out var width))
            {
                continue;
            }

            var size = new CatalogTireSize(
                width,
                ParseInt(match.Groups["ratio"].Value),
                ParseInt(match.Groups["rim"].Value));

            if (IsPlausibleMetricSize(size.Width, size.Ratio, size.Rim))
            {
                return size;
            }
        }

        return null;
    }

    private static bool IsPlausibleMetricSize(int? width, int? ratio, int? rim)
    {
        return width is >= 40 and <= 260 &&
            ratio is >= 20 and <= 120 &&
            rim is >= 6 and <= 30;
    }

    private static string? ResolvePosition(string text)
    {
        var hasFront = FrontRegex().IsMatch(text);
        var hasRear = RearRegex().IsMatch(text);
        return hasFront && hasRear
            ? "front/rear"
            : hasFront
                ? "front"
                : hasRear
                    ? "rear"
                    : null;
    }

    private static string? ResolveConstruction(string text)
    {
        if (RadialRegex().IsMatch(text))
        {
            return "radial";
        }

        if (BiasRegex().IsMatch(text))
        {
            return "bias";
        }

        return null;
    }

    private static string? ResolveType(string text)
    {
        if (AtvRegex().IsMatch(text))
        {
            return "ATV/UTV";
        }

        if (StreetRegex().IsMatch(text))
        {
            return "street";
        }

        if (MxOffroadRegex().IsMatch(text))
        {
            return "MX/offroad";
        }

        return TireWordRegex().IsMatch(text) ? "tire" : null;
    }

    private static string? ResolveModelLine(string text)
    {
        foreach (Match match in ModelLineRegex().Matches(text))
        {
            var value = match.Value;
            if (LoadRatingRegex().IsMatch(value))
            {
                continue;
            }

            return value;
        }

        return null;
    }

    [GeneratedRegex(@"\((?<value>[^)]+)\)", RegexOptions.Compiled)]
    private static partial Regex ParenthesizedTextRegex();

    [GeneratedRegex(@"(?<!\d)(?<width>\d{2,3})\s*/\s*(?<ratio>\d{2,3})(?:[A-Z]{0,2})?\s*(?:R|-)?\s*(?<rim>\d{2})(?!\d)", RegexOptions.Compiled)]
    private static partial Regex MetricTireSizeRegex();

    [GeneratedRegex(@"(?<!\d)(?<width>\d+\.\d{2})\s*-\s*(?<rim>\d{2})(?!\d)", RegexOptions.Compiled)]
    private static partial Regex DecimalTireSizeRegex();

    [GeneratedRegex(@"(?<!\d)(?<height>\d{2,3})\s*X\s*(?<width>\d{1,2}(?:\.\d+)?)\s*(?:[A-Z]{0,2})?\s*(?:R|-)?\s*(?<rim>\d{2})(?!\d)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex AtvTireSizeRegex();

    [GeneratedRegex(@"\b(?<alpha>[A-Z]{1,3})(?<ratio>\d{2,3})(?:[A-Z])?-?(?<rim>\d{2})\b", RegexOptions.Compiled)]
    private static partial Regex AlphaTireSizeRegex();

    [GeneratedRegex(@"\b(TIRE|TYRE|GEOMAX|MX|MOTOCROSS|OFF[\s-]?ROAD|DUAL[\s-]?SPORT|STREET|ATV|UTV)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex TireWordRegex();

    [GeneratedRegex(@"\bFRONT\b|\bFR\b", RegexOptions.Compiled)]
    private static partial Regex FrontRegex();

    [GeneratedRegex(@"\bREAR\b|\bRR\b", RegexOptions.Compiled)]
    private static partial Regex RearRegex();

    [GeneratedRegex(@"\bRADIAL\b|\bZR\b|\bR(?=\d{2}\b)", RegexOptions.Compiled)]
    private static partial Regex RadialRegex();

    [GeneratedRegex(@"\bBIAS\b|\bBIAS-PLY\b|\bTT\b", RegexOptions.Compiled)]
    private static partial Regex BiasRegex();

    [GeneratedRegex(@"\bATV\b|\bUTV\b|\bSXS\b|\bSIDE BY SIDE\b", RegexOptions.Compiled)]
    private static partial Regex AtvRegex();

    [GeneratedRegex(@"\bSTREET\b|\bCRUISER\b|\bSPORTBIKE\b|\bTOURING\b|\bROAD\b", RegexOptions.Compiled)]
    private static partial Regex StreetRegex();

    [GeneratedRegex(@"\bMX\b|\bMOTOCROSS\b|\bOFF[\s-]?ROAD\b|\bGEOMAX\b|\bDUNLOP\b", RegexOptions.Compiled)]
    private static partial Regex MxOffroadRegex();

    [GeneratedRegex(@"\b[A-Z]{1,6}\d{2,4}[A-Z]?\b", RegexOptions.Compiled)]
    private static partial Regex ModelLineRegex();

    [GeneratedRegex(@"^\d{2,3}[A-Z]$", RegexOptions.Compiled)]
    private static partial Regex LoadRatingRegex();

    private sealed record CatalogTireSize(int? Width, int? Ratio, int? Rim);
}
