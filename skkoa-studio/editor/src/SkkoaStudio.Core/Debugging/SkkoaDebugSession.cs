using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkkoaStudio.Core.Formatting;

namespace SkkoaStudio.Core.Debugging;

public enum SkkoaDebugState
{
    NotStarted,
    Paused,
    WaitingForInput,
    Completed,
    Faulted
}

public sealed record SkkoaDebugSnapshot(
    SkkoaDebugState State,
    int? CurrentLine,
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyList<string> CallStack,
    string Output,
    string? Message);

public sealed class SkkoaDebugSession
{
    private readonly DebugProgram program;
    private readonly HashSet<int> breakpoints;
    private readonly List<DebugFrame> stack = [];
    private readonly Queue<string> inputQueue = new();
    private readonly StringBuilder output = new();
    private Dictionary<string, SkkoaValue> lastVariables = new(StringComparer.Ordinal);
    private PendingInput? pendingInput;
    private string? message;

    public SkkoaDebugState State { get; private set; } = SkkoaDebugState.NotStarted;

    public SkkoaDebugSession(string source, IEnumerable<int>? breakpoints = null)
    {
        program = DebugProgram.Parse(source);
        this.breakpoints = new HashSet<int>(breakpoints ?? []);
    }

    public SkkoaDebugSnapshot Start()
    {
        stack.Clear();
        output.Clear();
        lastVariables = new Dictionary<string, SkkoaValue>(StringComparer.Ordinal);
        message = null;
        pendingInput = null;
        stack.Add(new DebugFrame("<시작>", program.MainStatements, new Dictionary<string, SkkoaValue>(StringComparer.Ordinal)));
        State = SkkoaDebugState.Paused;
        NormalizeFrames();
        return Snapshot();
    }

    public SkkoaDebugSnapshot StepInto()
    {
        if (State == SkkoaDebugState.NotStarted)
        {
            return Start();
        }
        if (State is SkkoaDebugState.Completed or SkkoaDebugState.Faulted)
        {
            return Snapshot();
        }

        try
        {
            if (!ConsumePendingInputIfPossible())
            {
                return Snapshot();
            }

            NormalizeFrames();
            if (State == SkkoaDebugState.Completed)
            {
                return Snapshot();
            }

            DebugFrame frame = stack[^1];
            DebugStatement statement = frame.Statements[frame.Index];
            ExecuteStatement(frame, statement);
            NormalizeFrames();
            if (State != SkkoaDebugState.WaitingForInput && State != SkkoaDebugState.Completed)
            {
                State = SkkoaDebugState.Paused;
            }
        }
        catch (Exception ex)
        {
            State = SkkoaDebugState.Faulted;
            message = ex.Message;
        }

        return Snapshot();
    }

    public SkkoaDebugSnapshot StepOver()
    {
        int initialDepth = stack.Count;
        int? initialLine = CurrentLine();
        StepInto();
        while (State == SkkoaDebugState.Paused &&
               stack.Count > initialDepth &&
               CurrentLine() != initialLine)
        {
            StepInto();
        }
        return Snapshot();
    }

    public SkkoaDebugSnapshot StepOut()
    {
        int targetDepth = Math.Max(0, stack.Count - 1);
        while (State == SkkoaDebugState.Paused && stack.Count > targetDepth)
        {
            StepInto();
        }
        return Snapshot();
    }

    public SkkoaDebugSnapshot Continue()
    {
        bool first = true;
        while (State == SkkoaDebugState.Paused)
        {
            int? line = CurrentLine();
            if (!first && line.HasValue && breakpoints.Contains(line.Value))
            {
                break;
            }
            first = false;
            StepInto();
            if (State == SkkoaDebugState.WaitingForInput)
            {
                break;
            }
        }
        return Snapshot();
    }

    public SkkoaDebugSnapshot Stop()
    {
        State = SkkoaDebugState.Completed;
        stack.Clear();
        pendingInput = null;
        return Snapshot();
    }

    public SkkoaDebugSnapshot QueueInput(string text)
    {
        inputQueue.Enqueue(text);
        if (State == SkkoaDebugState.WaitingForInput)
        {
            State = SkkoaDebugState.Paused;
            return StepInto();
        }
        return Snapshot();
    }

