using System.Text.RegularExpressions;

namespace CarRepair.Auth.Domain.ValueObjects;

public sealed partial record Cpf
{
    private Cpf(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool IsValid(string? input) => TryCreate(input, out _);

    public static string Normalize(string input) => DigitsOnlyRegex().Replace(input, string.Empty);

    public static bool TryCreate(string? input, out Cpf? cpf)
    {
        cpf = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = Normalize(input);
        if (normalized.Length != 11 || normalized.Distinct().Count() == 1)
        {
            return false;
        }

        if (!HasValidCheckDigits(normalized))
        {
            return false;
        }

        cpf = new Cpf(normalized);
        return true;
    }

    private static bool HasValidCheckDigits(string cpf)
    {
        static int CalculateDigit(ReadOnlySpan<char> digits, ReadOnlySpan<int> multipliers)
        {
            var sum = 0;
            for (var index = 0; index < multipliers.Length; index++)
            {
                sum += (digits[index] - '0') * multipliers[index];
            }

            var remainder = sum % 11;
            return remainder < 2 ? 0 : 11 - remainder;
        }

        Span<int> firstMultipliers = stackalloc[] { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        Span<int> secondMultipliers = stackalloc[] { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

        var firstDigit = CalculateDigit(cpf.AsSpan(0, 9), firstMultipliers);
        var secondDigit = CalculateDigit(cpf.AsSpan(0, 10), secondMultipliers);

        return cpf[9] - '0' == firstDigit && cpf[10] - '0' == secondDigit;
    }

    [GeneratedRegex("[^0-9]", RegexOptions.Compiled)]
    private static partial Regex DigitsOnlyRegex();
}
