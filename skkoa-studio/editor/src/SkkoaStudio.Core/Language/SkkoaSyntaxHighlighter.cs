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
                SkkoaTokenKind.Keyword => ClassifyKeyword(token.Text),
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

    private static SkkoaHighlightStyle ClassifyKeyword(string text)
    {
        return text switch
        {
            "시작" or "끝" => SkkoaHighlightStyle.BlockKeyword,
            "변수" or "상수" or "구조체" => SkkoaHighlightStyle.DeclarationKeyword,
            "만약" or "이면" or "아니면만약" or "아니면" => SkkoaHighlightStyle.ConditionalKeyword,
            "동안" or "반복" or "부터" or "까지" or "중단" or "계속" => SkkoaHighlightStyle.LoopKeyword,
            "출력" or "입력" => SkkoaHighlightStyle.IoKeyword,
            "함수" or "반환" => SkkoaHighlightStyle.FunctionKeyword,
            "가져오기" => SkkoaHighlightStyle.ImportKeyword,
            "그리고" or "또는" or "아님" => SkkoaHighlightStyle.LogicalKeyword,
            _ => SkkoaHighlightStyle.Keyword
        };
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
