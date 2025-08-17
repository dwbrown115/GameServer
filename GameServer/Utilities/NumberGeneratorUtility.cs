using System;
using System.Linq;
using System.Text;

namespace GameServer.Utilities
{
    public static class NumberGeneratorUtility
    {
        private static readonly Random _random = new Random();

        /// <summary>
        /// Generates a number string with a simple checksum digit.
        /// The last digit is a checksum (sum of all other digits modulo 10).
        /// </summary>
        /// <param name="length">The total length of the number string, including the checksum digit.</param>
        /// <returns>A generated number string.</returns>
        public static string GenerateValidNumber(int length)
        {
            if (length <= 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(length),
                    "Length must be greater than 1."
                );
            }

            StringBuilder sb = new StringBuilder();
            int sum = 0;

            // Generate random digits for the non-checksum part
            for (int i = 0; i < length - 1; i++)
            {
                int digit = _random.Next(0, 10);
                sb.Append(digit);
                sum += digit;
            }

            // Calculate checksum digit
            int checksum = sum % 10;
            sb.Append(checksum);

            return sb.ToString();
        }

        /// <summary>
        /// Validates a number string based on the simple checksum algorithm.
        /// </summary>
        /// <param name="number">The number string to validate.</param>
        /// <returns>True if the number is valid, false otherwise.</returns>
        public static bool IsValidNumber(string number)
        {
            if (string.IsNullOrEmpty(number) || number.Length <= 1 || !number.All(char.IsDigit))
            {
                return false;
            }

            int sum = 0;
            for (int i = 0; i < number.Length - 1; i++)
            {
                sum += (int)char.GetNumericValue(number[i]);
            }

            int expectedChecksum = sum % 10;
            int actualChecksum = (int)char.GetNumericValue(number[number.Length - 1]);

            return expectedChecksum == actualChecksum;
        }
    }
}