    private void ExecuteStatement(DebugFrame frame, DebugStatement statement)
    {
        message = null;
        switch (statement.Kind)
        {
            case DebugStatementKind.If:
                frame.Index++;
                DebugBranch? branch = statement.Branches.FirstOrDefault(branch =>
                    branch.Condition == null || ExpressionEvaluator.Evaluate(branch.Condition, Lookup, InvokeBuiltin).ToBool());
                if (branch != null && branch.Body.Count > 0)
                {
                    stack.Add(new DebugFrame("if", branch.Body, frame.Locals));
                }
                return;

            case DebugStatementKind.While:
                if (ExpressionEvaluator.Evaluate(statement.Condition ?? "거짓", Lookup, InvokeBuiltin).ToBool())
                {
                    stack.Add(new DebugFrame("while", statement.Body, frame.Locals));
                }
                else
                {
                    frame.Index++;
                }
                return;

            case DebugStatementKind.Repeat:
                ExecuteRepeat(frame, statement);
                return;

            case DebugStatementKind.Simple:
                ExecuteSimple(frame, statement);
                return;
        }
    }

    private void ExecuteRepeat(DebugFrame frame, DebugStatement statement)
    {
        if (!frame.RepeatValues.TryGetValue(statement, out int current))
        {
            current = (int)ExpressionEvaluator.Evaluate(statement.StartExpression ?? "0", Lookup, InvokeBuiltin).ToNumber();
            frame.RepeatValues[statement] = current;
        }

        int end = (int)ExpressionEvaluator.Evaluate(statement.EndExpression ?? "0", Lookup, InvokeBuiltin).ToNumber();
        if (current <= end)
        {
            AssignVariable(statement.Iterator ?? "i", SkkoaValue.Number(current));
            stack.Add(new DebugFrame("repeat", statement.Body, frame.Locals)
            {
                OnComplete = () => frame.RepeatValues[statement] = frame.RepeatValues[statement] + 1
            });
        }
        else
        {
            frame.RepeatValues.Remove(statement);
            frame.Index++;
        }
    }

    private void ExecuteSimple(DebugFrame frame, DebugStatement statement)
    {
        string line = statement.Text;
        if (line == "중단" || line == "계속")
        {
            throw new NotSupportedException("단계 실행기는 현재 '중단'과 '계속'을 제한적으로만 지원합니다. 컴파일/실행은 가능합니다.");
        }

        if (line.StartsWith("반환", StringComparison.Ordinal))
        {
            string expression = line.Length > 2 ? line[2..].Trim() : "";
            SkkoaValue value = string.IsNullOrWhiteSpace(expression)
                ? SkkoaValue.Number(0)
                : ExpressionEvaluator.Evaluate(expression, Lookup, InvokeBuiltin);
            CompleteFrame(value);
            return;
        }

        Match declaration = Regex.Match(line, @"^(변수|상수)\s+([A-Za-z_가-힣][A-Za-z0-9_가-힣]*)\s*:\s*([^\s=]+)(?:\s*=\s*(.+))?$");
        if (declaration.Success)
        {
            string name = declaration.Groups[2].Value;
            string type = declaration.Groups[3].Value;
            string initializer = declaration.Groups[4].Success ? declaration.Groups[4].Value.Trim() : "";
            frame.Locals[name] = DefaultValue(type);
            if (initializer.Length > 0)
            {
                if (TryStartFunctionCall(initializer, value => frame.Locals[name] = value))
                {
                    frame.Index++;
                    return;
                }
                frame.Locals[name] = ExpressionEvaluator.Evaluate(initializer, Lookup, InvokeBuiltin);
            }
            frame.Index++;
            return;
        }

        if (line.StartsWith("출력 ", StringComparison.Ordinal))
        {
            string expression = line["출력 ".Length..].Trim();
            if (TryStartFunctionCall(expression, value => AppendOutput(value)))
            {
                frame.Index++;
                return;
            }
            AppendOutput(ExpressionEvaluator.Evaluate(expression, Lookup, InvokeBuiltin));
            frame.Index++;
            return;
        }

        if (line.StartsWith("입력 ", StringComparison.Ordinal))
        {
            string target = line["입력 ".Length..].Trim();
            if (inputQueue.Count == 0)
            {
                pendingInput = new PendingInput(target, frame);
                State = SkkoaDebugState.WaitingForInput;
                message = "입력 대기: " + target;
                return;
            }
            AssignInput(target, inputQueue.Dequeue());
            frame.Index++;
            return;
        }

        Match assignment = Regex.Match(line, @"^(.+?)\s*=\s*(.+)$");
        if (assignment.Success)
        {
            string target = assignment.Groups[1].Value.Trim();
            string expression = assignment.Groups[2].Value.Trim();
            if (TryStartFunctionCall(expression, value => AssignTarget(target, value)))
            {
                frame.Index++;
                return;
            }
            AssignTarget(target, ExpressionEvaluator.Evaluate(expression, Lookup, InvokeBuiltin));
            frame.Index++;
            return;
        }

        if (TryStartFunctionCall(line, _ => { }))
        {
            frame.Index++;
            return;
        }

        ExpressionEvaluator.Evaluate(line, Lookup, InvokeBuiltin);
        frame.Index++;
    }

