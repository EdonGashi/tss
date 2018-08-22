using System;
using System.Collections.Generic;
using System.Text;

namespace TSS.Parsing
{
    public abstract class Token
    {
        public static Token[] Parse(string input) => Parse(input, null);

        public static Token[] Parse(string input, string document)
        {
            const int stateDefault = 0;
            const int stateWhitespace = 1;
            const int stateIdentifier = 2;
            const int stateStringSingle = 3;
            const int stateStringDouble = 4;
            const int stateScript = 5;
            const int stateCallback = 6;
            const int stateComment = 7;

            if (document != null)
            {
                document = document + ":";
            }

            var tokens = new List<Token>();
            var memory = new StringBuilder();
            var line = 1;
            var col = 0;
            var state = 0;
            var memcol = 0;
            for (var i = 0; i < input.Length; i++)
            {
                col++;
                var current = input[i];
                var eof = i == input.Length - 1;
                var next = i < input.Length - 1 ? input[i + 1] : '\0';
                if (current == '\n')
                {
                    line++;
                }

                switch (state)
                {
                    case stateDefault:
                        switch (current)
                        {
                            case '\r':
                                continue;
                            case '\n':
                            case ' ':
                            case '\t':
                                tokens.Add(new WhitespaceToken(line, col));
                                state = stateWhitespace;
                                continue;
                            case '\'':
                                memory.Clear();
                                memcol = col;
                                state = stateStringSingle;
                                continue;
                            case '/' when next == '*':
                                memcol = col;
                                i++;
                                col++;
                                state = stateComment;
                                continue;
                            case '"':
                                memory.Clear();
                                memcol = col;
                                state = stateStringDouble;
                                continue;
                            case '<' when next == '{':
                                memcol = col;
                                i++;
                                col++;
                                state = stateCallback;
                                continue;
                            case '<' when next == '?':
                                memcol = col;
                                i++;
                                col++;
                                state = stateScript;
                                continue;
                            case '{':
                                tokens.Add(new LCurlyToken(line, col));
                                continue;
                            case '}':
                                tokens.Add(new RCurlyToken(line, col));
                                continue;
                            case ':':
                                tokens.Add(new ColonToken(line, col));
                                continue;
                            case ';':
                                tokens.Add(new SemiColonToken(line, col));
                                continue;
                            case '*':
                                tokens.Add(new AsteriskToken(line, col));
                                continue;
                            case '!':
                                tokens.Add(new NotToken(line, col));
                                continue;
                            case '^':
                                tokens.Add(new CaretToken(line, col));
                                continue;
                            case ',':
                                tokens.Add(new CommaToken(line, col));
                                continue;
                            case '&':
                                tokens.Add(new AmpersandToken(line, col));
                                continue;
                            default:
                                if (char.IsLetterOrDigit(current) ||
                                    current == '.' ||
                                    current == '#' ||
                                    current == '$' ||
                                    current == '_' ||
                                    current == '-' ||
                                    current == '@' ||
                                    current == '%' ||
                                    current == '/' ||
                                    current == '<' ||
                                    current == '>' ||
                                    current == '=')
                                {
                                    memcol = col;
                                    i--;
                                    col--;
                                    state = stateIdentifier;
                                    continue;
                                }
                                else
                                {
                                    throw new FormatException($"Unknown symbol '{current}' at {document}{line}:{col}.");
                                }
                        }

                    case stateWhitespace:
                        switch (current)
                        {
                            case '\r':
                            case '\n':
                            case ' ':
                            case '\t':
                                continue;
                            default:
                                i--;
                                col--;
                                state = stateDefault;
                                continue;
                        }

                    case stateIdentifier:
                        switch (current)
                        {
                            case '.':
                            case '#':
                            case '$':
                                if (eof)
                                {
                                    throw new FormatException($"Unexpected end of input at {document}{line}:{memcol}.");
                                }

                                if (memory.Length != 0)
                                {
                                    tokens.Add(new IdentifierToken(memory.ToString(), line, memcol));
                                    memory.Clear();
                                }

                                memcol = col;
                                memory.Append(current);

                                continue;

                            default:
                                if (char.IsLetterOrDigit(current) ||
                                    current == '_' ||
                                    current == '-' ||
                                    current == '@' ||
                                    current == '%' ||
                                    current == '/' ||
                                    current == '<' && next != '?' ||
                                    current == '>' ||
                                    current == '=')
                                {
                                    memory.Append(current);
                                    if (eof)
                                    {
                                        tokens.Add(new IdentifierToken(memory.ToString(), line, memcol));
                                        memory.Clear();
                                    }
                                }
                                else
                                {
                                    if (memory.Length != 0)
                                    {
                                        tokens.Add(new IdentifierToken(memory.ToString(), line, memcol));
                                        memory.Clear();
                                    }

                                    col--;
                                    i--;
                                    state = stateDefault;
                                }

                                continue;
                        }

                    case stateStringSingle:
                        switch (current)
                        {
                            case '\'':
                                if (next == '\'')
                                {
                                    memory.Append(current);
                                    i++;
                                    col++;
                                }
                                else
                                {
                                    tokens.Add(new StringToken(memory.ToString(), line, memcol));
                                    memory.Clear();
                                    state = stateDefault;
                                }

                                continue;
                            default:
                                if (eof)
                                {
                                    throw new FormatException($"Unexpected end of string at {document}{line}:{memcol}.");
                                }

                                memory.Append(current);
                                continue;
                        }

                    case stateStringDouble:
                        switch (current)
                        {
                            case '"':
                                if (next == '"')
                                {
                                    memory.Append(current);
                                    i++;
                                    col++;
                                }
                                else
                                {
                                    tokens.Add(new StringToken(memory.ToString(), line, memcol));
                                    memory.Clear();
                                    state = stateDefault;
                                }

                                continue;
                            default:
                                if (eof)
                                {
                                    throw new FormatException($"Unexpected end of string at {document}{line}:{memcol}.");
                                }

                                memory.Append(current);
                                continue;
                        }

                    case stateScript:
                        if (current == '?' && next == '>')
                        {
                            tokens.Add(new ScriptToken(memory.ToString(), line, memcol));
                            memory.Clear();
                            col++;
                            i++;
                            state = stateDefault;
                        }
                        else
                        {
                            memory.Append(current);
                            if (eof)
                            {
                                throw new FormatException($"Unexpected end of script at {document}{line}:{col}.");
                            }
                        }

                        continue;

                    case stateCallback:
                        if (current == '}' && next == '>')
                        {
                            if (!int.TryParse(memory.ToString().Trim(), out var cb))
                            {
                                throw new FormatException($"Invalid numeric callback value at {document}{line}:{memcol}.");
                            }

                            tokens.Add(new CallbackToken(cb, line, memcol));
                            memory.Clear();
                            col++;
                            i++;
                            state = stateDefault;
                        }
                        else
                        {
                            memory.Append(current);
                            if (eof)
                            {
                                throw new FormatException($"Unexpected end of callback at {document}{line}:{col}.");
                            }
                        }

                        continue;

                    case stateComment:
                        if (current == '*' && next == '/')
                        {
                            col++;
                            i++;
                            state = stateDefault;
                        }

                        continue;
                }
            }

            return tokens.ToArray();
        }

