using System;
using System.Collections.Generic;

namespace TSS.Parsing
{
    public interface ITokenStream
    {
        string Document { get; }

        Token Peek(int index);

        Token[] Consume(int count);
    }

    public class TokenStream : ITokenStream
    {
        private readonly Token[] tokens;
        private int location;

        public TokenStream(Token[] tokens)
            : this(tokens, null)
        {
        }

        public TokenStream(Token[] tokens, string document)
        {
            this.tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            Document = document;
        }

        public string Document { get; }

        public Token Current => Peek(0);

        public Token Peek(int index)
        {
            var target = location + index;
            if (target < 0)
            {
                return null;
            }

            return target < tokens.Length ? tokens[target] : null;
        }

        public Token[] Consume(int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var end = location + count;
            if (end > tokens.Length)
            {
                throw new FormatException("Unexpected end of input.");
            }

            var result = new Token[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = tokens[location + i];
            }

            location += count;
            return result;
        }
    }

    public static class TokenStreamExtensions
    {
        public static Token Peek(this ITokenStream stream) => stream.Peek(0);

        public static Token Consume(this ITokenStream stream) => stream.Consume(1)[0];

        public static Token PeekOnly(this ITokenStream stream, Func<Token, bool> predicate, int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var i = 0;
            while (true)
            {
                var current = stream.Peek(i++);
                if (current == null)
                {
                    return null;
                }

                if (predicate(current))
                {
                    index--;
                    if (index < 0)
                    {
                        return current;
                    }
                }
            }
        }

        public static List<Token> ConsumeWhile(this ITokenStream stream, Func<Token, bool> predicate)
            => ConsumeWhile(stream, predicate, true);

        public static List<Token> ConsumeUntil(this ITokenStream stream, Func<Token, bool> predicate)
            => ConsumeWhile(stream, predicate, false);

        private static List<Token> ConsumeWhile(ITokenStream stream, Func<Token, bool> predicate, bool desiredValue)
        {
            var result = new List<Token>();
            while (true)
            {
                var current = stream.Peek();
                if (current == null)
                {
                    throw new FormatException("Unexpected end of input.");
                }

                if (predicate(current) == desiredValue)
                {
                    result.Add(stream.Consume());
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        public static string FormatPosition(this ITokenStream stream, Token token)
        {
            if (token == null)
            {
                throw new FormatException("Unexpected end of input.");
            }

            return stream.FormattedDocument() + token.Line + ":" + token.Column;
        }

        public static string FormattedDocument(this ITokenStream stream)
            => stream.Document != null ? stream.Document + ":" : "";
    }
}
