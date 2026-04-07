using System.Collections.Generic;

namespace NifValidatorPortable;

public sealed class TaxIdValidationResult
{
    public TaxIdValidationResult(
        bool isValid,
        string originalInput,
        string sanitizedInput,
        string normalized,
        string countryCode,
        string type,
        string validationMode,
        string summary,
        string error,
        IReadOnlyList<string> checks)
    {
        IsValid = isValid;
        OriginalInput = originalInput;
        SanitizedInput = sanitizedInput;
        Normalized = normalized;
        CountryCode = countryCode;
        Type = type;
        ValidationMode = validationMode;
        Summary = summary;
        Error = error;
        Checks = checks;
    }

    public bool IsValid { get; }
    public string OriginalInput { get; }
    public string SanitizedInput { get; }
    public string Normalized { get; }
    public string CountryCode { get; }
    public string Type { get; }
    public string ValidationMode { get; }
    public string Summary { get; }
    public string Error { get; }
    public IReadOnlyList<string> Checks { get; }
}
