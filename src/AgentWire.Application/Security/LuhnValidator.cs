namespace AgentWire.Application.Security;

public static class LuhnValidator
{
    /// <summary>
    /// Validates a digit string (no separators) using the Luhn checksum algorithm.
    /// Used to distinguish real-looking credit card numbers from arbitrary 13-19 digit runs.
    /// </summary>
    public static bool IsValid(string digitsOnly)
    {
        if (string.IsNullOrEmpty(digitsOnly))
        {
            return false;
        }

        int sum = 0;
        bool doubleDigit = false;

        for (int i = digitsOnly.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(digitsOnly[i]))
            {
                return false;
            }

            int digit = digitsOnly[i] - '0';

            if (doubleDigit)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
    }
}
