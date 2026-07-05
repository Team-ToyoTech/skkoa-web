namespace SkkoaStudio.Core.Language;

public enum SkkoaTokenKind
{
    Unknown,
    Keyword,
    Type,
    BooleanLiteral,
    StandardFunction,
    StandardModuleFunction,
    Identifier,
    Number,
    String,
    Character,
    Comment,
    Operator,
    Punctuation,
    NewLine,
    Whitespace
}

public sealed record SkkoaToken(
    SkkoaTokenKind Kind,
    string Text,
    int Start,
    int Length,
    int Line,
    int Column);

public enum SkkoaHighlightStyle
{
    Default = 0,
    Keyword = 1,
    Type = 2,
    Literal = 3,
    StandardFunction = 4,
    String = 5,
    Character = 6,
    Number = 7,
    Comment = 8,
    Operator = 9,
    Brace = 10,
    FunctionName = 11,
    StructName = 12,
    VariableName = 13
}

public sealed record SkkoaHighlightSpan(int Start, int Length, SkkoaHighlightStyle Style);