    private bool TryStartFunctionCall(string expression, Action<SkkoaValue> continuation)
    {
        if (!ExpressionEvaluator.TryParseCall(expression, out string name, out List<string> args) ||
            !program.Functions.TryGetValue(name, out DebugFunction? function))
        {
            return false;
        }

        Dictionary<string, SkkoaValue> locals = new(StringComparer.Ordinal);
        for (int i = 0; i < function.Parameters.Count; i++)
        {
            locals[function.Parameters[i]] = i < args.Count
                ? ExpressionEvaluator.Evaluate(args[i], Lookup, InvokeBuiltin)
                : SkkoaValue.Number(0);
        }

        stack.Add(new DebugFrame(function.Name, function.Body, locals)
        {
            Continuation = continuation
        });
        return true;
    }

    private bool ConsumePendingInputIfPossible()
    {
        if (pendingInput == null)
        {
            return true;
        }
        if (inputQueue.Count == 0)
        {
            State = SkkoaDebugState.WaitingForInput;
            return false;
        }
        AssignInput(pendingInput.Target, inputQueue.Dequeue());
        pendingInput.Frame.Index++;
        pendingInput = null;
        State = SkkoaDebugState.Paused;
        return true;
    }

    private void AssignInput(string target, string raw)
    {
        SkkoaValue value = double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
            ? SkkoaValue.Number(number)
            : raw is "참" or "true" ? SkkoaValue.Bool(true)
            : raw is "거짓" or "false" ? SkkoaValue.Bool(false)
            : SkkoaValue.String(raw);
        AssignTarget(target, value);
    }

    private void AssignTarget(string target, SkkoaValue value)
    {
        Match array = Regex.Match(target, @"^([A-Za-z_가-힣][A-Za-z0-9_가-힣]*)\[(.+)\]$");
        if (array.Success)
        {
            string name = array.Groups[1].Value;
            int index = (int)ExpressionEvaluator.Evaluate(array.Groups[2].Value, Lookup, InvokeBuiltin).ToNumber();
            List<SkkoaValue> values = Lookup(name).AsArray();
            while (values.Count <= index)
            {
                values.Add(SkkoaValue.Number(0));
            }
            values[index] = value;
            AssignVariable(name, SkkoaValue.Array(values));
            return;
        }

        AssignVariable(target, value);
    }

    private void AssignVariable(string name, SkkoaValue value)
    {
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            if (stack[i].Locals.ContainsKey(name))
            {
                stack[i].Locals[name] = value;
                return;
            }
        }
        stack[^1].Locals[name] = value;
    }

    private SkkoaValue Lookup(string name)
    {
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            if (stack[i].Locals.TryGetValue(name, out SkkoaValue value))
            {
                return value;
            }
        }
        return SkkoaValue.Number(0);
    }

    private SkkoaValue InvokeBuiltin(string name, IReadOnlyList<SkkoaValue> args)
    {
        return name switch
        {
            "길이" => SkkoaValue.Number(args.Count > 0 ? args[0].ToDisplayString().Length : 0),
            "비교" => SkkoaValue.Number(string.CompareOrdinal(args.ElementAtOrDefault(0).ToDisplayString(), args.ElementAtOrDefault(1).ToDisplayString())),
            "부분문자열" => Substring(args),
            "배열길이" => SkkoaValue.Number(args.Count > 0 ? args[0].AsArray().Count : 0),
            _ => SkkoaValue.Number(0)
        };
    }

    private static SkkoaValue Substring(IReadOnlyList<SkkoaValue> args)
    {
        if (args.Count < 3)
        {
            return SkkoaValue.String("");
        }
        string text = args[0].ToDisplayString();
        int start = Math.Clamp((int)args[1].ToNumber(), 0, text.Length);
        int length = Math.Clamp((int)args[2].ToNumber(), 0, text.Length - start);
        return SkkoaValue.String(text.Substring(start, length));
    }

    private static SkkoaValue DefaultValue(string type)
    {
        Match array = Regex.Match(type, @"^(.+)\[(\d*)\]$");
        if (array.Success)
        {
            int size = int.TryParse(array.Groups[2].Value, out int parsed) ? parsed : 0;
            return SkkoaValue.Array(Enumerable.Range(0, size).Select(_ => DefaultValue(array.Groups[1].Value)).ToList());
        }
        return type switch
        {
            "문자열" => SkkoaValue.String(""),
            "문자" => SkkoaValue.String("\0"),
            "논리" => SkkoaValue.Bool(false),
            _ => SkkoaValue.Number(0)
        };
    }

    private void AppendOutput(SkkoaValue value)
    {
        output.AppendLine(value.ToDisplayString());
    }

    private void CompleteFrame(SkkoaValue returnValue)
    {
        if (stack.Count == 0)
        {
            State = SkkoaDebugState.Completed;
            return;
        }

        DebugFrame frame = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        frame.Continuation?.Invoke(returnValue);
        NormalizeFrames();
    }

    private void NormalizeFrames()
    {
        while (stack.Count > 0 && stack[^1].Index >= stack[^1].Statements.Count)
        {
            DebugFrame frame = stack[^1];
            lastVariables = new Dictionary<string, SkkoaValue>(frame.Locals, StringComparer.Ordinal);
            stack.RemoveAt(stack.Count - 1);
            frame.OnComplete?.Invoke();
            frame.Continuation?.Invoke(SkkoaValue.Number(0));
        }

        if (stack.Count == 0 && State != SkkoaDebugState.Faulted)
        {
            State = SkkoaDebugState.Completed;
        }
    }

    private int? CurrentLine()
    {
        if (stack.Count == 0 || stack[^1].Index >= stack[^1].Statements.Count)
        {
            return null;
        }
        return stack[^1].Statements[stack[^1].Index].Line;
    }

    private SkkoaDebugSnapshot Snapshot()
    {
        Dictionary<string, SkkoaValue> sourceVariables = stack.Count == 0 ? lastVariables : stack[^1].Locals;
        Dictionary<string, string> variables = sourceVariables.ToDictionary(pair => pair.Key, pair => pair.Value.ToDisplayString(), StringComparer.Ordinal);
        return new SkkoaDebugSnapshot(
            State,
            CurrentLine(),
            variables,
            stack.Select(frame => frame.Name).Reverse().ToArray(),
            output.ToString(),
            message);
    }
}

