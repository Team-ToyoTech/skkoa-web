using System.Globalization;

namespace SkkoaStudio.Core.Language;

public sealed class SkkoaTokenizer
{
    public IReadOnlyList<SkkoaToken> Tokenize(string source)
    {
        List<SkkoaToken> tokens = [];
        int index = 0;
        int line = 1;
        int column = 1;

        while (index < source.Length)
        {
            int start = index;
            int startLine = line;
            int startColumn = column;
            char ch = source[index];

            if (ch == '\r' || ch == '\n')
            {
                if (ch == '\r' && index + 1 < source.Length && source[index + 1] == '\n')
                {
                    index += 2;
                }
                else
                {
                    index++;
                }
                tokens.Add(new SkkoaToken(SkkoaTokenKind.NewLine, source[start..index], start, index - start, startLine, startColumn));
                line++;
                column = 1;
                continue;
            }

            if (ch == ' ' || ch == '\t')
            {
                while (index < source.Length && (source[index] == ' ' || source[index] == '\t'))
                {
                    Advance(source[index++], ref line, ref column);
                }
                tokens.Add(new SkkoaToken(SkkoaTokenKind.Whitespace, source[start..index], start, index - start, startLine, startColumn));
                continue;
            }

            if (ch == '#')
            {
                while (index < source.Length && source[index] != '\r' && source[index] != '\n')
                {
                    Advance(source[index++], ref line, ref column);
                }
                tokens.Add(new SkkoaToken(SkkoaTokenKind.Comment, source[start..index], start, index - start, startLine, startColumn));
                continue;
            }

            if (ch == '"' || ch == '\'')
            {
                char quote = ch;
                index++;
                column++;
                bool escaped = false;
                while (index < source.Length)
                {
                    char current = source[index];
                    if (!escaped && current == quote)
                    {
                        index++;
                        column++;
                        break;
                    }
                    if (!escaped && (current == '\r' || current == '\n'))
                    {
                        break;
                    }
                    escaped = !escaped && current == '\\';
                    if (current != '\\')
                    {
                        escaped = false;
                    }
                    Advance(current, ref line, ref column);
                    index++;
                }

                tokens.Add(new SkkoaToken(
                    quote == '"' ? SkkoaTokenKind.String : SkkoaTokenKind.Character,
                    source[start..index], start, index - start, startLine, startColumn));
                continue;
            }

            if (char.IsDigit(ch))
            {
                while (index < source.Length && char.IsDigit(source[index]))
                {
                    index++;
                    column++;
                }
                if (index + 1 < source.Length && source[index] == '.' && char.IsDigit(source[index + 1]))
                {
                    index++;
                    column++;
                    while (index < source.Length && char.IsDigit(source[index]))
                    {
                        index++;
                        column++;
                    }
                }
                tokens.Add(new SkkoaToken(SkkoaTokenKind.Number, source[start..index], start, index - start, startLine, startColumn));
                continue;
            }

            if (IsIdentifierStart(ch))
            {
                while (index < source.Length && IsIdentifierPart(source[index]))
                {
                    Advance(source[index++], ref line, ref column);
                }

                string text = source[start..index];
                tokens.Add(new SkkoaToken(GetIdentifierKind(text), text, start, index - start, startLine, startColumn));
                continue;
            }

            if (IsOperatorStart(ch))
            {
                index++;
                column++;
                if (index < source.Length && (ch == '=' || ch == '!' || ch == '<' || ch == '>') && source[index] == '=')
                {
                    index++;
                    column++;
                }
                tokens.Add(new SkkoaToken(SkkoaTokenKind.Operator, source[start..index], start, index - start, startLine, startColumn));
                continue;
            }

            index++;
            column++;
            tokens.Add(new SkkoaToken(SkkoaTokenKind.Punctuation, source[start..index], start, index - start, startLine, startColumn));
        }

        return tokens;
    }

    private static SkkoaTokenKind GetIdentifierKind(string text)
    {
        if (SkkoaKeywords.KeywordSet.Contains(text))
        {
            return SkkoaTokenKind.Keyword;
        }
        if (SkkoaKeywords.TypeSet.Contains(text))
        {
            return SkkoaTokenKind.Type;
        }
        if (SkkoaKeywords.LiteralSet.Contains(text))
        {
            return SkkoaTokenKind.BooleanLiteral;
        }
        if (SkkoaKeywords.StandardFunctionSet.Contains(text))
        {
            return SkkoaTokenKind.StandardFunction;
        }
        if (SkkoaKeywords.StandardModuleFunctionSet.Contains(text))
        {
            return SkkoaTokenKind.StandardModuleFunction;
        }
        return SkkoaTokenKind.Identifier;
    }

    private static bool IsIdentifierStart(char ch)
    {
        UnicodeCategory category = char.GetUnicodeCategory(ch);
        return ch == '_' ||
               category is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter
                   or UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber;
    }

    private static bool IsIdentifierPart(char ch)
    {
        UnicodeCategory category = char.GetUnicodeCategory(ch);
        return IsIdentifierStart(ch) || char.IsDigit(ch) ||
               category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;
    }

    private static bool IsOperatorStart(char ch)
    {
        return ch is '+' or '-' or '*' or '/' or '%' or '=' or '!' or '<' or '>';
    }

    private static void Advance(char ch, ref int line, ref int column)
    {
        if (ch == '\n')
        {
            line++;
            column = 1;
        }
        else
        {
            column++;
        }
    }
}
