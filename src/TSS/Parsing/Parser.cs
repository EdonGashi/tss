using System;
using System.Collections.Generic;
using System.Text;
using TSS.Ast;

namespace TSS.Parsing
{
    public static class Parser
    {
        internal static Token DrainPeek(this ITokenStream tokens)
        {
            Token current;
            while (true)
            {
                current = tokens.Peek();
                if (current is WhitespaceToken)
                {
                    tokens.Consume();
                }
                else
                {
                    break;
                }
            }

            return current;
        }

        private static Token DrainConsume(this ITokenStream tokens)
        {
            tokens.DrainPeek();
            return tokens.Consume();
        }

        public static Stylesheet ParseStylesheet(ITokenStream tokens)
        {
            var statements = new List<StylesheetDeclaration>();
            while (tokens.DrainPeek() != null)
            {
                statements.Add(ParseStylesheetDeclaration(tokens));
            }

            return new Stylesheet(statements);
        }

        public static StylesheetDeclaration ParseStylesheetDeclaration(ITokenStream tokens)
        {
            var current = tokens.DrainPeek();
            switch (current)
            {
                case ScriptToken _:
                    return ParseScriptDeclaration(tokens);
                default:
                    return ParseStyleDeclaration(tokens, false);
            }
        }

        public static ScriptDeclaration ParseScriptDeclaration(ITokenStream tokens)
        {
            var token = tokens.DrainConsume();
            if (!(token is ScriptToken scriptToken))
            {
                throw new FormatException($"Expected script at {tokens.FormatPosition(token)}.");
            }

            return new ScriptDeclaration(scriptToken.Value);
        }

        public static StyleDeclaration ParseStyleDeclaration(ITokenStream tokens, bool nested)
        {
            var selector = ParseOrSelector(tokens, nested);
            var current = tokens.DrainConsume();
            if (!(current is LCurlyToken))
            {
                throw new FormatException($"Unexpected symbol at {tokens.FormatPosition(current)}, '{{' expected.");
            }

            var statements = new List<StylesheetStatement>();

            while (true)
            {
                current = tokens.DrainPeek();
                if (current is RCurlyToken)
                {
                    tokens.DrainConsume();
                    break;
                }

                statements.Add(ParseStatement(tokens));
            }

            return new StyleDeclaration(selector, statements);
        }

        public static StylesheetStatement ParseStatement(ITokenStream tokens)
        {
            var zero = tokens.DrainPeek();
            if (zero is ScriptToken scriptToken)
            {
                return ParseScriptDeclaration(tokens);
            }

            var one = tokens.PeekOnly(t => !(t is WhitespaceToken), 1);
            if (one is ColonToken)
            {
                tokens.Consume();
                tokens.DrainConsume();

                string key;
                switch (zero)
                {
                    case IdentifierToken id:
                        key = id.Value;
                        break;
                    case StringToken str:
                        key = str.Value;
                        break;
                    default:
                        throw new FormatException($"Expected an identifier or string at {tokens.FormatPosition(zero)}.");
                }

                var stringBuilder = new StringBuilder();
                var ws = false;
                var counter = 0;
                foreach (var t in tokens
                    .ConsumeUntil(t => t is SemiColonToken))
                {
                    switch (t)
                    {
                        case IdentifierToken id:
                            stringBuilder.Append(id.Value);
                            counter++;
                            ws = false;
                            continue;
                        case StringToken str:
                            stringBuilder.Append(str.Value);
                            counter++;
                            ws = false;
                            continue;
                        case WhitespaceToken _:
                            if (counter > 0 && !ws)
                            {
                                stringBuilder.Append(" ");
                            }

                            ws = true;
                            continue;
                        default:
                            throw new FormatException($"Invalid value at {tokens.FormatPosition(t)}.");
                    }
                }

                if (counter == 0)
                {
                    throw new FormatException($"Expected an assigned value to {tokens.FormatPosition(zero)}.");
                }

                tokens.Consume();
                if (ws)
                {
                    stringBuilder.Remove(stringBuilder.Length - 1, 1);
                }

                return new AssignmentStatement(key, stringBuilder.ToString());
            }

            return ParseStyleDeclaration(tokens, true);
        }

        public static OrSelector ParseOrSelector(ITokenStream tokens) => ParseOrSelector(tokens, false);

        public static OrSelector ParseOrSelector(ITokenStream tokens, bool isNested)
        {
            var selectors = new List<ContainmentSelector>
            {
                ParseContainmentSelector(tokens, isNested)
            };

            while (true)
            {
                var current = tokens.DrainPeek();
                if (current is CommaToken)
                {
                    tokens.Consume();
                    tokens.DrainPeek();
                    selectors.Add(ParseContainmentSelector(tokens, isNested));
                }
                else
                {
                    break;
                }
            }

            return new OrSelector(selectors);
        }

        public static ContainmentSelector ParseContainmentSelector(ITokenStream tokens, bool isNested)
        {
            var selectors = new List<AndSelector>
            {
                ParseAndSelector(tokens)
            };

            while (true)
            {
                var zero = tokens.Peek();
                if (!(zero is WhitespaceToken))
                {
                    break;
                }

                var one = tokens.DrainPeek();
                switch (one)
                {
                    case CommaToken _:
                    case LCurlyToken _:
                        goto exit;
                    default:
                        selectors.Add(ParseAndSelector(tokens));
                        continue;
                }
            }

            exit:

            for (var i = 1; i < selectors.Count; i++)
            {
                if (selectors[i].HasContextSelector)
                {
                    throw new FormatException("Unexpected context selector in rule.");
                }
            }

            if (isNested && !selectors[0].HasContextSelector)
            {
                selectors.Insert(0, new AndSelector(new[] { new ContextSelector() }));
            }

            return new ContainmentSelector(selectors);
        }

        public static AndSelector ParseAndSelector(ITokenStream tokens)
        {
            var selectors = new List<ElementSelector>
            {
                ParseNotSelector(tokens)
            };

            while (true)
            {
                var zero = tokens.Peek();
                switch (zero)
                {
                    case CaretToken _:
                        tokens.Consume();
                        selectors.Add(ParseNotSelector(tokens));
                        break;
                    case NotToken _:
                    case AsteriskToken _:
                    case AmpersandToken _:
                    case StringToken _:
                    case IdentifierToken _:
                    case ScriptToken _:
                        selectors.Add(ParseNotSelector(tokens));
                        break;
                    default:
                        return new AndSelector(selectors);
                }
            }

        }

        public static ElementSelector ParseNotSelector(ITokenStream tokens)
        {
            var zero = tokens.Peek();
            var inverse = false;
            if (zero is NotToken)
            {
                inverse = true;
                tokens.Consume();
            }

            var atom = ParseAtomSelector(tokens);
            return inverse ? new NotSelector(atom) : atom;
        }

        public static ElementSelector ParseAtomSelector(ITokenStream tokens)
        {
            var zero = tokens.Consume();
            switch (zero)
            {
                case AsteriskToken _:
                    return new AnySelector();
                case AmpersandToken _:
                    return new ContextSelector();
                case StringToken str:
                    return new IdentifierSelector(str.Value);
                case IdentifierToken id:
                    return new IdentifierSelector(id.Value);
                case ScriptToken script:
                    return new ScriptSelector(script.Value);
                case CallbackToken callback:
                    return new CallbackSelector(callback.Value);
                default:
                    throw new FormatException($"Expected selector identifier at {tokens.FormatPosition(zero)}.");
            }
        }
    }
}