internal sealed class DebugFrame
{
    public DebugFrame(string name, IReadOnlyList<DebugStatement> statements, Dictionary<string, SkkoaValue> locals)
    {
        Name = name;
        Statements = statements;
        Locals = locals;
    }

    public string Name { get; }
    public IReadOnlyList<DebugStatement> Statements { get; }
    public int Index { get; set; }
    public Dictionary<string, SkkoaValue> Locals { get; }
    public Dictionary<DebugStatement, int> RepeatValues { get; } = [];
    public Action? OnComplete { get; set; }
    public Action<SkkoaValue>? Continuation { get; set; }
}

internal sealed record PendingInput(string Target, DebugFrame Frame);

internal sealed class DebugProgram
{
    public IReadOnlyList<DebugStatement> MainStatements { get; init; } = [];
    public Dictionary<string, DebugFunction> Functions { get; init; } = new(StringComparer.Ordinal);

    public static DebugProgram Parse(string source)
    {
        List<DebugLine> lines = Normalize(source);
        Dictionary<string, DebugFunction> functions = new(StringComparer.Ordinal);
        List<DebugStatement> main = [];

        int index = 0;
        while (index < lines.Count)
        {
            DebugLine line = lines[index];
            if (line.Text.StartsWith("가져오기 ", StringComparison.Ordinal))
            {
                index++;
                continue;
            }
            if (line.Text.StartsWith("구조체 ", StringComparison.Ordinal))
            {
                SkipBlock(lines, ref index);
                continue;
            }
            if (line.Text.StartsWith("함수 ", StringComparison.Ordinal))
            {
                DebugFunction function = ParseFunction(lines, ref index);
                functions[function.Name] = function;
                continue;
            }
            if (line.Text == "시작")
            {
                index++;
                main = ParseStatements(lines, ref index, ["끝"]);
                if (index < lines.Count && lines[index].Text == "끝")
                {
                    index++;
                }
                continue;
            }
            index++;
        }

        return new DebugProgram { MainStatements = main, Functions = functions };
    }

    private static DebugFunction ParseFunction(List<DebugLine> lines, ref int index)
    {
        DebugLine header = lines[index++];
        Match match = Regex.Match(header.Text, @"^함수\s+([A-Za-z_가-힣][A-Za-z0-9_가-힣]*)\((.*)\)\s*:\s*\S+$");
        string name = match.Success ? match.Groups[1].Value : "anonymous";
        List<string> parameters = match.Success
            ? SplitArguments(match.Groups[2].Value)
                .Select(part => part.Split(':')[0].Trim())
                .Where(part => part.Length > 0)
                .ToList()
            : [];
        List<DebugStatement> body = ParseStatements(lines, ref index, ["끝"]);
        if (index < lines.Count && lines[index].Text == "끝")
        {
            index++;
        }
        return new DebugFunction(name, parameters, body);
    }

