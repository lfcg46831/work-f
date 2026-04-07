namespace NifValidatorPortable;

public sealed class CountryOption
{
    public CountryOption(string code, string displayText)
    {
        Code = code;
        DisplayText = displayText;
    }

    public string Code { get; }
    public string DisplayText { get; }
}
