using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace TotalCheckoutPOS.Services.POS.Api.Application
{
    public interface IVatNumberValidatorApplication
    {
        bool Validate(string value, string? forcedCountryCode = null);
    }

    public class VatNumberValidatorApplication : IVatNumberValidatorApplication
    {
        private static readonly Regex DigitsRegex = new(@"^\d{9}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex CountryPrefixedRegex = new(@"^[A-Z]{2}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ForeignBodyRegex = new(@"^[A-Z0-9]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex CleanRegex = new(@"[\s\.\-_/]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Lazy<HashSet<string>> CountryCodes = new(CreateCountryCodes);

        private static readonly HashSet<string> ValidDoublePrefixes =
        [
            "45", "70", "71", "72", "74", "75", "77", "78", "79", "90", "91", "98", "99"
        ];

        public bool Validate(string value, string? forcedCountryCode = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = NormalizeInput(value).ToUpperInvariant();
            if (normalized.Length < 2)
            {
                return false;
            }

            var fromParameter = NormalizeCountryCode(forcedCountryCode);
            string countryCode;
            string number;

            if (CountryPrefixedRegex.IsMatch(normalized))
            {
                countryCode = NormalizeCountryCode(normalized[..2]);
                number = normalized[2..];
            }
            else
            {
                countryCode = string.IsNullOrWhiteSpace(fromParameter) ? "PT" : fromParameter;
                number = normalized;
            }

            if (!IsCountryCodeValid(countryCode))
            {
                return false;
            }

            if (countryCode == "PT")
            {
                return IsPortugueseNifBodyValid(number);
            }

            return number.Length > 0 && ForeignBodyRegex.IsMatch(number);
        }

        private static bool IsCountryCodeValid(string? countryCode)
        {
            var normalized = NormalizeCountryCode(countryCode);
            if (normalized.Length != 2 || !char.IsLetter(normalized[0]) || !char.IsLetter(normalized[1]))
            {
                return false;
            }

            return normalized == "EL" || CountryCodes.Value.Contains(normalized);
        }

        private static bool IsPortugueseNifBodyValid(string nif)
        {
            if (!DigitsRegex.IsMatch(nif))
            {
                return false;
            }

            if (!HasValidPrefix(nif))
            {
                return false;
            }

            return HasValidCheckDigit(nif);
        }

        private static string NormalizeInput(string input)
        {
            return CleanRegex.Replace(input.Trim(), string.Empty);
        }

        private static string NormalizeCountryCode(string? countryCode)
        {
            return string.IsNullOrWhiteSpace(countryCode)
                ? string.Empty
                : countryCode.Trim().ToUpperInvariant();
        }

        private static bool HasValidPrefix(string nif)
        {
            return nif[0] switch
            {
                '1' or '2' or '3' or '5' or '6' or '8' => true,
                _ => ValidDoublePrefixes.Contains(nif[..2])
            };
        }

        private static bool HasValidCheckDigit(string nif)
        {
            var sum = 0;
            for (var i = 0; i < 8; i++)
            {
                sum += (nif[i] - '0') * (9 - i);
            }

            var remainder = sum % 11;
            var expected = remainder < 2 ? 0 : 11 - remainder;
            return (nif[8] - '0') == expected;
        }

        private static HashSet<string> CreateCountryCodes()
        {
            return CultureInfo
                .GetCultures(CultureTypes.SpecificCultures)
                .Select(culture =>
                {
                    try
                    {
                        return new RegionInfo(culture.Name).TwoLetterISORegionName;
                    }
                    catch
                    {
                        return string.Empty;
                    }
                })
                .Where(code => code.Length == 2 && code.All(char.IsLetter))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}
