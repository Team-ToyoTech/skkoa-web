using SkkoaStudio.Core.Compiler;
using SkkoaStudio.Core.Formatting;
using SkkoaStudio.Core.Language;
using SkkoaStudio.Core.ProjectSystem;
using SkkoaStudio.Core.Settings;
using Xunit;

namespace SkkoaStudio.Tests;

public sealed class LanguageServiceTests
{
    [Fact]
    public void Tokenizer_classifies_keywords()
    {
        SkkoaTokenizer tokenizer = new();
        IReadOnlyList<SkkoaToken> tokens = tokenizer.Tokenize("시작\n변수 x: 정수 = 1\n끝");

        Assert.Contains(tokens, token => token.Text == "시작" && token.Kind == SkkoaTokenKind.Keyword);
        Assert.Contains(tokens, token => token.Text == "변수" && token.Kind == SkkoaTokenKind.Keyword);
        Assert.Contains(tokens, token => token.Text == "정수" && token.Kind == SkkoaTokenKind.Type);
    }

    [Fact]
    public void Tokenizer_classifies_string_literals()
    {
        SkkoaTokenizer tokenizer = new();
        IReadOnlyList<SkkoaToken> tokens = tokenizer.Tokenize("출력 \"안녕\"");

        Assert.Contains(tokens, token => token.Text == "\"안녕\"" && token.Kind == SkkoaTokenKind.String);
    }

    [Fact]
    public void Tokenizer_classifies_hash_comments()
    {
        SkkoaTokenizer tokenizer = new();
        IReadOnlyList<SkkoaToken> tokens = tokenizer.Tokenize("출력 1 # comment");

        Assert.Contains(tokens, token => token.Kind == SkkoaTokenKind.Comment && token.Text == "# comment");
    }

    [Fact]
    public void Completion_returns_keyword_and_snippet_candidates()
    {
        SkkoaCompletionProvider provider = new();
        IReadOnlyList<SkkoaCompletionItem> completions = provider.GetCompletions("시");

        Assert.Contains(completions, item => item.DisplayText == "시작");
        Assert.Contains(provider.GetCompletions(""), item => item.IsSnippet && item.DisplayText.Contains("함수", StringComparison.Ordinal));
    }

    [Fact]
    public void Formatter_indents_after_block_open()
    {
        SkkoaFormatter formatter = new();
        string formatted = formatter.FormatDocument("시작\n출력 1\n끝", 4);

        Assert.Contains("    출력 1", formatted);
    }

    [Fact]
    public void Formatter_outdents_end()
    {
        SkkoaFormatter formatter = new();
        string formatted = formatter.FormatDocument("시작\n    출력 1\n    끝", 4);

        Assert.EndsWith("끝", formatted);
        Assert.DoesNotContain("    끝", formatted);
    }

    [Fact]
    public void Formatter_aligns_else()
    {
        SkkoaFormatter formatter = new();
        string formatted = formatter.FormatDocument("시작\n만약 참 이면\n출력 1\n아니면\n출력 2\n끝\n끝", 4);

        Assert.Contains("    아니면", formatted);
    }

    [Fact]
    public void Diagnostic_json_parses()
    {
        string json = """
        [
          {"severity":"error","code":"SKK001","message":"오류","file":"main.koa","line":12,"column":5,"length":2}
        ]
        """;

        IReadOnlyList<SkkoaDiagnostic> diagnostics = SkkoaDiagnostic.ParseJson(json);

        Assert.Single(diagnostics);
        Assert.Equal("SKK001", diagnostics[0].Code);
        Assert.Equal(12, diagnostics[0].Line);
    }

    [Fact]
    public void Diagnostic_json_sanitizes_null_and_invalid_fields()
    {
        string json = """
        [
          null,
          {"severity":null,"code":null,"message":null,"file":null,"line":0,"column":0,"length":0}
        ]
        """;

        IReadOnlyList<SkkoaDiagnostic> diagnostics = SkkoaDiagnostic.ParseJson(json);

        Assert.Single(diagnostics);
        Assert.Equal("error", diagnostics[0].Severity);
        Assert.Equal("SKK000", diagnostics[0].Code);
        Assert.Equal(1, diagnostics[0].Line);
        Assert.Equal(1, diagnostics[0].Column);
        Assert.Equal(1, diagnostics[0].Length);
    }

    [Fact]
    public async Task Process_runner_captures_exit_code_and_output()
    {
        SkkoaProcessRunner runner = new();
        SkkoaProcessResult result = await runner.RunAsync("cmd.exe", ["/c", "echo hello"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.StandardOutput);
    }

    [Fact]
    public async Task Process_runner_returns_error_when_process_cannot_start()
    {
        SkkoaProcessRunner runner = new();
        SkkoaProcessResult result = await runner.RunAsync("definitely-not-a-skkoa-tool.exe", []);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("프로세스를 시작할 수 없습니다", result.StandardError);
    }

    [Fact]
    public void Project_file_round_trips()
    {
        string dir = Path.Combine(Path.GetTempPath(), "skkoa-project-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        SkkoaProjectService service = new();
        SkkoaProject project = service.Create("HelloSkkoa", dir);

        SkkoaProject loaded = service.Load(project.ProjectPath!);

        Assert.Equal("HelloSkkoa", loaded.Name);
        Assert.Equal("main.koa", loaded.Entry);
    }

    [Fact]
    public void Project_file_loads_with_missing_lists()
    {
        string dir = Path.Combine(Path.GetTempPath(), "skkoa-project-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "broken.skkoaproj");
            File.WriteAllText(path, """{"name":"","entry":"","files":null,"libPaths":null,"outputName":null}""");
            SkkoaProjectService service = new();

            SkkoaProject project = service.Load(path);

            Assert.Equal("broken", project.Name);
            Assert.Equal("main.koa", project.Entry);
            Assert.Contains("main.koa", project.Files);
            Assert.Empty(project.LibPaths);
            Assert.Equal("", project.OutputName);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Settings_file_round_trips()
    {
        SkkoaStudioSettings settings = new()
        {
            Theme = "Light",
            FontFamily = "Consolas",
            FontSize = 13,
            TabSize = 2
        };

        Assert.Equal("Light", settings.Theme);
        Assert.Equal(2, settings.TabSize);
    }
}
