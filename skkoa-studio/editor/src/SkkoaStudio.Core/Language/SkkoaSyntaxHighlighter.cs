namespace SkkoaStudio.Core.Language;

public sealed class SkkoaSyntaxHighlighter
{
    private readonly SkkoaTokenizer tokenizer = new();

    public IReadOnlyList<SkkoaHighlightSpan> GetSpans(string source)
    {
        IReadOnlyList<SkkoaToken> tokens = tokenizer.Tokenize(source);
        List<SkkoaHighlightSpan> spans = [];

        for (int i = 0; i < tokens.Count; i++)
        {
            SkkoaToken token = tokens[i];
            if (token.Kind is SkkoaTokenKind.Whitespace or SkkoaTokenKind.NewLine)
            {
                continue;
            }

            SkkoaHighlightStyle style = token.Kind switch
            {
                SkkoaTokenKind.Keyword => SkkoaHighlightStyle.Keyword,
                SkkoaTokenKind.Type => SkkoaHighlightStyle.Type,
                SkkoaTokenKind.BooleanLiteral => SkkoaHighlightStyle.Literal,
                SkkoaTokenKind.StandardFunction or SkkoaTokenKind.StandardModuleFunction => SkkoaHighlightStyle.StandardFunction,
                SkkoaTokenKind.String => SkkoaHighlightStyle.String,
                SkkoaTokenKind.Character => SkkoaHighlightStyle.Character,
                SkkoaTokenKind.Number => SkkoaHighlightStyle.Number,
                SkkoaTokenKind.Comment => SkkoaHighlightStyle.Comment,
                SkkoaTokenKind.Operator => SkkoaHighlightStyle.Operator,
                SkkoaTokenKind.Punctuation when token.Text is "(" or ")" or "[" or "]" => SkkoaHighlightStyle.Brace,
                SkkoaTokenKind.Identifier => ClassifyIdentifier(tokens, i),
                _ => SkkoaHighlightStyle.Default
            };

            if (style != SkkoaHighlightStyle.Default)
            {
                spans.Add(new SkkoaHighlightSpan(token.Start, token.Length, style));
            }
        }

        return spans;
    }

    private static SkkoaHighlightStyle ClassifyIdentifier(IReadOnlyList<SkkoaToken> tokens, int index)
    {
        SkkoaToken? previous = PreviousSignificant(tokens, index);
        SkkoaToken? next = NextSignificant(tokens, index);

        if (previous?.Text == "함수" || next?.Text == "(")
        {
            return SkkoaHighlightStyle.FunctionName;
        }
        if (previous?.Text == "구조체")
        {
            return SkkoaHighlightStyle.StructName;
        }
        return SkkoaHighlightStyle.VariableName;
    }

    private static SkkoaToken? PreviousSignificant(IReadOnlyList<SkkoaToken> tokens, int index)
    {
        for (int i = index - 1; i >= 0; i--)
        {
            if (tokens[i].Kind is not (SkkoaTokenKind.Whitespace or SkkoaTokenKind.NewLine or SkkoaTokenKind.Comment))
            {
                return tokens[i];
            }
        }
        return null;
    }

    private static SkkoaToken? NextSignificant(IReadOnlyList<SkkoaToken> tokens, int index)
    {
        for (int i = index + 1; i < tokens.Count; i++)
        {
            if (tokens[i].Kind is not (SkkoaTokenKind.Whitespace or SkkoaTokenKind.NewLine or SkkoaTokenKind.Comment))
            {
                return tokens[i];
            }
        }
        return null;
    }
}
