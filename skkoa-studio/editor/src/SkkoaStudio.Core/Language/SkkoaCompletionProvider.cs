namespace SkkoaStudio.Core.Language;

public sealed record SkkoaCompletionItem(string DisplayText, string InsertText, int CaretOffset, bool IsSnippet);

public sealed class SkkoaCompletionProvider
{
    private static readonly SkkoaCompletionItem[] Snippets =
    [
        new("시작 block", "시작\n    \n끝", "시작\n    ".Length, true),
        new("만약 block", "만약 조건 이면\n    \n끝", "만약 조건 이면\n    ".Length, true),
        new("동안 block", "동안 조건 반복\n    \n끝", "동안 조건 반복\n    ".Length, true),
        new("반복 block", "반복 i: 1부터 10까지\n    \n끝", "반복 i: 1부터 10까지\n    ".Length, true),
        new("함수 block", "함수 이름(a: 정수): 정수\n    반환 \n끝", "함수 이름(a: 정수): 정수\n    반환 ".Length, true)
    ];

    public IReadOnlyList<SkkoaCompletionItem> GetCompletions(string prefix)
    {
        string normalized = prefix.Trim();
        IEnumerable<SkkoaCompletionItem> words = SkkoaKeywords.CompletionWords
            .Where(word => normalized.Length == 0 || word.StartsWith(normalized, StringComparison.Ordinal))
            .OrderBy(word => word, StringComparer.Ordinal)
            .Select(word => new SkkoaCompletionItem(word, word, word.Length, false));

        IEnumerable<SkkoaCompletionItem> snippets = Snippets
            .Where(item => normalized.Length == 0 ||
                           item.DisplayText.StartsWith(normalized, StringComparison.Ordinal) ||
                           item.InsertText.StartsWith(normalized, StringComparison.Ordinal));

        return words.Concat(snippets).ToArray();
    }
}