    private static List<DebugStatement> ParseStatements(List<DebugLine> lines, ref int index, string[] terminators)
    {
        List<DebugStatement> statements = [];
        while (index < lines.Count && !IsAnyTerminator(lines[index].Text, terminators))
        {
            DebugLine line = lines[index];
            if (line.Text.StartsWith("만약 ", StringComparison.Ordinal) && line.Text.EndsWith(" 이면", StringComparison.Ordinal))
            {
                statements.Add(ParseIf(lines, ref index));
                continue;
            }
            if (line.Text.StartsWith("동안 ", StringComparison.Ordinal) && line.Text.EndsWith(" 반복", StringComparison.Ordinal))
            {
                string condition = line.Text["동안 ".Length..^" 반복".Length].Trim();
                index++;
                List<DebugStatement> body = ParseStatements(lines, ref index, ["끝"]);
                if (index < lines.Count && lines[index].Text == "끝")
                {
                    index++;
                }
                statements.Add(DebugStatement.While(line.LineNumber, condition, body));
                continue;
            }
            if (line.Text.StartsWith("반복 ", StringComparison.Ordinal))
            {
                Match match = Regex.Match(line.Text, @"^반복\s+([A-Za-z_가-힣][A-Za-z0-9_가-힣]*)\s*:\s*(.+)부터\s*(.+)까지$");
                index++;
                List<DebugStatement> body = ParseStatements(lines, ref index, ["끝"]);
                if (index < lines.Count && lines[index].Text == "끝")
                {
                    index++;
                }
                statements.Add(DebugStatement.Repeat(
                    line.LineNumber,
                    match.Success ? match.Groups[1].Value : "i",
                    match.Success ? match.Groups[2].Value.Trim() : "0",
                    match.Success ? match.Groups[3].Value.Trim() : "0",
                    body));
                continue;
            }
            statements.Add(DebugStatement.Simple(line.LineNumber, line.Text));
            index++;
        }
        return statements;
    }

    private static DebugStatement ParseIf(List<DebugLine> lines, ref int index)
    {
        DebugLine first = lines[index++];
        string condition = first.Text["만약 ".Length..^" 이면".Length].Trim();
        List<DebugBranch> branches = [];
        List<DebugStatement> firstBody = ParseStatements(lines, ref index, ["아니면만약", "아니면", "끝"]);
        branches.Add(new DebugBranch(condition, firstBody));

        while (index < lines.Count && lines[index].Text.StartsWith("아니면만약 ", StringComparison.Ordinal))
        {
            DebugLine elseIf = lines[index++];
            string elseIfCondition = elseIf.Text["아니면만약 ".Length..^" 이면".Length].Trim();
            List<DebugStatement> body = ParseStatements(lines, ref index, ["아니면만약", "아니면", "끝"]);
            branches.Add(new DebugBranch(elseIfCondition, body));
        }

        if (index < lines.Count && lines[index].Text == "아니면")
        {
            index++;
            List<DebugStatement> elseBody = ParseStatements(lines, ref index, ["끝"]);
            branches.Add(new DebugBranch(null, elseBody));
        }

        if (index < lines.Count && lines[index].Text == "끝")
        {
            index++;
        }
        return DebugStatement.If(first.LineNumber, branches);
    }

    private static bool IsTerminator(string text, string terminator)
    {
        return terminator switch
        {
            "아니면만약" => text.StartsWith("아니면만약 ", StringComparison.Ordinal),
            _ => text == terminator
        };
    }

    private static bool IsAnyTerminator(string text, string[] terminators)
    {
        foreach (string terminator in terminators)
        {
            if (IsTerminator(text, terminator))
            {
                return true;
            }
        }
        return false;
    }

    private static void SkipBlock(List<DebugLine> lines, ref int index)
    {
        index++;
        int depth = 1;
        while (index < lines.Count && depth > 0)
        {
            string text = lines[index].Text;
            if (SkkoaFormatter.IsBlockOpening(text))
            {
                depth++;
            }
            if (text == "끝")
            {
                depth--;
            }
            index++;
        }
    }

    private static List<DebugLine> Normalize(string source)
    {
        List<DebugLine> lines = [];
        string[] rawLines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < rawLines.Length; i++)
        {
            string text = SkkoaFormatter.StripComment(rawLines[i]).Trim();
            if (text.Length > 0)
            {
                lines.Add(new DebugLine(i + 1, text));
            }
        }
        return lines;
    }

    private static List<string> SplitArguments(string text) => ExpressionEvaluator.SplitArguments(text);
}

