namespace SkkoaStudio.Core.Formatting;

public sealed class SkkoaFormatter
{
    public string FormatDocument(string source, int tabSize = 4)
    {
        string[] lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int depth = 0;
        string indentUnit = new(' ', Math.Clamp(tabSize, 1, 12));
        List<string> formatted = new(lines.Length);

        foreach (string rawLine in lines)
        {
            string trimmed = rawLine.Trim();
            if (trimmed.Length == 0)
            {
                formatted.Add("");
                continue;
            }

            string code = StripComment(trimmed).Trim();
            if (IsClosingOrContinuation(code))
            {
                depth = Math.Max(0, depth - 1);
            }

            formatted.Add(string.Concat(Enumerable.Repeat(indentUnit, depth)) + trimmed);

            if (IsBlockOpening(code))
            {
                depth++;
            }
        }

        return string.Join(Environment.NewLine, formatted);
    }

    public string FormatSelection(string selection, int baseDepth, int tabSize = 4)
    {
        string formatted = FormatDocument(selection, tabSize);
        if (baseDepth <= 0)
        {
            return formatted;
        }

        string prefix = new(' ', baseDepth * Math.Clamp(tabSize, 1, 12));
        return string.Join(Environment.NewLine, formatted.Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(line => line.Length == 0 ? line : prefix + line));
    }

    public string GetIndentForNewLine(string currentLine, int tabSize = 4)
    {
        string leading = new(currentLine.TakeWhile(ch => ch is ' ' or '\t').ToArray());
        string code = StripComment(currentLine).Trim();
        string indentUnit = new(' ', Math.Clamp(tabSize, 1, 12));
        if (IsBlockOpening(code))
        {
            return leading + indentUnit;
        }
        return leading;
    }

    public string NormalizeCurrentLineIndent(string previousText, string currentLine, int tabSize = 4)
    {
        string code = StripComment(currentLine).Trim();
        if (!IsClosingOrContinuation(code))
        {
            return currentLine;
        }

        int depth = CalculateDepth(previousText);
        string indent = new(' ', Math.Max(0, depth - 1) * Math.Clamp(tabSize, 1, 12));
        return indent + currentLine.TrimStart();
    }

    public int CalculateDepth(string source)
    {
        int depth = 0;
        foreach (string raw in source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            string code = StripComment(raw).Trim();
            if (code.Length == 0)
            {
                continue;
            }
            if (IsClosingOrContinuation(code))
            {
                depth = Math.Max(0, depth - 1);
            }
            if (IsBlockOpening(code))
            {
                depth++;
            }
        }
        return depth;
    }

    public static bool IsBlockOpening(string code)
    {
        return code == "시작" ||
               code == "아니면" ||
               code.StartsWith("아니면만약 ", StringComparison.Ordinal) && code.EndsWith(" 이면", StringComparison.Ordinal) ||
               code.StartsWith("구조체 ", StringComparison.Ordinal) ||
               code.StartsWith("함수 ", StringComparison.Ordinal) ||
               code.StartsWith("만약 ", StringComparison.Ordinal) && code.EndsWith(" 이면", StringComparison.Ordinal) ||
               code.StartsWith("동안 ", StringComparison.Ordinal) && code.EndsWith(" 반복", StringComparison.Ordinal) ||
               code.StartsWith("반복 ", StringComparison.Ordinal) && code.Contains("부터", StringComparison.Ordinal) && code.EndsWith("까지", StringComparison.Ordinal);
    }

    public static bool IsClosingOrContinuation(string code)
    {
        return code == "끝" ||
               code == "아니면" ||
               code.StartsWith("아니면만약 ", StringComparison.Ordinal);
    }

    public static string StripComment(string line)
    {
        bool inString = false;
        bool inChar = false;
        bool escaped = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
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
            else if (ch == '#' && !inString && !inChar)
            {
                return line[..i];
            }
        }
        return line;
    }
}
