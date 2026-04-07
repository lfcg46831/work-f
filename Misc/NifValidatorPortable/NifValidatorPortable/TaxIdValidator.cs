using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NifValidatorPortable;

public static class TaxIdValidator
{
    private static readonly Regex CleanRegex = new(@"[\s\.\-_/]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PtDigitsRegex = new(@"^\d{9}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> PtDoublePrefixes =
    [
        "45", "70", "71", "72", "74", "75", "77", "78", "79", "90", "91", "98", "99"
    ];

    public static TaxIdValidationResult ValidateTaxId(string? input, string? forcedCountryCode = null)
    {
        var checks = new List<string>();

        if (string.IsNullOrWhiteSpace(input))
        {
            checks.Add("A entrada foi rejeitada porque está vazia ou só contém espaços.");
            return Fail(string.Empty, string.Empty, string.Empty, string.Empty, "Sem classificação", "Sem validação", "Valor vazio.", checks);
        }

        var originalInput = input.Trim();
        var sanitized = CleanRegex.Replace(originalInput.ToUpperInvariant(), string.Empty);
        checks.Add("A entrada foi normalizada: remoção de espaços, pontos, hífenes, barras e conversão para maiúsculas.");

        if (sanitized.Length < 2)
        {
            checks.Add("Depois da normalização, o valor ficou demasiado curto para ser validado.");
            return Fail(originalInput, sanitized, sanitized, string.Empty, "Sem classificação", "Sem validação", "Valor demasiado curto.", checks);
        }

        var countryFromParameter = IsoCountryCatalog.NormalizeCountryCode(forcedCountryCode);
        string countryCode;
        string number;

        if (sanitized.Length >= 2 && char.IsLetter(sanitized[0]) && char.IsLetter(sanitized[1]))
        {
            countryCode = IsoCountryCatalog.NormalizeCountryCode(sanitized[..2]);
            number = sanitized[2..];
            checks.Add($"Foi detetado prefixo de pais na propria entrada: {countryCode}.");
        }
        else
        {
            countryCode = string.IsNullOrWhiteSpace(countryFromParameter) ? "PT" : countryFromParameter;
            number = sanitized;
            checks.Add(string.IsNullOrWhiteSpace(countryFromParameter)
                ? "Sem prefixo explicito: a validacao assumiu Portugal por omissao."
                : $"Sem prefixo explicito: a validacao usou o pais selecionado ({countryCode}).");
        }

        if (countryCode.Length != 2 || !char.IsLetter(countryCode[0]) || !char.IsLetter(countryCode[1]))
        {
            checks.Add("O codigo do pais nao ficou num formato ISO alfa-2 valido.");
            return Fail(originalInput, sanitized, sanitized, countryCode, "Sem classificacao", "Sem validacao", "Codigo de pais invalido.", checks);
        }

        if (countryCode == "PT")
        {
            checks.Add("Modo portugues ativado: formato fixo de 9 digitos com verificacao de prefixo e digito de controlo.");

            if (!PtDigitsRegex.IsMatch(number))
            {
                checks.Add("Falhou a regra base do NIF portugues: o valor nao contem exatamente 9 digitos.");
                return Fail(originalInput, sanitized, "PT" + number, countryCode, "Sem classificacao", "PT algoritmico", "O NIF portugues tem de ter exatamente 9 digitos.", checks);
            }

            checks.Add("Passou a regra de comprimento: foram encontrados exatamente 9 digitos.");

            if (!HasValidPortuguesePrefix(number))
            {
                checks.Add("Falhou a validacao de prefixo: o NIF nao comeca por uma serie portuguesa reconhecida.");
                return Fail(originalInput, sanitized, "PT" + number, countryCode, "Sem classificacao", "PT algoritmico", "Prefixo portugues invalido ou nao suportado.", checks);
            }

            checks.Add($"O prefixo {number[..2]} encaixa numa serie portuguesa admitida.");

            if (!HasValidPortugueseCheckDigit(number))
            {
                checks.Add("Falhou o digito de controlo calculado por modulo 11.");
                return Fail(originalInput, sanitized, "PT" + number, countryCode, ClassifyPortugueseTaxId(number), "PT algoritmico", "Digito de controlo invalido.", checks);
            }

            checks.Add("O digito de controlo foi recalculado e coincide com o ultimo digito.");

            return Success(
                originalInput,
                sanitized,
                "PT" + number,
                countryCode,
                ClassifyPortugueseTaxId(number),
                "PT algoritmico",
                "NIF portugues valido.",
                checks);
        }

        checks.Add("Modo estrangeiro ativado: so sera validado o codigo do pais.");

        if (!IsoCountryCatalog.IsValidCountryCode(countryCode))
        {
            checks.Add($"O codigo {countryCode} nao existe na lista ISO 3166 carregada em runtime.");
            return Fail(
                originalInput,
                sanitized,
                countryCode + number,
                countryCode,
                "Estrangeiro",
                "Codigo de pais",
                "Codigo de pais invalido para identificador estrangeiro.",
                checks);
        }

        checks.Add($"O codigo de pais {countryCode} foi reconhecido como valido.");
        checks.Add("O resto do identificador estrangeiro nao foi validado a pedido.");
        return Success(
            originalInput,
            sanitized,
            countryCode + number,
            countryCode,
            "Estrangeiro",
            "Codigo de pais",
            $"Codigo de pais valido para {countryCode}.",
            checks);
    }

    private static bool HasValidPortuguesePrefix(string nif)
    {
        if (!PtDigitsRegex.IsMatch(nif))
        {
            return false;
        }

        return nif[0] switch
        {
            '1' or '2' or '3' or '5' or '6' or '8' => true,
            _ => PtDoublePrefixes.Contains(nif[..2])
        };
    }

    private static bool HasValidPortugueseCheckDigit(string nif)
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

    private static string ClassifyPortugueseTaxId(string nif)
    {
        var prefix2 = nif[..2];
        return prefix2 switch
        {
            "45" => "PT - Pessoa singular nao residente",
            "70" or "74" or "75" => "PT - Heranca indivisa",
            "71" => "PT - Entidade coletiva nao residente",
            "72" => "PT - Fundo de investimento",
            "77" => "PT - Atribuicao oficiosa",
            "78" => "PT - Nao residente (VAT refund)",
            "79" => "PT - Regime excecional",
            "90" or "91" => "PT - Condominio, sociedade irregular ou heranca",
            "98" => "PT - Nao residente sem estabelecimento estavel",
            "99" => "PT - Sociedade civil sem personalidade juridica",
            _ => nif[0] switch
            {
                '1' or '2' or '3' => "PT - Pessoa singular",
                '5' => "PT - Pessoa coletiva",
                '6' => "PT - Organismo publico",
                '8' => "PT - Empresario em nome individual",
                _ => "PT - Tipo nao classificado"
            }
        };
    }

    private static TaxIdValidationResult Success(
        string originalInput,
        string sanitizedInput,
        string normalized,
        string countryCode,
        string type,
        string validationMode,
        string summary,
        IReadOnlyList<string> checks)
    {
        return new TaxIdValidationResult(
            true,
            originalInput,
            sanitizedInput,
            normalized,
            countryCode,
            type,
            validationMode,
            summary,
            string.Empty,
            checks);
    }

    private static TaxIdValidationResult Fail(
        string originalInput,
        string sanitizedInput,
        string normalized,
        string countryCode,
        string type,
        string validationMode,
        string error,
        IReadOnlyList<string> checks)
    {
        return new TaxIdValidationResult(
            false,
            originalInput,
            sanitizedInput,
            normalized,
            countryCode,
            type,
            validationMode,
            error,
            error,
            checks);
    }
}