internal sealed record DebugLine(int LineNumber, string Text);
internal sealed record DebugFunction(string Name, IReadOnlyList<string> Parameters, IReadOnlyList<DebugStatement> Body);
internal sealed record DebugBranch(string? Condition, IReadOnlyList<DebugStatement> Body);

internal enum DebugStatementKind
{
    Simple,
    If,
    While,
    Repeat
}

internal sealed class DebugStatement
{
    private DebugStatement(DebugStatementKind kind, int line)
    {
        Kind = kind;
        Line = line;
    }

    public DebugStatementKind Kind { get; }
    public int Line { get; }
    public string Text { get; private init; } = "";
    public string? Condition { get; private init; }
    public string? Iterator { get; private init; }
    public string? StartExpression { get; private init; }
    public string? EndExpression { get; private init; }
    public IReadOnlyList<DebugStatement> Body { get; private init; } = [];
    public IReadOnlyList<DebugBranch> Branches { get; private init; } = [];

    public static DebugStatement Simple(int line, string text) => new(DebugStatementKind.Simple, line) { Text = text };
    public static DebugStatement If(int line, IReadOnlyList<DebugBranch> branches) => new(DebugStatementKind.If, line) { Branches = branches };
    public static DebugStatement While(int line, string condition, IReadOnlyList<DebugStatement> body) => new(DebugStatementKind.While, line) { Condition = condition, Body = body };
    public static DebugStatement Repeat(int line, string iterator, string start, string end, IReadOnlyList<DebugStatement> body) =>
        new(DebugStatementKind.Repeat, line) { Iterator = iterator, StartExpression = start, EndExpression = end, Body = body };
}

internal enum SkkoaValueKind
{
    Number,
    Bool,
    String,
    Array
}