        public int Line { get; }

        public int Column { get; }

        protected Token(int line, int column)
        {
            Line = line;
            Column = column;
        }
    }

    public class StringToken : Token
    {
        public StringToken(string value, int line, int column)
            : base(line, column)
        {
            Value = value;
        }

        public string Value { get; }
    }

    public class IdentifierToken : Token
    {
        public IdentifierToken(string value, int line, int column)
            : base(line, column)
        {
            Value = value;
        }

        public string Value { get; }
    }

    public class ScriptToken : Token
    {
        public ScriptToken(string value, int line, int column)
            : base(line, column)
        {
            Value = value;
        }

        public string Value { get; }
    }

    public class CallbackToken : Token
    {
        public CallbackToken(int value, int line, int column)
            : base(line, column)
        {
            Value = value;
        }

        public int Value { get; }
    }

    public class WhitespaceToken : Token
    {
        public WhitespaceToken(int line, int column)
            : base(line, column)
        {
        }
    }

    public class SemiColonToken : Token
    {
        public SemiColonToken(int line, int column)
            : base(line, column)
        {
        }
    }

    public class ColonToken : Token
    {
        public ColonToken(int line, int column)
            : base(line, column)
        {
        }
    }

    public class LCurlyToken : Token
    {
        public LCurlyToken(int line, int column)
            : base(line, column)
        {
        }
    }

    public class RCurlyToken : Token
    {
        public RCurlyToken(int line, int column)
            : base(line, column)
        {
        }
    }

    public class AsteriskToken : Token
    {
        public AsteriskToken(int line, int column)
            : base(line, column)
        {
        }
    }

    public class AmpersandToken : Token
    {
        public AmpersandToken(int line, int column)
            : base(line, column)
        {
        }
    }

    public class NotToken : Token
    {
        public NotToken(int line, int column)
            : base(line, column)
        {
        }
    }

    public class CaretToken : Token
    {
        public CaretToken(int line, int column)
            : base(line, column)
        {
        }
    }

    public class CommaToken : Token
    {
        public CommaToken(int line, int column)
            : base(line, column)
        {
        }
    }
}
