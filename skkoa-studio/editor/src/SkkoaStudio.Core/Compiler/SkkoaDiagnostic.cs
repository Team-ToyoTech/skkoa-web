using System.Text.Json;
using System.Text.Json.Serialization;

namespace SkkoaStudio.Core.Compiler;

public sealed class SkkoaDiagnostic
{
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "error";

    [JsonPropertyName("code")]
    public string Code { get; set; } = "SKK000";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("file")]
    public string File { get; set; } = "";

    [JsonPropertyName("line")]
    public int Line { get; set; } = 1;

    [JsonPropertyName("column")]
    public int Column { get; set; } = 1;

    [JsonPropertyName("length")]
    public int Length { get; set; } = 1;

    public bool IsWarning => Severity.Equals("warning", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<SkkoaDiagnostic> ParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return (JsonSerializer.Deserialize<List<SkkoaDiagnostic?>>(json, JsonOptions()) ?? [])
                .Where(diagnostic => diagnostic != null)
                .Select(diagnostic => Sanitize(diagnostic!))
                .ToArray();
        }
        catch
        {
            return
            [
                new SkkoaDiagnostic
                {
                    Severity = "error",
                    Code = "SKKJSON",
                    Message = "진단 JSON을 파싱할 수 없습니다.",
                    Line = 1,
                    Column = 1,
                    Length = 1
                }
            ];
        }
    }

    private static SkkoaDiagnostic Sanitize(SkkoaDiagnostic diagnostic)
    {
        diagnostic.Severity = string.IsNullOrWhiteSpace(diagnostic.Severity) ? "error" : diagnostic.Severity;
        diagnostic.Code = string.IsNullOrWhiteSpace(diagnostic.Code) ? "SKK000" : diagnostic.Code;
        diagnostic.Message ??= "";
        diagnostic.File ??= "";
        diagnostic.Line = Math.Max(1, diagnostic.Line);
        diagnostic.Column = Math.Max(1, diagnostic.Column);
        diagnostic.Length = Math.Max(1, diagnostic.Length);
        return diagnostic;
    }

    public static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