internal readonly record struct SkkoaValue(SkkoaValueKind Kind, object? Value)
{
    public static SkkoaValue Number(double value) => new(SkkoaValueKind.Number, value);
    public static SkkoaValue Bool(bool value) => new(SkkoaValueKind.Bool, value);
    public static SkkoaValue String(string value) => new(SkkoaValueKind.String, value);
    public static SkkoaValue Array(List<SkkoaValue> value) => new(SkkoaValueKind.Array, value);

    public double ToNumber() => Kind switch
    {
        SkkoaValueKind.Number => Convert.ToDouble(Value, CultureInfo.InvariantCulture),
        SkkoaValueKind.Bool => ToBool() ? 1 : 0,
        SkkoaValueKind.String => double.TryParse((string?)Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : 0,
        SkkoaValueKind.Array => AsArray().Count,
        _ => 0
    };

    public bool ToBool() => Kind switch
    {
        SkkoaValueKind.Bool => (bool)(Value ?? false),
        SkkoaValueKind.Number => Math.Abs(ToNumber()) > double.Epsilon,
        SkkoaValueKind.String => !string.IsNullOrEmpty((string?)Value),
        SkkoaValueKind.Array => AsArray().Count > 0,
        _ => false
    };

    public List<SkkoaValue> AsArray() => Kind == SkkoaValueKind.Array && Value is List<SkkoaValue> list ? list : [];

    public string ToDisplayString()
    {
        return Kind switch
        {
            SkkoaValueKind.Bool => ToBool() ? "참" : "거짓",
            SkkoaValueKind.Number => Math.Abs(ToNumber() - Math.Round(ToNumber())) < 0.0000001
                ? ((long)Math.Round(ToNumber())).ToString(CultureInfo.InvariantCulture)
                : ToNumber().ToString(CultureInfo.InvariantCulture),
            SkkoaValueKind.String => (string?)Value ?? "",
            SkkoaValueKind.Array => "[" + string.Join(", ", AsArray().Select(item => item.ToDisplayString())) + "]",
            _ => ""
        };
    }
}

internal sealed class ExpressionEvaluator
{
    private readonly List<ExprToken> tokens;
    private readonly Func<string, SkkoaValue> lookup;
    private readonly Func<string, IReadOnlyList<SkkoaValue>, SkkoaValue> invoke;
    private int index;

    private ExpressionEvaluator(string expression, Func<string, SkkoaValue> lookup, Func<string, IReadOnlyList<SkkoaValue>, SkkoaValue> invoke)
    {
        tokens = Tokenize(expression);
        this.lookup = lookup;
        this.invoke = invoke;
    }

    public static SkkoaValue Evaluate(string expression, Func<string, SkkoaValue> lookup, Func<string, IReadOnlyList<SkkoaValue>, SkkoaValue> invoke)
    {
        return new ExpressionEvaluator(expression, lookup, invoke).ParseOr();
    }

    public static bool TryParseCall(string expression, out string name, out List<string> args)
    {
        name = "";
        args = [];
        Match match = Regex.Match(expression.Trim(), @"^([A-Za-z_가-힣][A-Za-z0-9_가-힣]*)\((.*)\)$");
        if (!match.Success)
        {
            return false;
        }
        name = match.Groups[1].Value;
        args = SplitArguments(match.Groups[2].Value);
        return true;
    }

    public static List<string> SplitArguments(string text)
    {
        List<string> args = [];
        int start = 0;
        int depth = 0;
        bool inString = false;
        bool inChar = false;
        bool escaped = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if (ch == '\\' && (inString || inChar))
            {
                escaped = true;
                continue;
            }
            if (ch == '"' && !inChar)
            {
                inString = !inString;
            }
            else if (ch == '\'' && !inString)
            {
                inChar = !inChar;
            }
            else if (!inString && !inChar)
            {
                if (ch is '(' or '[')
                {
                    depth++;
                }
                else if (ch is ')' or ']')
                {
                    depth--;
                }
                else if (ch == ',' && depth == 0)
                {
                    args.Add(text[start..i].Trim());
                    start = i + 1;
                }
            }
        }

        string last = text[start..].Trim();
        if (last.Length > 0)
        {
            args.Add(last);
        }
        return args;
    }

    private SkkoaValue ParseOr()
    {
        SkkoaValue left = ParseAnd();
        while (Match("또는"))
        {
            left = SkkoaValue.Bool(left.ToBool() || ParseAnd().ToBool());
        }
        return left;
    }

    private SkkoaValue ParseAnd()
    {
        SkkoaValue left = ParseEquality();
        while (Match("그리고"))
        {
            left = SkkoaValue.Bool(left.ToBool() && ParseEquality().ToBool());
        }
        return left;
    }

    private SkkoaValue ParseEquality()
    {
        SkkoaValue left = ParseComparison();
        while (Peek("==") || Peek("!="))
        {
            string op = Advance().Text;
            SkkoaValue right = ParseComparison();
            bool equal = left.ToDisplayString() == right.ToDisplayString();
            left = SkkoaValue.Bool(op == "==" ? equal : !equal);
        }
        return left;
    }

    private SkkoaValue ParseComparison()
    {
        SkkoaValue left = ParseTerm();
        while (Peek("<") || Peek("<=") || Peek(">") || Peek(">="))
        {
            string op = Advance().Text;
            SkkoaValue right = ParseTerm();
            double l = left.ToNumber();
            double r = right.ToNumber();
            left = SkkoaValue.Bool(op switch
            {
                "<" => l < r,
                "<=" => l <= r,
                ">" => l > r,
                _ => l >= r
            });
        }
        return left;
    }

    private SkkoaValue ParseTerm()
    {
        SkkoaValue left = ParseFactor();
        while (Peek("+") || Peek("-"))
        {
            string op = Advance().Text;
            SkkoaValue right = ParseFactor();
            if (op == "+" && (left.Kind == SkkoaValueKind.String || right.Kind == SkkoaValueKind.String))
            {
                left = SkkoaValue.String(left.ToDisplayString() + right.ToDisplayString());
            }
            else
            {
                left = SkkoaValue.Number(op == "+" ? left.ToNumber() + right.ToNumber() : left.ToNumber() - right.ToNumber());
            }
        }
        return left;
    }

    private SkkoaValue ParseFactor()
    {
        SkkoaValue left = ParseUnary();
        while (Peek("*") || Peek("/") || Peek("%"))
        {
            string op = Advance().Text;
            SkkoaValue right = ParseUnary();
            left = SkkoaValue.Number(op switch
            {
                "*" => left.ToNumber() * right.ToNumber(),
                "/" => right.ToNumber() == 0 ? 0 : left.ToNumber() / right.ToNumber(),
                _ => right.ToNumber() == 0 ? 0 : left.ToNumber() % right.ToNumber()
            });
        }
        return left;
    }

    private SkkoaValue ParseUnary()
    {
        if (Match("-"))
        {
            return SkkoaValue.Number(-ParseUnary().ToNumber());
        }
        if (Match("아님"))
        {
            return SkkoaValue.Bool(!ParseUnary().ToBool());
        }
        return ParsePrimary();
    }

    private SkkoaValue ParsePrimary()
    {
        ExprToken token = Advance();
        if (token.Kind == ExprTokenKind.Number)
        {
            return SkkoaValue.Number(double.Parse(token.Text, CultureInfo.InvariantCulture));
        }
        if (token.Kind == ExprTokenKind.String)
        {
            return SkkoaValue.String(Unquote(token.Text));
        }
        if (token.Text == "참")
        {
            return SkkoaValue.Bool(true);
        }
        if (token.Text == "거짓")
        {
            return SkkoaValue.Bool(false);
        }
        if (token.Text == "(")
        {
            SkkoaValue value = ParseOr();
            Match(")");
            return value;
        }
        if (token.Text == "[")
        {
            List<SkkoaValue> values = [];
            if (!Peek("]"))
            {
                do
                {
                    values.Add(ParseOr());
                } while (Match(","));
            }
            Match("]");
            return SkkoaValue.Array(values);
        }
        if (token.Kind == ExprTokenKind.Identifier)
        {
            if (Match("("))
            {
                List<SkkoaValue> args = [];
                if (!Peek(")"))
                {
                    do
                    {
                        args.Add(ParseOr());
                    } while (Match(","));
                }
                Match(")");
                return invoke(token.Text, args);
            }
            if (Match("["))
            {
                int arrayIndex = (int)ParseOr().ToNumber();
                Match("]");
                List<SkkoaValue> values = lookup(token.Text).AsArray();
                return arrayIndex >= 0 && arrayIndex < values.Count ? values[arrayIndex] : SkkoaValue.Number(0);
            }
            return lookup(token.Text);
        }
        return SkkoaValue.Number(0);
    }

    private bool Match(string text)
    {
        if (!Peek(text))
        {
            return false;
        }
        index++;
        return true;
    }

    private bool Peek(string text) => index < tokens.Count && tokens[index].Text == text;
    private ExprToken Advance() => index < tokens.Count ? tokens[index++] : new ExprToken(ExprTokenKind.End, "");

    private static string Unquote(string text)
    {
        if (text.Length >= 2)
        {
            text = text[1..^1];
        }
        return text.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\"", "\"").Replace("\\'", "'");
    }

    private static List<ExprToken> Tokenize(string expression)
    {
        List<ExprToken> result = [];
        int i = 0;
        while (i < expression.Length)
        {
            char ch = expression[i];
            if (char.IsWhiteSpace(ch))
            {
                i++;
                continue;
            }
            if (char.IsDigit(ch))
            {
                int start = i++;
                while (i < expression.Length && char.IsDigit(expression[i])) i++;
                if (i + 1 < expression.Length && expression[i] == '.' && char.IsDigit(expression[i + 1]))
                {
                    i++;
                    while (i < expression.Length && char.IsDigit(expression[i])) i++;
                }
                result.Add(new ExprToken(ExprTokenKind.Number, expression[start..i]));
                continue;
            }
            if (ch is '"' or '\'')
            {
                char quote = ch;
                int start = i++;
                bool escaped = false;
                while (i < expression.Length)
                {
                    if (!escaped && expression[i] == quote)
                    {
                        i++;
                        break;
                    }
                    escaped = !escaped && expression[i] == '\\';
                    if (expression[i] != '\\') escaped = false;
                    i++;
                }
                result.Add(new ExprToken(ExprTokenKind.String, expression[start..i]));
                continue;
            }
            if (IsIdentifierStart(ch))
            {
                int start = i++;
                while (i < expression.Length && IsIdentifierPart(expression[i])) i++;
                result.Add(new ExprToken(ExprTokenKind.Identifier, expression[start..i]));
                continue;
            }
            if (i + 1 < expression.Length && (expression.Substring(i, 2) is "==" or "!=" or "<=" or ">="))
            {
                result.Add(new ExprToken(ExprTokenKind.Operator, expression.Substring(i, 2)));
                i += 2;
                continue;
            }
            result.Add(new ExprToken("+-*/%<>()[],".Contains(ch) ? ExprTokenKind.Operator : ExprTokenKind.Unknown, ch.ToString()));
            i++;
        }
        return result;
    }

    private static bool IsIdentifierStart(char ch)
    {
        UnicodeCategory category = char.GetUnicodeCategory(ch);
        return ch == '_' || category is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber;
    }

    private static bool IsIdentifierPart(char ch)
    {
        UnicodeCategory category = char.GetUnicodeCategory(ch);
        return IsIdentifierStart(ch) || char.IsDigit(ch) ||
               category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;
    }
}

internal enum ExprTokenKind
{
    Unknown,
    Identifier,
    Number,
    String,
    Operator,
    End
}

internal sealed record ExprToken(ExprTokenKind Kind, string Text);
