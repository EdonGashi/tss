using System;
using System.Collections.Generic;
using System.Linq;

namespace TSS.Visitors
{
    internal static class NumberRangeHelpers
    {
        public static IEnumerable<int> ParseNumberRange(string expression)
        {
            return ParseNumberRange(expression, null);
        }

        public static IEnumerable<int> ParseNumberRange(string expression, Dictionary<string, IEnumerable<int>> context)
        {
            foreach (var expr in expression.Split(','))
            {
                var token = expr.Trim();
                if (token.StartsWith(":"))
                {
                    if (context != null && context.TryGetValue(token.Substring(1).Replace(";;", ","), out var range))
                    {
                        foreach (var number in range)
                        {
                            yield return number;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Warn: token not found {token}");
                    }
                }
                else if (token.Contains("-"))
                {
                    var range = token.Split('-');
                    if (range.Length != 2)
                    {
                        throw new InvalidOperationException("Unexpected \"-\" symbol.");
                    }

                    var start = ParseToken(range[0]);
                    var end = ParseToken(range[1]);
                    if (start > end)
                    {
                        continue;
                    }

                    foreach (var number in Enumerable.Range(start, end - start + 1))
                    {
                        yield return number;
                    }
                }
                else
                {
                    yield return ParseToken(token);
                }
            }
        }

        public static string GetLettersFromNumber(int columnNumber)
        {
            var dividend = columnNumber;
            var letters = string.Empty;
            while (dividend > 0)
            {
                var modulo = (dividend - 1) % 26;
                letters = Convert.ToChar(65 + modulo) + letters;
                dividend = (dividend - modulo) / 26;
            }

            return letters;
        }

        public static int GetNumberFromLetters(string letters)
        {
            if (string.IsNullOrEmpty(letters))
            {
                throw new ArgumentNullException(nameof(letters));
            }

            letters = letters.ToUpperInvariant();

            var sum = 0;
            foreach (var t in letters)
            {
                if (t < 'A' || t > 'Z')
                {
                    throw new FormatException($"Invalid symbol \"{t}\".");
                }

                sum *= 26;
                sum += t - 'A' + 1;
            }

            return sum;
        }

        public static int GetNumberFromExpression(string expression)
        {
            return ParseToken(expression);
        }

        private static int ParseToken(string token)
        {
            return int.TryParse(token, out var result) ? result : GetNumberFromLetters(token);
        }
    }
}
