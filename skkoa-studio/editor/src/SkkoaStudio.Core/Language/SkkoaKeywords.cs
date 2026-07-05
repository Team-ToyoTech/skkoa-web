namespace SkkoaStudio.Core.Language;

public static class SkkoaKeywords
{
    public static readonly string[] Keywords =
    [
        "시작", "끝", "변수", "상수", "출력", "입력", "만약", "이면",
        "아니면만약", "아니면", "동안", "반복", "부터", "까지", "중단",
        "계속", "함수", "반환", "구조체", "가져오기", "그리고", "또는", "아님"
    ];

    public static readonly string[] Types =
    [
        "정수", "실수", "논리", "문자", "문자열", "없음", "포인터"
    ];

    public static readonly string[] Literals =
    [
        "참", "거짓"
    ];

    public static readonly string[] StandardFunctions =
    [
        "길이", "비교", "부분문자열", "배열길이", "주소", "값", "할당", "해제"
    ];

    public static readonly string[] StandardModuleFunctions =
    [
        "스택초기화", "스택넣기", "스택빼기", "스택보기", "스택비었나",
        "스택가득찼나", "스택크기", "큐초기화", "큐넣기", "큐빼기",
        "큐보기", "큐비었나", "큐가득찼나", "큐크기"
    ];

    public static readonly string[] SnippetNames =
    [
        "snippet:시작", "snippet:만약", "snippet:동안", "snippet:반복", "snippet:함수"
    ];

    public static readonly HashSet<string> KeywordSet = new(Keywords);
    public static readonly HashSet<string> TypeSet = new(Types);
    public static readonly HashSet<string> LiteralSet = new(Literals);
    public static readonly HashSet<string> StandardFunctionSet = new(StandardFunctions);
    public static readonly HashSet<string> StandardModuleFunctionSet = new(StandardModuleFunctions);

    public static IEnumerable<string> CompletionWords =>
        Keywords.Concat(Types).Concat(Literals).Concat(StandardFunctions).Concat(StandardModuleFunctions);
}
