using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NifValidatorPortable;

public static class IsoCountryCatalog
{
    private static readonly Lazy<IReadOnlyList<CountryOption>> CountriesLazy = new(CreateCountries);
    private static readonly Lazy<HashSet<string>> CountryCodesLazy = new(CreateCountryCodes);

    public static IReadOnlyList<CountryOption> Countries => CountriesLazy.Value;

    public static bool IsValidCountryCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return false;
        }

        var normalized = NormalizeCountryCode(countryCode);
        return normalized == "EL" || CountryCodesLazy.Value.Contains(normalized);
    }

    public static string NormalizeCountryCode(string? countryCode)
    {
        return string.IsNullOrWhiteSpace(countryCode)
            ? string.Empty
            : countryCode.Trim().ToUpperInvariant();
    }

    private static IReadOnlyList<CountryOption> CreateCountries()
    {
        return CultureInfo
            .GetCultures(CultureTypes.SpecificCultures)
            .Select(culture =>
            {
                try
                {
                    return new RegionInfo(culture.Name);
                }
                catch
                {
                    return null;
                }
            })
            .Where(region => region is not null)
            .GroupBy(region => region!.TwoLetterISORegionName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()!)
            .Where(region =>
                region.TwoLetterISORegionName.Length == 2 &&
                region.TwoLetterISORegionName.All(char.IsLetter))
            .OrderBy(region => region.EnglishName, StringComparer.CurrentCultureIgnoreCase)
            .Select(region => new CountryOption(region.TwoLetterISORegionName, BuildCountryLabel(region)))
            .ToList();
    }

    private static HashSet<string> CreateCountryCodes()
    {
        return Countries
            .Select(country => country.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildCountryLabel(RegionInfo region)
    {
        if (string.Equals(region.TwoLetterISORegionName, "GR", StringComparison.OrdinalIgnoreCase))
        {
            return $"{region.EnglishName} (GR, tax IDs may use EL)";
        }

        return $"{region.EnglishName} ({region.TwoLetterISORegionName})";
    }
}
