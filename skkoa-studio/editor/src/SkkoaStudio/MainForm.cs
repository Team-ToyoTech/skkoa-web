using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ScintillaNet.Abstractions.Enumerations;
using ScintillaNet.WinForms;
using SkkoaStudio.Core.Compiler;
using SkkoaStudio.Core.Debugging;
using SkkoaStudio.Core.Diagnostics;
using SkkoaStudio.Core.Formatting;
using SkkoaStudio.Core.Language;
using SkkoaStudio.Core.ProjectSystem;
using SkkoaStudio.Core.Settings;
using SkkoaStudio.Core.Updates;

namespace SkkoaStudio;

public partial class MainForm : Form
{
    private const int ErrorIndicator = 8;
    private const int WarningIndicator = 9;
    private const int BreakpointMarker = 0;
    private const int ErrorMarker = 1;
    private const int WarningMarker = 2;
    private const int CurrentLineMarker = 3;
    private const int GcsCompStr = 0x0008;
    private const int GcsResultStr = 0x0800;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private static readonly Regex AnsiEscapeRegex = new(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);

    private readonly SkkoaSettingsService settingsService;
    private readonly SkkoaCompilerService compilerService;
    private readonly SkkoaDiagnosticService diagnosticService;
    private readonly SkkoaProjectService projectService = new();
    private readonly SkkoaSyntaxHighlighter highlighter = new();
    private readonly SkkoaCompletionProvider completionProvider = new();
    private readonly SkkoaFormatter formatter = new();
    private readonly SkkoaUpdateService updateService = new();
    private readonly System.Windows.Forms.Timer diagnosticsTimer = new();
    private readonly ToolTip diagnosticsToolTip = new();
    private readonly HashSet<string> reportedEditorErrors = [];

    private bool watchListDrawingConfigured;
    private Color watchListSurfaceColor = ColorTranslator.FromHtml("#1b1b1f");
    private Color watchListHeaderColor = ColorTranslator.FromHtml("#24242a");
    private Color watchListBorderColor = ColorTranslator.FromHtml("#33333a");
    private Color watchListTextColor = ColorTranslator.FromHtml("#f2f2f2");
    private Color watchListAccentColor = ColorTranslator.FromHtml("#a259ff");

    private SkkoaStudioSettings settings;
    private SkkoaProject? currentProject;
    private SkkoaRunningProcess? runningProcess;
    private string? lastExecutablePath;
    private SkkoaDebugSession? debugSession;
    private CancellationTokenSource? diagnosticsCts;
    private CancellationTokenSource? buildCts;
    private int untitledCounter = 1;
    private string completionPrefix = "";
    private bool startupUpdateCheckStarted;

    private readonly string[] startupArgs;

    public MainForm(SkkoaSettingsService settingsService, string[]? startupArgs = null)
    {
        this.settingsService = settingsService;
        this.startupArgs = startupArgs ?? [];
        settings = settingsService.Load();
        compilerService = new SkkoaCompilerService
        {
            CompilerPath = settings.CompilerPath,
            LibPath = settings.LibPath
        };
        diagnosticService = new SkkoaDiagnosticService(compilerService);

        InitializeComponent();
        ConfigureRootSplitters();
        Icon = LoadAppIcon();
        editorTabs.DrawMode = System.Windows.Forms.TabDrawMode.OwnerDrawFixed;
        editorTabs.DrawItem += EditorTabs_DrawItem;
        editorTabs.ControlAdded += (_, _) => ScheduleEditorTabHeaderFillUpdate();
        editorTabs.ControlRemoved += (_, _) => ScheduleEditorTabHeaderFillUpdate();
        editorTabs.SizeChanged += (_, _) => UpdateEditorTabHeaderFill();
        editorTabs.SelectedIndexChanged += (_, _) => ScheduleEditorTabHeaderFillUpdate();
        editorOutputSplit.Panel1.SizeChanged += (_, _) => UpdateEditorTabHeaderFill();
        Shown += (_, _) => ScheduleEditorTabHeaderFillUpdate();
        bottomTabs.DrawMode = System.Windows.Forms.TabDrawMode.OwnerDrawFixed;
        bottomTabs.DrawItem += BottomTabs_DrawItem;
        bottomTabs.ControlAdded += (_, _) => ScheduleBottomTabHeaderFillUpdate();
        bottomTabs.ControlRemoved += (_, _) => ScheduleBottomTabHeaderFillUpdate();
        bottomTabs.SizeChanged += (_, _) => UpdateBottomTabHeaderFill();
        bottomTabs.SelectedIndexChanged += (_, _) => ScheduleBottomTabHeaderFillUpdate();
        editorOutputSplit.Panel2.SizeChanged += (_, _) => UpdateBottomTabHeaderFill();
        Shown += (_, _) => ScheduleBottomTabHeaderFillUpdate();
        Shown += async (_, _) => await CheckForUpdatesOnStartupAsync();
        diagnosticsTimer.Interval = 420;
        diagnosticsTimer.Tick += async (_, _) => await RunDiagnosticsForActiveEditorAsync();

        UpdateRecentMenus();
        ApplySettingsToUi();
        OpenStartupArguments();
        if (editorTabs.TabPages.Count == 0)
        {
            NewFile();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyWindowFrameTheme();
    }

    private void ConfigureRootSplitters()
    {
        Shown += (_, _) => ApplyPreferredRootSplitterLayout();
        mainSplit.SizeChanged += (_, _) => ClampRootSplitterLayout();
        editorOutputSplit.SizeChanged += (_, _) => ClampRootSplitterLayout();
    }

    private void ApplyPreferredRootSplitterLayout()
    {
        SetSplitterDistanceSafe(mainSplit, desiredDistance: 230, panel1MinSize: 160, panel2MinSize: 360);
        int editorHeight = Math.Max(260, editorOutputSplit.Height - 220);
        SetSplitterDistanceSafe(editorOutputSplit, editorHeight, panel1MinSize: 260, panel2MinSize: 150);
    }

    private void ClampRootSplitterLayout()
    {
        SetSplitterDistanceSafe(mainSplit, mainSplit.SplitterDistance, panel1MinSize: 160, panel2MinSize: 360);
        SetSplitterDistanceSafe(editorOutputSplit, editorOutputSplit.SplitterDistance, panel1MinSize: 260, panel2MinSize: 150);
    }

    private void ConfigureNestedSplit(SplitContainer split, int desiredDistance, int panel1MinSize, int panel2MinSize)
    {
        split.HandleCreated += (_, _) => SetSplitterDistanceSafe(split, desiredDistance, panel1MinSize, panel2MinSize);
        split.SizeChanged += (_, _) => SetSplitterDistanceSafe(split, split.SplitterDistance, panel1MinSize, panel2MinSize);
    }

    private void SplitContainer_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not SplitContainer split || split.Panel1Collapsed || split.Panel2Collapsed)
        {
            return;
        }

        Rectangle splitterBounds = split.Orientation == Orientation.Vertical
            ? new Rectangle(split.SplitterDistance, 0, split.SplitterWidth, split.Height)
            : new Rectangle(0, split.SplitterDistance, split.Width, split.SplitterWidth);
        using SolidBrush brush = new(ThemeBorder());
        e.Graphics.FillRectangle(brush, splitterBounds);
    }

    private static void SetSplitterDistanceSafe(SplitContainer split, int desiredDistance, int panel1MinSize, int panel2MinSize)
    {
        if (split.Panel1Collapsed || split.Panel2Collapsed)
        {
            return;
        }

        int length = split.Orientation == Orientation.Vertical ? split.Width : split.Height;
        if (length <= 1)
        {
            return;
        }

        int safePanel1MinSize = panel1MinSize;
        int safePanel2MinSize = panel2MinSize;
        if (length < safePanel1MinSize + safePanel2MinSize)
        {
            safePanel1MinSize = 0;
            safePanel2MinSize = 0;
        }

        if (split.Panel1MinSize != safePanel1MinSize)
        {
            split.Panel1MinSize = safePanel1MinSize;
        }
        if (split.Panel2MinSize != safePanel2MinSize)
        {
            split.Panel2MinSize = safePanel2MinSize;
        }

        int minimum = split.Panel1MinSize;
        int maximum = length - split.Panel2MinSize;
        if (maximum < minimum)
        {
            return;
        }

        int distance = Math.Clamp(desiredDistance, minimum, maximum);
        if (split.SplitterDistance != distance)
        {
            split.SplitterDistance = distance;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!ConfirmSaveAllTabs())
        {
            e.Cancel = true;
            return;
        }
        runningProcess?.Stop();
        runningProcess?.Dispose();
        diagnosticsTimer.Dispose();
        diagnosticsToolTip.Dispose();
        diagnosticsCts?.Cancel();
        diagnosticsCts?.Dispose();
        buildCts?.Cancel();
        buildCts?.Dispose();
        settingsService.Save(settings);
        base.OnFormClosing(e);
    }

    private void NewFile()
    {
        string name = $"untitled{untitledCounter++}.koa";
        CreateEditorTab(new SkkoaDocument { DisplayName = name, Text = "", IsDirty = false });
    }

    private void OpenFile()
    {
        using OpenFileDialog dialog = new()
        {
            Filter = "SKKOA files (*.koa)|*.koa|All files (*.*)|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        foreach (string path in dialog.FileNames)
        {
            OpenFile(path);
        }
    }

    private void OpenFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        foreach (TabPage page in editorTabs.TabPages)
        {
            if (page.Tag is EditorTabState existing &&
                existing.Document.FilePath?.Equals(path, StringComparison.OrdinalIgnoreCase) == true)
            {
                editorTabs.SelectedTab = page;
                return;
            }
        }

        try
        {
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                ShowError("파일을 찾을 수 없습니다: " + fullPath);
                return;
            }

            string text = File.ReadAllText(fullPath, Encoding.UTF8);
            CreateEditorTab(new SkkoaDocument
            {
                FilePath = fullPath,
                DisplayName = Path.GetFileName(fullPath),
                Text = text,
                IsDirty = false
            });
            settingsService.AddRecentFile(settings, fullPath);
            UpdateRecentMenus();
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            ShowError("파일을 열 수 없습니다." + Environment.NewLine + ex.Message);
        }
    }

    private bool SaveFile()
    {
        EditorTabState? state = CurrentState();
        if (state == null)
        {
            return false;
        }
        if (state.Document.IsUntitled)
        {
            return SaveFileAs();
        }
        SaveState(state, state.Document.FilePath!);
        return true;
    }

    private bool SaveFileAs()
    {
        EditorTabState? state = CurrentState();
        if (state == null)
        {
            return false;
        }
        using SaveFileDialog dialog = new()
        {
            Filter = "SKKOA files (*.koa)|*.koa|All files (*.*)|*.*",
            FileName = state.Document.DisplayName.EndsWith(".koa", StringComparison.OrdinalIgnoreCase)
                ? state.Document.DisplayName
                : state.Document.DisplayName + ".koa"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return false;
        }
        SaveState(state, dialog.FileName);
        return true;
    }

    private void SaveState(EditorTabState state, string path)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
            if (settings.FormatOnSave)
            {
                state.Editor.Text = formatter.FormatDocument(state.Editor.Text, settings.TabSize);
            }
            File.WriteAllText(fullPath, state.Editor.Text, Encoding.UTF8);
            state.Document.FilePath = fullPath;
            state.Document.DisplayName = Path.GetFileName(fullPath);
            state.Document.Text = state.Editor.Text;
            state.Document.IsDirty = false;
            UpdateTabTitle(state);
            settingsService.AddRecentFile(settings, fullPath);
            UpdateRecentMenus();
            buildStatus.Text = "Saved";
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            ShowError("파일을 저장할 수 없습니다." + Environment.NewLine + ex.Message);
            buildStatus.Text = "Save failed";
        }
    }

    private void CloseActiveTab()
    {
        if (!ConfirmSaveActiveTab())
        {
            return;
        }
        if (editorTabs.SelectedTab != null)
        {
            TabPage page = editorTabs.SelectedTab;
            editorTabs.TabPages.Remove(page);
            page.Dispose();
            ScheduleEditorTabHeaderFillUpdate();
        }
        if (editorTabs.TabPages.Count == 0)
        {
            NewFile();
        }
    }

    private bool ConfirmSaveActiveTab()
    {
        EditorTabState? state = CurrentState();
        if (state == null || !state.Document.IsDirty)
        {
            return true;
        }
        DialogResult result = MessageBox.Show(
            this,
            $"{state.Document.DisplayName} 파일을 저장하시겠습니까?",
            "SKKOA Studio",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);
        if (result == DialogResult.Cancel)
        {
            return false;
        }
        return result != DialogResult.Yes || SaveFile();
    }

    private bool ConfirmSaveAllTabs()
    {
        foreach (TabPage page in editorTabs.TabPages)
        {
            editorTabs.SelectedTab = page;
            if (!ConfirmSaveActiveTab())
            {
                return false;
            }
        }

        return true;
    }

    private void CreateEditorTab(SkkoaDocument document)
    {
        Scintilla editor;
        try
        {
            editor = new Scintilla
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Text = document.Text
            };
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            ShowError("에디터를 초기화할 수 없습니다." + Environment.NewLine + ex.Message);
            return;
        }
        EditorTabState state = new(document, editor);
        TabPage page = new()
        {
            Tag = state,
            UseVisualStyleBackColor = false,
            BackColor = ThemeBackground(),
            ForeColor = ThemeText(),
            Padding = Padding.Empty
        };
        state.TabPage = page;
        page.Controls.Add(editor);
        editorTabs.TabPages.Add(page);
        editorTabs.SelectedTab = page;
        ScheduleEditorTabHeaderFillUpdate();

        ConfigureEditor(editor);
        ApplyEditorTheme(editor);
        ApplyHighlight(editor);
        UpdateTabTitle(state);

        editor.TextChanged += (_, _) =>
        {
            state.Document.Text = editor.Text;
            state.Document.IsDirty = true;
            UpdateTabTitle(state);
            ApplyHighlight(editor);
            ScheduleDiagnostics();
        };
        editor.KeyDown += Editor_KeyDown;
        editor.MouseDown += Editor_MouseDown;
        editor.MouseMove += Editor_MouseMove;
        editor.UpdateUi += (_, _) =>
        {
            UpdateStatus();
            HighlightBraces(editor);
        };
    }

    private void ConfigureEditor(Scintilla editor)
    {
        TryEditorAction("editor settings", () =>
        {
            editor.TabWidth = Math.Clamp(settings.TabSize, 1, 12);
            editor.UseTabs = !settings.InsertSpaces;
            editor.WrapMode = ScintillaNet.Abstractions.Enumerations.WrapMode.None;
            editor.IndentationGuides = IndentView.LookBoth;
            editor.CaretLineVisible = true;
            editor.AutoCSeparator = '\n';
            editor.AutoCIgnoreCase = false;
        });

        TryEditorAction("editor margins", () =>
        {
            dynamic margin0 = editor.Margins[0];
            margin0.Type = MarginType.Number;
            margin0.Width = 48;
            dynamic margin1 = editor.Margins[1];
            margin1.Type = MarginType.Symbol;
            margin1.Width = 22;
            margin1.Sensitive = true;
            margin1.Mask = unchecked((int)0x0F);
        });

        TryEditorAction("editor markers", () =>
        {
            dynamic breakpoint = editor.Markers[BreakpointMarker];
            breakpoint.Symbol = MarkerSymbol.Circle;
            breakpoint.SetBackColor(Color.Firebrick);
            breakpoint.SetForeColor(PrimaryColor());

            dynamic error = editor.Markers[ErrorMarker];
            error.Symbol = MarkerSymbol.ShortArrow;
            error.SetBackColor(Color.Firebrick);
            error.SetForeColor(Color.White);

            dynamic warning = editor.Markers[WarningMarker];
            warning.Symbol = MarkerSymbol.ShortArrow;
            warning.SetBackColor(Color.Goldenrod);
            warning.SetForeColor(Color.Black);

            dynamic current = editor.Markers[CurrentLineMarker];
            current.Symbol = MarkerSymbol.ShortArrow;
            current.SetBackColor(Color.DodgerBlue);
            current.SetForeColor(Color.White);
        });

        TryEditorAction("editor indicators", () =>
        {
            dynamic errorIndicator = editor.Indicators[ErrorIndicator];
            errorIndicator.Style = IndicatorStyle.Squiggle;
            errorIndicator.ForeColor = Color.Firebrick;

            dynamic warningIndicator = editor.Indicators[WarningIndicator];
            warningIndicator.Style = IndicatorStyle.Dash;
            warningIndicator.ForeColor = Color.Goldenrod;
        });
    }

    private void ApplyEditorTheme(Scintilla editor)
    {
        bool dark = settings.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);
        Color back = ThemeEditorBackground();
        Color fore = ThemeEditorText();
        Color margin = dark ? ColorTranslator.FromHtml("#1b1b1f") : ColorTranslator.FromHtml("#eef1f7");
        Color currentLine = dark ? ColorTranslator.FromHtml("#22222a") : ColorTranslator.FromHtml("#edf1ff");

        TryEditorAction("editor theme", () =>
        {
            editor.StyleResetDefault();
            for (int i = 0; i < 34; i++)
            {
                dynamic style = editor.Styles[i];
                style.Font = settings.FontFamily;
                style.SizeF = settings.FontSize;
                style.BackColor = back;
                style.ForeColor = fore;
            }
            editor.StyleClearAll();

            SetStyle(editor, SkkoaHighlightStyle.Keyword, dark ? Color.FromArgb(86, 156, 214) : Color.FromArgb(0, 73, 135), bold: true);
            SetStyle(editor, SkkoaHighlightStyle.BlockKeyword, dark ? Color.FromArgb(215, 186, 125) : Color.FromArgb(118, 79, 0), bold: true);
            SetStyle(editor, SkkoaHighlightStyle.DeclarationKeyword, dark ? Color.FromArgb(78, 201, 176) : Color.FromArgb(15, 111, 104), bold: true);
            SetStyle(editor, SkkoaHighlightStyle.ConditionalKeyword, dark ? Color.FromArgb(197, 134, 192) : Color.FromArgb(126, 55, 130), bold: true);
            SetStyle(editor, SkkoaHighlightStyle.LoopKeyword, dark ? Color.FromArgb(206, 145, 120) : Color.FromArgb(159, 76, 0), bold: true);
            SetStyle(editor, SkkoaHighlightStyle.IoKeyword, dark ? Color.FromArgb(79, 193, 255) : Color.FromArgb(0, 95, 153), bold: true);
            SetStyle(editor, SkkoaHighlightStyle.FunctionKeyword, dark ? Color.FromArgb(220, 220, 170) : Color.FromArgb(121, 94, 38), bold: true);
            SetStyle(editor, SkkoaHighlightStyle.ImportKeyword, dark ? Color.FromArgb(181, 206, 168) : Color.FromArgb(76, 109, 28), bold: true);
            SetStyle(editor, SkkoaHighlightStyle.LogicalKeyword, dark ? Color.FromArgb(156, 220, 254) : Color.FromArgb(45, 92, 156), bold: true);
            SetStyle(editor, SkkoaHighlightStyle.Type, dark ? Color.FromArgb(78, 201, 176) : Color.FromArgb(38, 127, 153), bold: true);
            SetStyle(editor, SkkoaHighlightStyle.Literal, dark ? Color.FromArgb(181, 206, 168) : Color.FromArgb(9, 134, 88));
            SetStyle(editor, SkkoaHighlightStyle.StandardFunction, dark ? Color.FromArgb(255, 207, 110) : Color.FromArgb(121, 94, 38));
            SetStyle(editor, SkkoaHighlightStyle.String, dark ? Color.FromArgb(206, 145, 120) : Color.FromArgb(163, 21, 21));
            SetStyle(editor, SkkoaHighlightStyle.Character, dark ? Color.FromArgb(206, 145, 120) : Color.FromArgb(163, 21, 21));
            SetStyle(editor, SkkoaHighlightStyle.Number, dark ? Color.FromArgb(181, 206, 168) : Color.FromArgb(9, 134, 88));
            SetStyle(editor, SkkoaHighlightStyle.Comment, dark ? Color.FromArgb(106, 153, 85) : Color.FromArgb(0, 128, 0));
            SetStyle(editor, SkkoaHighlightStyle.Operator, dark ? Color.FromArgb(197, 134, 192) : Color.FromArgb(0, 0, 128));
            SetStyle(editor, SkkoaHighlightStyle.Brace, dark ? Color.FromArgb(215, 186, 125) : Color.FromArgb(121, 94, 38));
            SetStyle(editor, SkkoaHighlightStyle.FunctionName, dark ? Color.FromArgb(220, 220, 170) : Color.FromArgb(121, 94, 38));
            SetStyle(editor, SkkoaHighlightStyle.StructName, dark ? Color.FromArgb(78, 201, 176) : Color.FromArgb(38, 127, 153), bold: true);
            SetStyle(editor, SkkoaHighlightStyle.VariableName, fore);

            editor.CaretForeColor = dark ? Color.White : Color.Black;
            editor.CaretLineBackColor = currentLine;
            editor.SetSelectionBackColor(true, dark ? ColorTranslator.FromHtml("#3a2f4f") : ColorTranslator.FromHtml("#d8c7ff"));
            editor.BackColor = back;
            editor.ForeColor = fore;

            dynamic lineStyle = editor.Styles[33];
            lineStyle.BackColor = margin;
            lineStyle.ForeColor = dark ? Color.FromArgb(135, 140, 150) : Color.FromArgb(102, 102, 116);
        });
        ApplyHighlight(editor);
    }

    private static void SetStyle(Scintilla editor, SkkoaHighlightStyle styleId, Color color, bool bold = false)
    {
        dynamic style = editor.Styles[(int)styleId];
        style.ForeColor = color;
        style.Bold = bold;
    }

    private void ApplyHighlight(Scintilla editor)
    {
        TryEditorAction("syntax highlight", () =>
        {
            string text = editor.Text;
            editor.ClearDocumentStyle();
            foreach (SkkoaHighlightSpan span in highlighter.GetSpans(text))
            {
                if (span.Start < 0 || span.Start >= text.Length)
                {
                    continue;
                }
                editor.StartStyling(span.Start);
                editor.SetStyling(Math.Min(span.Length, text.Length - span.Start), (int)span.Style);
            }
        });
        EditorTabState? state = StateForEditor(editor);
        ApplyDiagnostics(state);
        ApplyBreakpointMarkers(state);
    }

    private void ScheduleDiagnostics()
    {
        if (!settings.DiagnosticsOnType)
        {
            return;
        }
        diagnosticsTimer.Stop();
        diagnosticsTimer.Start();
    }

    private async Task RunDiagnosticsForActiveEditorAsync()
    {
        diagnosticsTimer.Stop();
        EditorTabState? state = CurrentState();
        if (state == null)
        {
            return;
        }

        diagnosticsCts?.Cancel();
        diagnosticsCts = new CancellationTokenSource();
        CancellationToken token = diagnosticsCts.Token;

        try
        {
            IReadOnlyList<SkkoaDiagnostic> diagnostics = await diagnosticService.GetDiagnosticsForTextAsync(
                state.Editor.Text,
                state.Document.FilePath ?? state.Document.DisplayName,
                GetWorkingDirectory(state),
                GetLibPath(),
                token);

            if (token.IsCancellationRequested)
            {
                return;
            }
            state.Diagnostics = diagnostics;
            ApplyDiagnostics(state);
            if (ReferenceEquals(CurrentState(), state))
            {
                FillProblems(state);
            }
            UpdateStatus();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            state.Diagnostics =
            [
                new SkkoaDiagnostic
                {
                    Severity = "error",
                    Code = "SKKDIAG",
                    Message = "실시간 진단 실행 중 오류가 발생했습니다: " + ex.Message,
                    File = state.Document.FilePath ?? state.Document.DisplayName,
                    Line = 1,
                    Column = 1,
                    Length = 1
                }
            ];
            ApplyDiagnostics(state);
            if (ReferenceEquals(CurrentState(), state))
            {
                FillProblems(state);
            }
            UpdateStatus();
        }
    }

    private void ApplyDiagnostics(EditorTabState? state)
    {
        if (state == null)
        {
            return;
        }
        TryEditorAction("diagnostic markers", () =>
        {
            Scintilla editor = state.Editor;
            editor.IndicatorCurrent = ErrorIndicator;
            editor.IndicatorClearRange(0, editor.TextLength);
            editor.IndicatorCurrent = WarningIndicator;
            editor.IndicatorClearRange(0, editor.TextLength);
            editor.MarkerDeleteAll(ErrorMarker);
            editor.MarkerDeleteAll(WarningMarker);

            foreach (SkkoaDiagnostic diagnostic in state.Diagnostics)
            {
                int position = PositionFromLineColumn(editor.Text, diagnostic.Line, diagnostic.Column);
                int length = Math.Max(1, diagnostic.Length);
                int indicatorLength = Math.Min(length, Math.Max(0, editor.TextLength - position));
                if (indicatorLength > 0)
                {
                    editor.IndicatorCurrent = diagnostic.IsWarning ? WarningIndicator : ErrorIndicator;
                    editor.IndicatorFillRange(position, indicatorLength);
                }
                if (diagnostic.Line > 0 && diagnostic.Line <= editor.Lines.Count)
                {
                    dynamic line = editor.Lines[diagnostic.Line - 1];
                    line.MarkerAdd(diagnostic.IsWarning ? WarningMarker : ErrorMarker);
                }
            }
        });
    }

    private void FillProblems(EditorTabState state)
    {
        problemsGrid.Rows.Clear();
        foreach (SkkoaDiagnostic diagnostic in state.Diagnostics)
        {
            problemsGrid.Rows.Add(diagnostic.Severity, diagnostic.Code, diagnostic.Message, diagnostic.File, diagnostic.Line, diagnostic.Column);
        }
    }

    private async Task CheckCurrentAsync()
    {
        try
        {
            if (!SaveFile())
            {
                return;
            }
            EditorTabState? state = CurrentState();
            if (state == null || state.Document.FilePath == null)
            {
                return;
            }

            bottomTabs.SelectedIndex = 1;
            outputBox.Clear();
            buildStatus.Text = "Checking";
            SkkoaCompileResult result = await compilerService.CheckAsync(new SkkoaCompileOptions
            {
                SourcePath = state.Document.FilePath,
                WorkingDirectory = GetWorkingDirectory(state),
                LibPath = GetLibPath(),
                CheckOnly = true,
                DiagnosticsJson = true
            }, AppendOutput, AppendOutput);

            state.Diagnostics = result.Diagnostics;
            ApplyDiagnostics(state);
            FillProblems(state);
            AppendOutput(result.StandardError);
            buildStatus.Text = result.Success ? "Check succeeded" : "Check failed";
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            AppendOutput("Check 실패: " + ex.Message + Environment.NewLine);
            buildStatus.Text = "Check failed";
        }
    }

    private async Task<SkkoaCompileResult?> CompileCurrentAsync()
    {
        try
        {
            if (!SaveFile())
            {
                return null;
            }
            EditorTabState? state = CurrentState();
            if (state == null || state.Document.FilePath == null)
            {
                return null;
            }

            bottomTabs.SelectedIndex = 1;
            outputBox.Clear();
            buildStatus.Text = "Compiling";
            buildCts?.Cancel();
            buildCts?.Dispose();
            buildCts = new CancellationTokenSource();

            string buildDir = Path.Combine(Path.GetTempPath(), "skkoa-studio-build", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(buildDir);
            string? outputName = currentProject?.OutputName;
            if (string.IsNullOrWhiteSpace(outputName))
            {
                outputName = Path.GetFileNameWithoutExtension(state.Document.FilePath);
            }
            string exePath = Path.Combine(buildDir, SanitizeFileName(outputName) + ".exe");

            IReadOnlyList<string> missing = await compilerService.FindMissingRuntimeToolsAsync(buildCts.Token);
            if (missing.Count > 0)
            {
                AppendOutput("누락된 빌드 도구: " + string.Join(", ", missing) + Environment.NewLine);
                AppendOutput("설치된 Studio 번들 도구가 누락되었습니다. Tools > Install/Repair Toolchain으로 복구할 수 있습니다." + Environment.NewLine);
            }

            SkkoaCompileResult result = await compilerService.CompileAsync(new SkkoaCompileOptions
            {
                SourcePath = GetEntryFilePath(state),
                OutputPath = exePath,
                WorkingDirectory = GetWorkingDirectory(state),
                LibPath = GetLibPath()
            }, AppendOutput, AppendOutput, buildCts.Token);

            AppendOutput(result.StandardError);
            if (result.ExitCode == 0 && File.Exists(exePath))
            {
                lastExecutablePath = exePath;
                AppendOutput("실행 파일: " + exePath + Environment.NewLine);
                buildStatus.Text = "Compile succeeded";
            }
            else
            {
                buildStatus.Text = "Compile failed";
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            buildStatus.Text = "Compile canceled";
            return null;
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            AppendOutput("Compile 실패: " + ex.Message + Environment.NewLine);
            buildStatus.Text = "Compile failed";
            return null;
        }
    }

    private async Task RunCurrentAsync()
    {
        SkkoaCompileResult? compile = await CompileCurrentAsync();
        if (compile == null || compile.ExitCode != 0 || string.IsNullOrWhiteSpace(lastExecutablePath))
        {
            return;
        }
        StartExecutable(lastExecutablePath);
    }

    private void StartExecutable(string executablePath)
    {
        try
        {
            if (!File.Exists(executablePath))
            {
                ShowError("실행 파일을 찾을 수 없습니다: " + executablePath);
                return;
            }
            StopActiveProcess();
            bottomTabs.SelectedIndex = 2;
            consoleBox.Clear();
            consoleInputBox.Clear();
            consoleBox.AppendText("실행 파일: " + executablePath + Environment.NewLine);
            buildStatus.Text = "Running";
            SkkoaRunningProcess? process = null;
            process = compilerService.RunExecutable(
                executablePath,
                Path.GetDirectoryName(executablePath),
                AppendConsole,
                AppendConsole,
                (exitCode, elapsed) =>
                {
                    SafeBeginInvoke(() =>
                    {
                        if (!ReferenceEquals(runningProcess, process))
                        {
                            process?.Dispose();
                            return;
                        }
                        AppendConsole($"종료 코드: {exitCode}, 실행 시간: {elapsed.TotalMilliseconds:0}ms{Environment.NewLine}");
                        buildStatus.Text = "Run finished";
                        runningProcess?.Dispose();
                        runningProcess = null;
                    });
                });
            runningProcess = process;
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            AppendConsole("실행 실패: " + ex.Message + Environment.NewLine);
            buildStatus.Text = "Run failed";
        }
    }

    private void StopActiveProcess()
    {
        runningProcess?.Stop();
        runningProcess?.Dispose();
        runningProcess = null;
        StopDebugging();
        buildStatus.Text = "Stopped";
    }

    private void SendConsoleInput()
    {
        string text = consoleInputBox.Text;
        consoleInputBox.Clear();
        if (debugSession != null && debugSession.State == SkkoaDebugState.WaitingForInput)
        {
            UpdateDebugUi(debugSession.QueueInput(text));
            return;
        }
        if (runningProcess == null)
        {
            AppendConsole("실행 중인 프로세스가 없습니다." + Environment.NewLine);
            return;
        }
        if (runningProcess.SendInput(text))
        {
            AppendConsole("> " + text + Environment.NewLine);
        }
        else
        {
            AppendConsole("입력을 보낼 수 없습니다. 프로세스가 종료되었거나 표준 입력이 닫혔습니다." + Environment.NewLine);
        }
    }

    private void ConsoleInputBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            SendConsoleInput();
        }
    }

    private void StartDebugging()
    {
        EditorTabState? state = CurrentState();
        if (state == null)
        {
            return;
        }
        debugSession = new SkkoaDebugSession(state.Editor.Text, state.Breakpoints.Select(line => line + 1));
        bottomTabs.SelectedIndex = 3;
        ClearCurrentLineMarkers();
        UpdateDebugUi(debugSession.Start());
    }

    private void ContinueDebugging()
    {
        if (debugSession == null)
        {
            StartDebugging();
            return;
        }
        UpdateDebugUi(debugSession.Continue());
    }

    private void StepIntoDebug()
    {
        if (debugSession == null)
        {
            StartDebugging();
            return;
        }
        UpdateDebugUi(debugSession.StepInto());
    }

    private void StepOverDebug()
    {
        if (debugSession == null)
        {
            StartDebugging();
            return;
        }
        UpdateDebugUi(debugSession.StepOver());
    }

    private void StepOutDebug()
    {
        if (debugSession == null)
        {
            return;
        }
        UpdateDebugUi(debugSession.StepOut());
    }

    private void StopDebugging()
    {
        if (debugSession == null)
        {
            return;
        }
        UpdateDebugUi(debugSession.Stop());
        debugSession = null;
        ClearCurrentLineMarkers();
        UpdateDebugToolbarState(null);
    }

    private void UpdateDebugUi(SkkoaDebugSnapshot snapshot)
    {
        watchList.Items.Clear();
        foreach ((string name, string value) in snapshot.Variables)
        {
            watchList.Items.Add(new ListViewItem([name, value]));
        }
        debugBox.Text =
            "State: " + snapshot.State + Environment.NewLine +
            "Line: " + (snapshot.CurrentLine?.ToString() ?? "-") + Environment.NewLine +
            "Call Stack:" + Environment.NewLine +
            string.Join(Environment.NewLine, snapshot.CallStack.Select(item => "  " + item)) +
            Environment.NewLine + Environment.NewLine +
            "Output:" + Environment.NewLine + snapshot.Output +
            (string.IsNullOrWhiteSpace(snapshot.Message) ? "" : Environment.NewLine + snapshot.Message);

        consoleBox.Text = snapshot.Output;
        ClearCurrentLineMarkers();
        if (snapshot.CurrentLine.HasValue)
        {
            MarkCurrentLine(snapshot.CurrentLine.Value);
        }
        buildStatus.Text = "Debug " + snapshot.State;
        UpdateDebugToolbarState(snapshot.State);
    }

    private void UpdateDebugToolbarState(SkkoaDebugState? snapshotState)
    {
        if (debugToolStrip == null)
        {
            return;
        }

        SkkoaDebugState? state = snapshotState ?? debugSession?.State;
        bool active = debugSession != null;
        bool canStep = active && state == SkkoaDebugState.Paused;

        debugToolStrip.Visible = active;
        debugToolStrip.Enabled = active;
        debugContinueButton.Enabled = canStep;
        debugStepButton.Enabled = canStep;
        debugStepOverButton.Enabled = canStep;
        debugStepOutButton.Enabled = canStep;
        debugStopButton.Enabled = active;
    }

    private void MarkCurrentLine(int oneBasedLine)
    {
        Scintilla? editor = CurrentEditor();
        if (editor == null || oneBasedLine < 1 || oneBasedLine > editor.Lines.Count)
        {
            return;
        }
        TryEditorAction("current debug line", () =>
        {
            dynamic line = editor.Lines[oneBasedLine - 1];
            line.MarkerAdd(CurrentLineMarker);
            int position = PositionFromLineColumn(editor.Text, oneBasedLine, 1);
            editor.GotoPosition(position);
        });
    }

    private void ClearCurrentLineMarkers()
    {
        Scintilla? editor = CurrentEditor();
        if (editor != null)
        {
            TryEditorAction("clear current debug line", () => editor.MarkerDeleteAll(CurrentLineMarker));
        }
    }

    private void FormatDocument()
    {
        Scintilla? editor = CurrentEditor();
        if (editor == null)
        {
            return;
        }
        int position = editor.CurrentPosition;
        TryEditorAction("format document", () =>
        {
            editor.Text = formatter.FormatDocument(editor.Text, settings.TabSize);
            editor.GotoPosition(Math.Min(position, editor.TextLength));
        });
    }

    private void FormatSelection()
    {
        Scintilla? editor = CurrentEditor();
        if (editor == null)
        {
            return;
        }
        int start = editor.SelectionStart;
        int end = editor.SelectionEnd;
        if (start == end)
        {
            return;
        }
        TryEditorAction("format selection", () =>
        {
            string selected = editor.GetTextRange(start, end - start);
            string formatted = formatter.FormatSelection(selected, 0, settings.TabSize);
            editor.SetTargetRange(start, end);
            editor.ReplaceTarget(formatted);
        });
    }

    private void ToggleComment()
    {
        Scintilla? editor = CurrentEditor();
        if (editor == null)
        {
            return;
        }
        string[] lines = editor.Text.Replace("\r\n", "\n").Split('\n');
        int currentLine = editor.CurrentLine;
        if (currentLine < 0 || currentLine >= lines.Length)
        {
            return;
        }
        string line = lines[currentLine];
        int indentLength = line.TakeWhile(char.IsWhiteSpace).Count();
        string trimmed = line[indentLength..];
        lines[currentLine] = trimmed.StartsWith("#", StringComparison.Ordinal)
            ? line[..indentLength] + trimmed[1..].TrimStart()
            : line[..indentLength] + "# " + trimmed;
        TryEditorAction("toggle comment", () => editor.Text = string.Join(Environment.NewLine, lines));
    }

    private void FindText()
    {
        string? query = Prompt("Find", "찾을 텍스트:");
        if (string.IsNullOrEmpty(query))
        {
            return;
        }
        FindNext(query);
    }

    private void FindAllText()
    {
        Scintilla? editor = CurrentEditor();
        string? query = Prompt("Find All", "찾을 텍스트:");
        if (editor == null || string.IsNullOrEmpty(query))
        {
            return;
        }
        outputBox.Clear();
        bottomTabs.SelectedIndex = 1;
        string[] lines = editor.Text.Split(["\r\n", "\n"], StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(query, StringComparison.Ordinal))
            {
                outputBox.AppendText($"{i + 1}: {lines[i]}{Environment.NewLine}");
            }
        }
    }

    private void ReplaceText()
    {
        string? query = Prompt("Replace", "찾을 텍스트:");
        if (string.IsNullOrEmpty(query))
        {
            return;
        }
        string? replacement = Prompt("Replace", "바꿀 텍스트:");
        if (replacement == null)
        {
            return;
        }
        Scintilla? editor = CurrentEditor();
        if (editor == null)
        {
            return;
        }
        TryEditorAction("replace", () => editor.Text = editor.Text.Replace(query, replacement, StringComparison.Ordinal));
    }

    private void FindNext(string query)
    {
        Scintilla? editor = CurrentEditor();
        if (editor == null)
        {
            return;
        }
        int start = Math.Min(editor.CurrentPosition + 1, editor.Text.Length);
        int index = editor.Text.IndexOf(query, start, StringComparison.Ordinal);
        if (index < 0)
        {
            index = editor.Text.IndexOf(query, StringComparison.Ordinal);
        }
        if (index >= 0)
        {
            TryEditorAction("find", () =>
            {
                editor.SetSelection(index, index + query.Length);
                editor.GotoPosition(index);
            });
        }
    }

    private void ShowCompletion(bool force)
    {
        Scintilla? editor = CurrentEditor();
        if (editor == null)
        {
            return;
        }
        if (IsImeCompositionActive(editor))
        {
            completionList.Visible = false;
            return;
        }

        int position = Math.Min(editor.CurrentPosition, editor.Text.Length);
        (int _, completionPrefix) = GetCompletionPrefix(editor.Text, position);
        IReadOnlyList<SkkoaCompletionItem> items = completionProvider.GetCompletions(completionPrefix);
        if (!force && (completionPrefix.Length == 0 || items.Count == 0))
        {
            completionList.Visible = false;
            return;
        }

        completionList.BeginUpdate();
        completionList.Items.Clear();
        foreach (SkkoaCompletionItem item in items)
        {
            completionList.Items.Add(item);
        }
        completionList.DisplayMember = nameof(SkkoaCompletionItem.DisplayText);
        completionList.EndUpdate();
        if (completionList.Items.Count == 0)
        {
            completionList.Visible = false;
            return;
        }
        completionList.SelectedIndex = 0;

        Control parent = editor.Parent ?? this;
        if (completionList.Parent != parent)
        {
            completionList.Parent?.Controls.Remove(completionList);
            parent.Controls.Add(completionList);
        }
        try
        {
            Point screen = editor.PointToScreen(new Point(editor.PointXFromPosition(position), editor.PointYFromPosition(position) + 20));
            completionList.Location = parent.PointToClient(screen);
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            completionList.Location = new Point(12, 12);
        }
        completionList.Visible = true;
        completionList.BringToFront();
        editor.Focus();
    }

    private void InsertSelectedCompletion()
    {
        if (!completionList.Visible || completionList.SelectedItem is not SkkoaCompletionItem item)
        {
            return;
        }
        Scintilla? editor = CurrentEditor();
        if (editor == null)
        {
            return;
        }
        if (IsImeCompositionActive(editor))
        {
            completionList.Visible = false;
            return;
        }

        int position = Math.Min(editor.CurrentPosition, editor.Text.Length);
        (int start, string currentPrefix) = GetCompletionPrefix(editor.Text, position);
        if (!string.IsNullOrEmpty(currentPrefix))
        {
            completionPrefix = currentPrefix;
        }
        else
        {
            start = Math.Max(0, position - completionPrefix.Length);
        }
        string rawInsertText = item.InsertText.Replace("\r\n", "\n");
        string insertText;
        string indent = GetCurrentLineIndent(editor);
        if (item.IsSnippet)
        {
            string[] snippetLines = rawInsertText.Split('\n');
            insertText = string.Join(Environment.NewLine, snippetLines.Select((line, index) => index == 0 ? line : indent + line));
        }
        else
        {
            insertText = rawInsertText.Replace("\n", Environment.NewLine);
        }
        TryEditorAction("insert completion", () =>
        {
            editor.SetTargetRange(start, position);
            editor.ReplaceTarget(insertText);
        });
        int newlinesBeforeCaret = rawInsertText.Take(item.CaretOffset).Count(ch => ch == '\n');
        int newlineExpansion = (Environment.NewLine.Length - 1) * newlinesBeforeCaret;
        int snippetIndentExpansion = item.IsSnippet ? indent.Length * newlinesBeforeCaret : 0;
        TryEditorAction("completion caret", () => editor.GotoPosition(start + item.CaretOffset + newlineExpansion + snippetIndentExpansion));
        completionList.Visible = false;
    }

    private void CompletionList_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            Scintilla? editor = CurrentEditor();
            if (editor != null && ShouldDeferCompletionEnter(editor))
            {
                completionList.Visible = false;
                return;
            }
            e.SuppressKeyPress = true;
            InsertSelectedCompletion();
        }
        else if (e.KeyCode == Keys.Tab)
        {
            e.SuppressKeyPress = true;
            InsertSelectedCompletion();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            completionList.Visible = false;
        }
    }

    private void Editor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (completionList.Visible && e.KeyCode == Keys.Enter)
        {
            Scintilla? editor = CurrentEditor();
            if (editor != null && ShouldDeferCompletionEnter(editor))
            {
                completionList.Visible = false;
            }
            else
            {
                e.SuppressKeyPress = true;
                InsertSelectedCompletion();
            }
            return;
        }
        if (completionList.Visible && e.KeyCode == Keys.Tab)
        {
            e.SuppressKeyPress = true;
            InsertSelectedCompletion();
            return;
        }
        if (completionList.Visible && e.KeyCode == Keys.Escape)
        {
            completionList.Visible = false;
            e.SuppressKeyPress = true;
            return;
        }
        if (e.Control && e.KeyCode == Keys.Space)
        {
            e.SuppressKeyPress = true;
            ShowCompletion(force: true);
            return;
        }
        if (e.KeyCode == Keys.Tab && settings.InsertSpaces)
        {
            e.SuppressKeyPress = true;
            Scintilla? editor = CurrentEditor();
            if (editor != null)
            {
                TryEditorAction("insert spaces", () => editor.ReplaceSelection(new string(' ', Math.Clamp(settings.TabSize, 1, 12))));
            }
            return;
        }
        if (e.KeyCode == Keys.Enter && settings.FormatOnEnter)
        {
            Scintilla? editor = CurrentEditor();
            if (editor != null)
            {
                e.SuppressKeyPress = true;
                string indent = formatter.GetIndentForNewLine(GetCurrentLineText(editor), settings.TabSize);
                TryEditorAction("insert newline", () => editor.ReplaceSelection(Environment.NewLine + indent));
            }
            return;
        }
        SafeBeginInvoke(() => ShowCompletion(force: false));
    }

    private void Editor_MouseDown(object? sender, MouseEventArgs e)
    {
        if (sender is not Scintilla editor || e.Button != MouseButtons.Left || e.X > 72)
        {
            return;
        }
        TryEditorAction("toggle breakpoint", () =>
        {
            int position = editor.CharPositionFromPointClose(e.X, e.Y);
            if (position < 0)
            {
                return;
            }
            int line = editor.LineFromPosition(position);
            EditorTabState? state = StateForEditor(editor);
            if (state == null)
            {
                return;
            }
            if (!state.Breakpoints.Add(line))
            {
                state.Breakpoints.Remove(line);
            }
            ApplyBreakpointMarkers(state);
        });
    }

    private void Editor_MouseMove(object? sender, MouseEventArgs e)
    {
        if (sender is not Scintilla editor)
        {
            return;
        }
        EditorTabState? state = StateForEditor(editor);
        if (state == null)
        {
            return;
        }
        TryEditorAction("diagnostic tooltip", () =>
        {
            int position = editor.CharPositionFromPointClose(e.X, e.Y);
            if (position < 0)
            {
                diagnosticsToolTip.Hide(editor);
                return;
            }
            int line = editor.LineFromPosition(position) + 1;
            int column = editor.GetColumn(position) + 1;
            SkkoaDiagnostic? diagnostic = state.Diagnostics.FirstOrDefault(d =>
                d.Line == line && column >= d.Column && column <= d.Column + Math.Max(1, d.Length));
            if (diagnostic != null)
            {
                diagnosticsToolTip.Show($"{diagnostic.Code}: {diagnostic.Message}", editor, e.Location.X + 12, e.Location.Y + 18, 3000);
            }
        });
    }

    private void ApplyBreakpointMarkers(EditorTabState? state)
    {
        if (state == null)
        {
            return;
        }
        TryEditorAction("breakpoint markers", () =>
        {
            state.Editor.MarkerDeleteAll(BreakpointMarker);
            foreach (int zeroBasedLine in state.Breakpoints)
            {
                if (zeroBasedLine >= 0 && zeroBasedLine < state.Editor.Lines.Count)
                {
                    dynamic line = state.Editor.Lines[zeroBasedLine];
                    line.MarkerAdd(BreakpointMarker);
                }
            }
        });
    }

    private void HighlightBraces(Scintilla editor)
    {
        TryEditorAction("brace highlight", () =>
        {
            int position = Math.Max(0, editor.CurrentPosition - 1);
            if (position >= editor.Text.Length || !"()[]".Contains(editor.Text[position]))
            {
                editor.BraceHighlight(-1, -1);
                return;
            }
            int match = editor.BraceMatch(position);
            if (match >= 0)
            {
                editor.BraceHighlight(position, match);
            }
            else
            {
                editor.BraceBadLight(position);
            }
        });
    }

    private void ProblemsGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }
        if (!int.TryParse(Convert.ToString(problemsGrid.Rows[e.RowIndex].Cells["Line"].Value), out int line) ||
            !int.TryParse(Convert.ToString(problemsGrid.Rows[e.RowIndex].Cells["Column"].Value), out int column))
        {
            return;
        }
        GoToLineColumn(line, column);
    }

    private void GoToLineColumn(int line, int column)
    {
        Scintilla? editor = CurrentEditor();
        if (editor == null)
        {
            return;
        }
        int position = PositionFromLineColumn(editor.Text, line, column);
        TryEditorAction("go to problem", () =>
        {
            editor.GotoPosition(position);
            editor.SetSelection(position, position);
            editor.Focus();
        });
    }

    private void NewProject()
    {
        try
        {
            using FolderBrowserDialog folder = new() { Description = "새 프로젝트 폴더를 선택하세요." };
            if (folder.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            string? name = Prompt("New Project", "프로젝트 이름:");
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }
            currentProject = projectService.Create(name.Trim(), Path.Combine(folder.SelectedPath, SanitizeFileName(name.Trim())));
            settingsService.AddRecentProject(settings, currentProject.ProjectPath!);
            UpdateRecentMenus();
            RefreshProjectTree();
            OpenFile(projectService.GetEntryPath(currentProject));
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            ShowError("프로젝트를 만들 수 없습니다." + Environment.NewLine + ex.Message);
        }
    }

    private void OpenProject()
    {
        using OpenFileDialog dialog = new()
        {
            Filter = "SKKOA project (*.skkoaproj)|*.skkoaproj|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        OpenProject(dialog.FileName);
    }

    private void OpenProject(string path)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                ShowError("프로젝트 파일을 찾을 수 없습니다: " + fullPath);
                return;
            }
            currentProject = projectService.Load(fullPath);
            settingsService.AddRecentProject(settings, fullPath);
            UpdateRecentMenus();
            RefreshProjectTree();
            string entryPath = projectService.GetEntryPath(currentProject);
            if (File.Exists(entryPath))
            {
                OpenFile(entryPath);
            }
            else
            {
                AppendOutput("진입 파일을 찾을 수 없습니다: " + entryPath + Environment.NewLine);
            }
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            ShowError("프로젝트를 열 수 없습니다." + Environment.NewLine + ex.Message);
        }
    }

    private void RefreshProjectTree()
    {
        projectTree.Nodes.Clear();
        if (currentProject == null)
        {
            projectTree.Nodes.Add("No project");
            return;
        }
        TreeNode root = new(currentProject.Name) { Tag = currentProject.ProjectPath };
        foreach (string file in currentProject.Files)
        {
            try
            {
                root.Nodes.Add(new TreeNode(file) { Tag = Path.GetFullPath(Path.Combine(currentProject.ProjectDirectory, file)) });
            }
            catch
            {
                root.Nodes.Add(new TreeNode(file));
            }
        }
        projectTree.Nodes.Add(root);
        root.Expand();
    }

    private void ProjectTree_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node.Tag is string path && File.Exists(path) && path.EndsWith(".koa", StringComparison.OrdinalIgnoreCase))
        {
            OpenFile(path);
        }
    }

    private void EditorTabs_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Middle)
        {
            return;
        }
        for (int i = 0; i < editorTabs.TabPages.Count; i++)
        {
            if (editorTabs.GetTabRect(i).Contains(e.Location))
            {
                editorTabs.SelectedIndex = i;
                CloseActiveTab();
                break;
            }
        }
    }

    private void ActiveEditorChanged()
    {
        completionList.Visible = false;
        EditorTabState? state = CurrentState();
        if (state == null)
        {
            problemsGrid.Rows.Clear();
        }
        else
        {
            FillProblems(state);
        }
        ApplyBreakpointMarkers(CurrentState());
        UpdateStatus();
    }

    private void ShowOptions()
    {
        using OptionsForm form = new(settings);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        settings = form.Settings;
        settingsService.Save(settings);
        compilerService.CompilerPath = settings.CompilerPath;
        compilerService.LibPath = settings.LibPath;
        ApplySettingsToUi();
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        if (startupUpdateCheckStarted)
        {
            return;
        }
        startupUpdateCheckStarted = true;

        if (!settings.CheckForUpdatesOnStartup)
        {
            return;
        }

        try
        {
            IReadOnlyList<Uri> manifestUris = SkkoaUpdateService.GetManifestUris(settings.UpdateManifestUrl);
            if (manifestUris.Count == 0)
            {
                return;
            }

            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(12));
            string currentVersion = SkkoaUpdateService.GetCurrentVersion();
            SkkoaUpdateCheckResult result = await updateService.CheckForUpdatesAsync(manifestUris, currentVersion, cts.Token);
            if (IsDisposed || !IsHandleCreated || !result.IsUpdateAvailable)
            {
                if (result.Errors.Count >= manifestUris.Count)
                {
                    AppendOutput("업데이트 확인 실패: " + result.Errors[0] + Environment.NewLine);
                }
                return;
            }

            buildStatus.Text = "Update available";
            if (ShowUpdatePrompt(result) == DialogResult.OK)
            {
                StartStudioUpdate(result);
            }
        }
        catch (OperationCanceledException)
        {
            AppendOutput("업데이트 확인 시간이 초과되었습니다." + Environment.NewLine);
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            AppendOutput("업데이트 확인 실패: " + ex.Message + Environment.NewLine);
        }
    }

    private DialogResult ShowUpdatePrompt(SkkoaUpdateCheckResult result)
    {
        SkkoaUpdateManifest manifest = result.Manifest ?? throw new InvalidOperationException("Update manifest is missing.");
        using Form form = new()
        {
            Text = "SKKOA Studio 업데이트",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(560, 270),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = ThemeBackground(),
            ForeColor = ThemeText(),
            Icon = Icon
        };

        Label title = new()
        {
            Text = "새 버전이 있습니다",
            Font = new Font(Font.FontFamily, 15, FontStyle.Bold),
            ForeColor = PrimaryColor(),
            Location = new Point(24, 22),
            AutoSize = true
        };
        string releaseNotes = string.IsNullOrWhiteSpace(manifest.ReleaseNotes)
            ? ""
            : Environment.NewLine + Environment.NewLine + manifest.ReleaseNotes.Trim();
        Label body = new()
        {
            Text = $"현재 버전: {result.CurrentVersion}{Environment.NewLine}새 버전: {manifest.Version}{Environment.NewLine}변경된 파일만 다운로드한 뒤 SKKOA Studio를 다시 시작합니다.{releaseNotes}",
            Location = new Point(26, 64),
            AutoSize = true,
            MaximumSize = new Size(500, 0)
        };
        Button update = new()
        {
            Text = "업데이트",
            DialogResult = DialogResult.OK,
            Location = new Point(338, 190),
            Width = 96
        };
        Button later = new()
        {
            Text = "나중에",
            DialogResult = DialogResult.Cancel,
            Location = new Point(440, 190),
            Width = 96
        };
        StyleButton(update, PrimaryColor(), PrimaryColor(), Color.White);
        StyleButton(later, ThemeSurfaceAlt(), ThemeBorder(), ThemeText());
        form.Controls.AddRange([title, body, update, later]);
        form.AcceptButton = update;
        form.CancelButton = later;
        return form.ShowDialog(this);
    }

    private void StartStudioUpdate(SkkoaUpdateCheckResult result)
    {
        if (result.ManifestUri == null)
        {
            return;
        }

        if (!ConfirmSaveAllTabs())
        {
            return;
        }

        try
        {
            string updaterPath = PrepareUpdaterCopy();
            string installDirectory = AppContext.BaseDirectory;
            ProcessStartInfo startInfo = new(updaterPath)
            {
                WorkingDirectory = Path.GetDirectoryName(updaterPath) ?? installDirectory,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("--manifest-url");
            startInfo.ArgumentList.Add(result.ManifestUri.ToString());
            startInfo.ArgumentList.Add("--install-dir");
            startInfo.ArgumentList.Add(installDirectory);
            startInfo.ArgumentList.Add("--current-pid");
            startInfo.ArgumentList.Add(Process.GetCurrentProcess().Id.ToString());
            startInfo.ArgumentList.Add("--restart");
            startInfo.ArgumentList.Add("SkkoaStudio.exe");

            Process.Start(startInfo);
            buildStatus.Text = "Updating";
            Close();
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            ShowError("업데이트를 시작할 수 없습니다." + Environment.NewLine + ex.Message);
        }
    }

    private static string PrepareUpdaterCopy()
    {
        string appDirectory = AppContext.BaseDirectory;
        string sourceUpdaterPath = Path.Combine(appDirectory, "SkkoaStudio.Updater.exe");
        if (!File.Exists(sourceUpdaterPath))
        {
            throw new FileNotFoundException("업데이트 관리자 실행 파일을 찾을 수 없습니다.", sourceUpdaterPath);
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "SKKOA Studio", "updater");
        CleanupOldUpdaterCopies(tempRoot);
        string targetDirectory = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(targetDirectory);

        foreach (string sourcePath in Directory.EnumerateFiles(appDirectory, "SkkoaStudio.Updater.*"))
        {
            CopyUpdaterDependency(sourcePath, targetDirectory);
        }
        CopyUpdaterDependency(Path.Combine(appDirectory, "SkkoaStudio.Core.dll"), targetDirectory, required: true);
        CopyUpdaterDependency(Path.Combine(appDirectory, "SkkoaStudio.Core.pdb"), targetDirectory, required: false);

        string targetUpdaterPath = Path.Combine(targetDirectory, "SkkoaStudio.Updater.exe");
        if (!File.Exists(targetUpdaterPath))
        {
            throw new FileNotFoundException("업데이트 관리자 복사본을 만들 수 없습니다.", targetUpdaterPath);
        }

        return targetUpdaterPath;
    }

    private static void CopyUpdaterDependency(string sourcePath, string targetDirectory, bool required = false)
    {
        if (!File.Exists(sourcePath))
        {
            if (required)
            {
                throw new FileNotFoundException("업데이트 관리자 의존 파일을 찾을 수 없습니다.", sourcePath);
            }
            return;
        }

        File.Copy(sourcePath, Path.Combine(targetDirectory, Path.GetFileName(sourcePath)), overwrite: true);
    }

    private static void CleanupOldUpdaterCopies(string tempRoot)
    {
        try
        {
            if (!Directory.Exists(tempRoot))
            {
                return;
            }

            foreach (string directory in Directory.GetDirectories(tempRoot))
            {
                try
                {
                    DirectoryInfo info = new(directory);
                    if (info.CreationTimeUtc < DateTime.UtcNow.AddDays(-2))
                    {
                        info.Delete(recursive: true);
                    }
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    private async Task InstallToolchainAsync()
    {
        try
        {
            bottomTabs.SelectedIndex = 1;
            outputBox.Clear();
            string script = Path.Combine(AppContext.BaseDirectory, "tools", "skkoa", "download", "skkoa-windows.ps1");
            if (!File.Exists(script))
            {
                script = Path.GetFullPath(Path.Combine("editor", "tools", "skkoa", "download", "skkoa-windows.ps1"));
            }
            string installRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SKKOA Studio", "skkoa");
            buildStatus.Text = "Installing toolchain";
            SkkoaToolchainInstaller installer = new();
            SkkoaProcessResult result = await installer.InstallAsync(script, installRoot, AppendOutput);
            buildStatus.Text = result.ExitCode == 0 ? "Toolchain installed" : "Toolchain install failed";
            string installedCompiler = Path.Combine(installRoot, "bin", "skkoa.exe");
            string installedLib = Path.Combine(installRoot, "lib");
            if (File.Exists(installedCompiler))
            {
                settings.CompilerPath = installedCompiler;
                compilerService.CompilerPath = installedCompiler;
            }
            if (Directory.Exists(installedLib))
            {
                settings.LibPath = installedLib;
                compilerService.LibPath = installedLib;
            }
            settingsService.Save(settings);
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            AppendOutput("Toolchain 설치 실패: " + ex.Message + Environment.NewLine);
            buildStatus.Text = "Toolchain install failed";
        }
    }

    private void OpenDocumentation()
    {
        try
        {
            string localDoc = Path.GetFullPath(Path.Combine("docs", "index.html"));
            Process.Start(new ProcessStartInfo
            {
                FileName = File.Exists(localDoc) ? localDoc : "https://skkoa.toyotech.dev/docs/",
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            ShowError("문서를 열 수 없습니다." + Environment.NewLine + ex.Message);
        }
    }

    private void CleanBuildOutput()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "skkoa-studio-build"));
        string tempRoot = Path.GetFullPath(Path.GetTempPath());
        try
        {
            if (root.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
            AppendOutput("빌드 임시 폴더를 정리했습니다." + Environment.NewLine);
        }
        catch (Exception ex)
        {
            AppendOutput("정리 실패: " + ex.Message + Environment.NewLine);
        }
    }

    private void SetTheme(string theme)
    {
        settings.Theme = theme;
        settingsService.Save(settings);
        ApplySettingsToUi();
    }

    private void ToggleTheme()
    {
        SetTheme(settings.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark");
    }

    private void ApplySettingsToUi()
    {
        bool dark = settings.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);
        Color primary = PrimaryColor();
        Color background = ThemeBackground();
        Color surface = ThemeSurface();
        Color surfaceAlt = ThemeSurfaceAlt();
        Color border = ThemeBorder();
        Color text = ThemeText();
        Color mutedText = ThemeMutedText();

        BackColor = background;
        ForeColor = text;
        ApplyToolStripTheme(menuStrip, background, text, primary, dark);
        ApplyToolStripTheme(toolStrip, background, text, primary, dark);
        ApplyToolStripTheme(debugToolStrip, surface, text, primary, dark);

        menuStrip.BackColor = background;
        menuStrip.ForeColor = text;
        toolStrip.BackColor = background;
        toolStrip.ForeColor = text;
        statusStrip.BackColor = background;
        statusStrip.ForeColor = mutedText;
        foreach (ToolStripItem item in statusStrip.Items)
        {
            item.ForeColor = mutedText;
        }
        ApplyMenuItemColors(menuStrip.Items, surface, surfaceAlt, text);

        mainSplit.BackColor = border;
        mainSplit.Panel1.BackColor = background;
        mainSplit.Panel2.BackColor = background;
        editorOutputSplit.BackColor = border;
        editorOutputSplit.Panel1.BackColor = background;
        editorOutputSplit.Panel2.BackColor = background;

        projectTree.BackColor = surface;
        projectTree.ForeColor = text;
        projectTree.LineColor = border;

        completionList.BackColor = surface;
        completionList.ForeColor = text;

        ApplyTabTheme(editorTabs, background, surface, text);
        ApplyTabTheme(bottomTabs, background, dark ? background : surface, text);

        outputBox.BackColor = consoleBox.BackColor = debugBox.BackColor = consoleInputBox.BackColor = ThemeEditorBackground();
        outputBox.ForeColor = consoleBox.ForeColor = debugBox.ForeColor = consoleInputBox.ForeColor = text;
        outputBox.BorderStyle = consoleBox.BorderStyle = debugBox.BorderStyle = consoleInputBox.BorderStyle = BorderStyle.FixedSingle;
        StyleButton(consoleSendButton, surfaceAlt, border, text);

        watchList.BackColor = surface;
        watchList.ForeColor = text;
        watchList.BorderStyle = BorderStyle.None;
        ConfigureWatchListDrawing(surface, surfaceAlt, border, text, primary);
        StyleProblemsGrid(background, surface, surfaceAlt, border, text, primary);
        UpdateDebugToolbarState(debugSession?.State);
        foreach (TabPage page in editorTabs.TabPages)
        {
            if (page.Tag is EditorTabState state)
            {
                ConfigureEditor(state.Editor);
                ApplyEditorTheme(state.Editor);
            }
        }
        Invalidate(true);
        ApplyWindowFrameTheme();
        UpdateStatus();
    }

    private static void ApplyToolStripTheme(ToolStrip strip, Color background, Color text, Color primary, bool dark)
    {
        strip.Renderer = new PrimaryToolStripRenderer(primary, dark);
        strip.BackColor = background;
        strip.ForeColor = text;
        foreach (ToolStripItem item in strip.Items)
        {
            item.ForeColor = text;
            item.BackColor = background;
        }
    }

    private static void ApplyMenuItemColors(ToolStripItemCollection items, Color surface, Color surfaceAlt, Color text)
    {
        foreach (ToolStripItem item in items)
        {
            item.ForeColor = text;
            item.BackColor = surface;
            if (item is ToolStripMenuItem menuItem)
            {
                menuItem.DropDown.BackColor = surface;
                menuItem.DropDown.ForeColor = text;
                menuItem.DropDown.Padding = Padding.Empty;
                foreach (ToolStripItem child in menuItem.DropDownItems)
                {
                    child.BackColor = surface;
                }
                ApplyMenuItemColors(menuItem.DropDownItems, surface, surfaceAlt, text);
            }
            else if (item is ToolStripSeparator separator)
            {
                separator.BackColor = surfaceAlt;
            }
        }
    }

    private void ApplyTabTheme(TabControl tabs, Color background, Color surface, Color text)
    {
        tabs.BackColor = background;
        if (tabs is DarkTabControl darkTabs)
        {
            darkTabs.DarkTheme = settings.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);
            darkTabs.AccentColor = PrimaryColor();
        }
        foreach (TabPage page in tabs.TabPages)
        {
            page.UseVisualStyleBackColor = false;
            page.BackColor = surface;
            page.ForeColor = text;
            page.Padding = Padding.Empty;
            page.Margin = Padding.Empty;
        }
        tabs.Invalidate();
        if (ReferenceEquals(tabs, editorTabs))
        {
            UpdateEditorTabHeaderFill();
        }
        if (ReferenceEquals(tabs, bottomTabs))
        {
            UpdateBottomTabHeaderFill();
        }
    }

    private void ScheduleEditorTabHeaderFillUpdate()
    {
        if (!IsHandleCreated)
        {
            UpdateEditorTabHeaderFill();
            return;
        }

        try
        {
            BeginInvoke((Action)UpdateEditorTabHeaderFill);
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            AppendOutput($"UI warning (tab header): {ex.Message}{Environment.NewLine}");
        }
    }

    private void UpdateEditorTabHeaderFill()
    {
        try
        {
            if (editorTabHeaderFill == null || editorTabs == null || editorTabHeaderFill.IsDisposed || editorTabs.IsDisposed)
            {
                return;
            }

            int height = Math.Max(editorTabs.ItemSize.Height + 7, 32);
            int hostWidth = Math.Max(editorTabs.Width, editorOutputSplit.Panel1.ClientSize.Width);
            editorTabHeaderFill.BackColor = ThemeBackground();
            editorTabHeaderFill.SetBounds(0, 0, hostWidth, height);
            editorTabHeaderFill.Visible = editorTabs.TabPages.Count > 0 && hostWidth > 0;
            RebuildEditorTabStrip(height);
            if (editorTabHeaderFill.Visible)
            {
                editorTabHeaderFill.BringToFront();
            }
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            AppendOutput($"UI warning (tab header layout): {ex.Message}{Environment.NewLine}");
        }
    }

    private void RebuildEditorTabStrip(int height)
    {
        editorTabHeaderFill.SuspendLayout();
        try
        {
            while (editorTabHeaderFill.Controls.Count > 0)
            {
                Control control = editorTabHeaderFill.Controls[0];
                editorTabHeaderFill.Controls.RemoveAt(0);
                control.Dispose();
            }
            int x = 5;
            int y = 3;
            int buttonHeight = Math.Max(24, height - 6);
            bool dark = settings.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);
            Color selectedBack = dark ? ColorTranslator.FromHtml("#24242a") : Color.White;
            Color tabBack = dark ? ColorTranslator.FromHtml("#1b1b1f") : ColorTranslator.FromHtml("#eef1f7");
            Color tabText = ThemeText();
            for (int i = 0; i < editorTabs.TabPages.Count; i++)
            {
                int tabIndex = i;
                TabPage page = editorTabs.TabPages[i];
                bool selected = tabIndex == editorTabs.SelectedIndex;
                Label tab = new()
                {
                    AutoEllipsis = true,
                    BackColor = selected ? selectedBack : tabBack,
                    ForeColor = tabText,
                    Font = Font,
                    Text = page.Text,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Bounds = new Rectangle(x, y, Math.Min(150, Math.Max(112, editorTabs.ItemSize.Width)), buttonHeight),
                    Cursor = Cursors.Hand
                };
                tab.MouseDown += (_, e) =>
                {
                    if (tabIndex < 0 || tabIndex >= editorTabs.TabPages.Count)
                    {
                        return;
                    }

                    editorTabs.SelectedIndex = tabIndex;
                    if (e.Button == MouseButtons.Middle)
                    {
                        CloseActiveTab();
                    }
                    else
                    {
                        ActiveEditorChanged();
                        ScheduleEditorTabHeaderFillUpdate();
                    }
                };
                editorTabHeaderFill.Controls.Add(tab);
                x += tab.Width + 2;
            }
        }
        finally
        {
            editorTabHeaderFill.ResumeLayout(false);
        }
    }

    private void ScheduleBottomTabHeaderFillUpdate()
    {
        if (!IsHandleCreated)
        {
            UpdateBottomTabHeaderFill();
            return;
        }

        try
        {
            BeginInvoke((Action)UpdateBottomTabHeaderFill);
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            AppendOutput($"UI warning (bottom tab header): {ex.Message}{Environment.NewLine}");
        }
    }

    private void UpdateBottomTabHeaderFill()
    {
        try
        {
            if (bottomTabHeaderFill == null || bottomTabs == null || bottomTabHeaderFill.IsDisposed || bottomTabs.IsDisposed)
            {
                return;
            }

            int height = Math.Max(bottomTabs.ItemSize.Height + 7, 32);
            int hostWidth = Math.Max(bottomTabs.Width, editorOutputSplit.Panel2.ClientSize.Width);
            bottomTabHeaderFill.BackColor = ThemeBackground();
            bottomTabHeaderFill.SetBounds(0, 0, hostWidth, height);
            bottomTabHeaderFill.Visible = bottomTabs.TabPages.Count > 0 && hostWidth > 0;
            RebuildBottomTabStrip(height);
            if (bottomTabHeaderFill.Visible)
            {
                bottomTabHeaderFill.BringToFront();
            }
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            AppendOutput($"UI warning (bottom tab header layout): {ex.Message}{Environment.NewLine}");
        }
    }

    private void RebuildBottomTabStrip(int height)
    {
        bottomTabHeaderFill.SuspendLayout();
        try
        {
            while (bottomTabHeaderFill.Controls.Count > 0)
            {
                Control control = bottomTabHeaderFill.Controls[0];
                bottomTabHeaderFill.Controls.RemoveAt(0);
                control.Dispose();
            }

            bool dark = IsDarkTheme();
            Color selectedBack = dark ? ColorTranslator.FromHtml("#24242a") : Color.White;
            Color tabBack = dark ? ColorTranslator.FromHtml("#1b1b1f") : ColorTranslator.FromHtml("#eef1f7");
            Color tabText = ThemeText();
            int x = 5;
            int y = 3;
            int buttonHeight = Math.Max(24, height - 6);

            for (int i = 0; i < bottomTabs.TabPages.Count; i++)
            {
                int tabIndex = i;
                TabPage page = bottomTabs.TabPages[i];
                bool selected = tabIndex == bottomTabs.SelectedIndex;
                Label tab = new()
                {
                    AutoEllipsis = true,
                    BackColor = selected ? selectedBack : tabBack,
                    ForeColor = tabText,
                    Font = Font,
                    Text = page.Text,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Bounds = new Rectangle(x, y, Math.Min(150, Math.Max(112, bottomTabs.ItemSize.Width)), buttonHeight),
                    Cursor = Cursors.Hand
                };
                tab.MouseDown += (_, _) =>
                {
                    if (tabIndex < 0 || tabIndex >= bottomTabs.TabPages.Count)
                    {
                        return;
                    }

                    bottomTabs.SelectedIndex = tabIndex;
                    ScheduleBottomTabHeaderFillUpdate();
                };
                bottomTabHeaderFill.Controls.Add(tab);
                x += tab.Width + 2;
            }
        }
        finally
        {
            bottomTabHeaderFill.ResumeLayout(false);
        }
    }

    private void StyleProblemsGrid(Color background, Color surface, Color surfaceAlt, Color border, Color text, Color primary)
    {
        problemsGrid.BackgroundColor = background;
        problemsGrid.GridColor = border;
        problemsGrid.BorderStyle = BorderStyle.None;
        problemsGrid.EnableHeadersVisualStyles = false;
        problemsGrid.ColumnHeadersDefaultCellStyle.BackColor = surfaceAlt;
        problemsGrid.ColumnHeadersDefaultCellStyle.ForeColor = text;
        problemsGrid.RowHeadersDefaultCellStyle.BackColor = surfaceAlt;
        problemsGrid.RowHeadersDefaultCellStyle.ForeColor = text;
        problemsGrid.DefaultCellStyle.BackColor = surface;
        problemsGrid.DefaultCellStyle.ForeColor = text;
        problemsGrid.DefaultCellStyle.SelectionBackColor = primary;
        problemsGrid.DefaultCellStyle.SelectionForeColor = Color.White;
        problemsGrid.AlternatingRowsDefaultCellStyle.BackColor = surfaceAlt;
        problemsGrid.AlternatingRowsDefaultCellStyle.ForeColor = text;
    }

    private static void StyleButton(Button button, Color background, Color border, Color text)
    {
        button.UseVisualStyleBackColor = false;
        button.BackColor = background;
        button.ForeColor = text;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = border;
        button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(background);
        button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(background);
        button.MinimumSize = new Size(96, 30);
        button.Width = Math.Max(button.Width, 96);
        button.Padding = new Padding(8, 2, 8, 2);
    }

    private void ConfigureWatchListDrawing(Color surface, Color header, Color border, Color text, Color accent)
    {
        watchListSurfaceColor = surface;
        watchListHeaderColor = header;
        watchListBorderColor = border;
        watchListTextColor = text;
        watchListAccentColor = accent;
        watchList.OwnerDraw = true;

        if (!watchListDrawingConfigured)
        {
            watchList.DrawColumnHeader += WatchList_DrawColumnHeader;
            watchList.DrawItem += WatchList_DrawItem;
            watchList.DrawSubItem += WatchList_DrawSubItem;
            watchListDrawingConfigured = true;
        }

        watchList.Invalidate();
    }

    private void WatchList_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using SolidBrush background = new(watchListHeaderColor);
        using Pen border = new(watchListBorderColor);
        e.Graphics.FillRectangle(background, e.Bounds);
        Rectangle textBounds = Rectangle.Inflate(e.Bounds, -8, 0);
        TextRenderer.DrawText(
            e.Graphics,
            e.Header?.Text ?? "",
            watchList.Font,
            textBounds,
            watchListTextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.Graphics.DrawRectangle(border, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
    }

    private void WatchList_DrawItem(object? sender, DrawListViewItemEventArgs e)
    {
        if (watchList.View != View.Details)
        {
            e.DrawDefault = true;
        }
    }

    private void WatchList_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        bool selected = e.Item?.Selected == true;
        Color backgroundColor = selected ? watchListAccentColor : watchListSurfaceColor;
        Color textColor = selected ? Color.White : watchListTextColor;

        using SolidBrush background = new(backgroundColor);
        e.Graphics.FillRectangle(background, e.Bounds);

        Rectangle textBounds = Rectangle.Inflate(e.Bounds, -8, 0);
        TextRenderer.DrawText(
            e.Graphics,
            e.SubItem?.Text ?? "",
            watchList.Font,
            textBounds,
            textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void OpenStartupArguments()
    {
        foreach (string arg in startupArgs)
        {
            string path;
            try
            {
                path = Path.GetFullPath(arg);
            }
            catch
            {
                continue;
            }
            if (!File.Exists(path))
            {
                continue;
            }
            if (path.EndsWith(".skkoaproj", StringComparison.OrdinalIgnoreCase))
            {
                OpenProject(path);
            }
            else
            {
                OpenFile(path);
            }
        }
    }

    private void EditorTabs_DrawItem(object? sender, DrawItemEventArgs e)
    {
        DrawTabItem(editorTabs, e);
    }

    private void BottomTabs_DrawItem(object? sender, DrawItemEventArgs e)
    {
        DrawTabItem(bottomTabs, e);
    }

    private void DrawTabItem(TabControl tabs, DrawItemEventArgs e)
    {
        if (tabs is DarkTabControl)
        {
            return;
        }
        if (e.Index < 0 || e.Index >= tabs.TabPages.Count)
        {
            return;
        }

        TabPage page = tabs.TabPages[e.Index];
        bool selected = e.Index == tabs.SelectedIndex;
        bool dark = settings.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);
        Color back = selected
            ? (dark ? ColorTranslator.FromHtml("#24242a") : Color.White)
            : (dark ? ColorTranslator.FromHtml("#1b1b1f") : ColorTranslator.FromHtml("#eef1f7"));
        using SolidBrush backBrush = new(back);
        Rectangle bounds = e.Bounds;
        bounds.Inflate(2, 2);
        e.Graphics.FillRectangle(backBrush, bounds);
        TextRenderer.DrawText(e.Graphics, page.Text, Font, e.Bounds, ThemeText(),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (selected)
        {
            using Pen pen = new(PrimaryColor(), 3);
            e.Graphics.DrawLine(pen, e.Bounds.Left + 6, e.Bounds.Bottom - 3, e.Bounds.Right - 6, e.Bounds.Bottom - 3);
        }
    }

    private static Bitmap CreateToolbarIcon(string name, int size = 24)
    {
        Bitmap bitmap = new(size, size);
        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        g.ScaleTransform(size / 24f, size / 24f);

        Color strokeColor = Color.FromArgb(242, 242, 242);
        Color mutedColor = Color.FromArgb(169, 169, 179);
        Color primaryColor = ColorTranslator.FromHtml("#a259ff");
        using Pen stroke = new(strokeColor, 2.1f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using Pen muted = new(mutedColor, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using Pen accent = new(primaryColor, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using SolidBrush fill = new(primaryColor);
        using SolidBrush lightFill = new(Color.FromArgb(235, 235, 240));

        switch (name)
        {
            case "New":
                g.DrawRectangle(stroke, 5, 4, 11, 16);
                g.DrawLine(accent, 16, 8, 20, 8);
                g.DrawLine(accent, 18, 6, 18, 10);
                break;
            case "Open":
                g.DrawLine(stroke, 4, 9, 9, 9);
                g.DrawLine(stroke, 9, 9, 11, 6);
                g.DrawLine(stroke, 11, 6, 20, 6);
                g.DrawRectangle(muted, 4, 8, 16, 11);
                g.DrawLine(accent, 7, 14, 17, 14);
                break;
            case "Save":
                g.DrawRectangle(stroke, 5, 4, 14, 16);
                g.DrawLine(muted, 8, 4, 8, 10);
                g.DrawLine(muted, 8, 10, 16, 10);
                g.DrawRectangle(accent, 8, 14, 8, 5);
                break;
            case "Check":
                g.DrawEllipse(muted, 4, 4, 16, 16);
                g.DrawLines(accent, [new PointF(8, 12), new PointF(11, 15), new PointF(17, 9)]);
                break;
            case "Compile":
                g.DrawLine(stroke, 7, 17, 17, 7);
                g.DrawLine(stroke, 13, 5, 19, 11);
                g.DrawLine(accent, 5, 19, 10, 14);
                break;
            case "Run":
                g.FillPolygon(fill, [new PointF(8, 5), new PointF(18, 12), new PointF(8, 19)]);
                break;
            case "Continue":
                g.FillPolygon(fill, [new PointF(6, 5), new PointF(16, 12), new PointF(6, 19)]);
                g.DrawLine(stroke, 19, 6, 19, 18);
                break;
            case "Step":
                g.DrawLine(accent, 5, 12, 16, 12);
                g.DrawLines(accent, [new PointF(12, 8), new PointF(16, 12), new PointF(12, 16)]);
                g.DrawLine(stroke, 19, 6, 19, 18);
                break;
            case "Step Over":
                g.DrawArc(accent, 5, 5, 13, 12, 190, 270);
                g.DrawLines(accent, [new PointF(14, 13), new PointF(18, 17), new PointF(19, 11)]);
                g.DrawLine(stroke, 7, 20, 19, 20);
                break;
            case "Step Out":
                g.DrawLine(accent, 12, 18, 12, 6);
                g.DrawLines(accent, [new PointF(8, 10), new PointF(12, 6), new PointF(16, 10)]);
                g.DrawLine(stroke, 6, 19, 18, 19);
                break;
            case "Debug":
                g.DrawEllipse(accent, 6, 6, 12, 12);
                g.FillEllipse(lightFill, 10, 10, 4, 4);
                g.DrawLine(muted, 12, 3, 12, 6);
                g.DrawLine(muted, 12, 18, 12, 21);
                g.DrawLine(muted, 3, 12, 6, 12);
                g.DrawLine(muted, 18, 12, 21, 12);
                break;
            case "Stop":
                using (SolidBrush stopFill = new(Color.FromArgb(220, 70, 70)))
                {
                    g.FillRectangle(stopFill, 7, 7, 10, 10);
                }
                break;
            case "Theme":
                g.FillEllipse(fill, 5, 5, 14, 14);
                using (SolidBrush maskFill = new(Color.FromArgb(17, 17, 17)))
                {
                    g.FillRectangle(maskFill, 12, 4, 8, 16);
                }
                g.DrawEllipse(stroke, 5, 5, 14, 14);
                break;
            default:
                g.DrawEllipse(stroke, 5, 5, 14, 14);
                break;
        }

        return bitmap;
    }

    private void ShowAbout()
    {
        using Form form = new()
        {
            Text = "About SKKOA Studio",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(560, 280),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = ThemeBackground(),
            ForeColor = ThemeText(),
            Icon = Icon
        };
        PictureBox icon = new()
        {
            Image = Icon?.ToBitmap(),
            SizeMode = PictureBoxSizeMode.StretchImage,
            Location = new Point(24, 24),
            Size = new Size(64, 64)
        };
        Label title = new()
        {
            Text = "SKKOA Studio",
            Font = new Font(Font.FontFamily, 18, FontStyle.Bold),
            ForeColor = PrimaryColor(),
            Location = new Point(108, 26),
            AutoSize = true
        };
        Label text = new()
        {
            Text = $"SKKOA; LTW IDE\r\nStarter Kit with Korean Oriented Architecture; Language to Write\r\nVersion {SkkoaUpdateService.GetCurrentVersion()}\r\nTeam ToyoTech",
            Location = new Point(111, 66),
            AutoSize = true,
            MaximumSize = new Size(400, 0)
        };
        Button ok = new() { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(414, 196), Width = 96 };
        StyleButton(ok, ThemeSurfaceAlt(), ThemeBorder(), ThemeText());
        form.Controls.AddRange([icon, title, text, ok]);
        form.AcceptButton = ok;
        form.ShowDialog(this);
    }

    private Color PrimaryColor()
    {
        try
        {
            return ColorTranslator.FromHtml(settings.PrimaryColor);
        }
        catch
        {
            return ColorTranslator.FromHtml("#a259ff");
        }
    }

    private bool IsDarkTheme()
    {
        return settings.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);
    }

    private Color ThemeBackground()
    {
        return IsDarkTheme()
            ? ColorTranslator.FromHtml("#111111")
            : ColorTranslator.FromHtml("#f4f5f8");
    }

    private Color ThemeSurface()
    {
        return IsDarkTheme()
            ? ColorTranslator.FromHtml("#1b1b1f")
            : Color.White;
    }

    private Color ThemeSurfaceAlt()
    {
        return IsDarkTheme()
            ? ColorTranslator.FromHtml("#24242a")
            : ColorTranslator.FromHtml("#eef1f7");
    }

    private Color ThemeBorder()
    {
        return IsDarkTheme()
            ? ColorTranslator.FromHtml("#33333a")
            : ColorTranslator.FromHtml("#c9ced8");
    }

    private Color ThemeText()
    {
        return IsDarkTheme()
            ? ColorTranslator.FromHtml("#f2f2f2")
            : ColorTranslator.FromHtml("#202026");
    }

    private Color ThemeMutedText()
    {
        return IsDarkTheme()
            ? ColorTranslator.FromHtml("#a9a9b3")
            : ColorTranslator.FromHtml("#575866");
    }

    private Color ThemeEditorBackground()
    {
        return IsDarkTheme()
            ? ColorTranslator.FromHtml("#15151a")
            : Color.White;
    }

    private Color ThemeEditorText()
    {
        return IsDarkTheme()
            ? ColorTranslator.FromHtml("#eeeeee")
            : ColorTranslator.FromHtml("#202026");
    }

    private void TryEditorAction(string actionName, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            string key = actionName + ":" + ex.GetType().FullName + ":" + ex.Message;
            if (reportedEditorErrors.Add(key))
            {
                AppendOutput($"Editor warning ({actionName}): {ex.Message}{Environment.NewLine}");
            }
        }
    }

    private void ShowError(string message)
    {
        AppendOutput(message + Environment.NewLine);
        try
        {
            MessageBox.Show(this, message, "SKKOA Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch
        {
        }
    }

    private static bool IsRecoverableException(Exception ex)
    {
        return ex is not OutOfMemoryException &&
               ex is not AccessViolationException &&
               ex is not AppDomainUnloadedException &&
               ex is not BadImageFormatException;
    }

    private static string SanitizeFileName(string value)
    {
        string name = string.IsNullOrWhiteSpace(value) ? "skkoa-output" : value.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }
        return string.IsNullOrWhiteSpace(name) ? "skkoa-output" : name;
    }

    private void ApplyWindowFrameTheme()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        bool dark = IsDarkTheme();
        int useDarkMode = dark ? 1 : 0;
        TrySetDwmAttribute(DwmwaUseImmersiveDarkMode, useDarkMode);

        Color caption = ThemeBackground();
        Color border = dark ? Color.Black : ThemeBorder();
        Color text = ThemeText();
        TrySetDwmAttribute(DwmwaCaptionColor, ColorToColorRef(caption));
        TrySetDwmAttribute(DwmwaBorderColor, ColorToColorRef(border));
        TrySetDwmAttribute(DwmwaTextColor, ColorToColorRef(text));
    }

    private void TrySetDwmAttribute(int attribute, int value)
    {
        try
        {
            _ = DwmSetWindowAttribute(Handle, attribute, ref value, Marshal.SizeOf<int>());
        }
        catch
        {
        }
    }

    private static int ColorToColorRef(Color color)
    {
        return color.R | (color.G << 8) | (color.B << 16);
    }

    private static Icon? LoadAppIcon()
    {
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "assets", "icons", "skkoa.ico"),
            Path.GetFullPath(Path.Combine("skkoa-studio", "editor", "assets", "icons", "skkoa.ico"))
        ];
        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                try
                {
                    return new Icon(candidate);
                }
                catch
                {
                }
            }
        }
        return null;
    }

    private void UpdateRecentMenus()
    {
        recentFilesMenu.DropDownItems.Clear();
        foreach (string path in settings.RecentFiles)
        {
            recentFilesMenu.DropDownItems.Add(MenuItem(path, (_, _) => OpenFile(path)));
        }
        recentFilesMenu.Enabled = recentFilesMenu.DropDownItems.Count > 0;

        recentProjectsMenu.DropDownItems.Clear();
        foreach (string path in settings.RecentProjects)
        {
            recentProjectsMenu.DropDownItems.Add(MenuItem(path, (_, _) => OpenProject(path)));
        }
        recentProjectsMenu.Enabled = recentProjectsMenu.DropDownItems.Count > 0;
    }

    private void UpdateStatus()
    {
        Scintilla? editor = CurrentEditor();
        string lineText = "Ln -, Col -";
        if (editor != null)
        {
            try
            {
                lineText = $"Ln {editor.CurrentLine + 1}, Col {editor.GetColumn(editor.CurrentPosition) + 1}";
            }
            catch (Exception ex) when (IsRecoverableException(ex))
            {
                lineText = "Ln ?, Col ?";
            }
        }
        lineStatus.Text = lineText;
        encodingStatus.Text = "UTF-8";
        tabStatus.Text = $"Tab {settings.TabSize}";
        themeStatus.Text = settings.Theme;
        EditorTabState? state = CurrentState();
        int errors = state?.Diagnostics.Count(d => !d.IsWarning) ?? 0;
        int warnings = state?.Diagnostics.Count(d => d.IsWarning) ?? 0;
        diagnosticsStatus.Text = $"{errors} errors, {warnings} warnings";
    }

    private void AppendOutput(string text)
    {
        if (IsDisposed)
        {
            return;
        }
        if (InvokeRequired)
        {
            SafeBeginInvoke(() => AppendOutput(text));
            return;
        }
        outputBox.AppendText(StripAnsi(text));
    }

    private void AppendConsole(string text)
    {
        if (IsDisposed)
        {
            return;
        }
        if (InvokeRequired)
        {
            SafeBeginInvoke(() => AppendConsole(text));
            return;
        }
        consoleBox.AppendText(StripAnsi(text));
    }

    private void SafeBeginInvoke(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }
        try
        {
            BeginInvoke(action);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string StripAnsi(string text)
    {
        return AnsiEscapeRegex.Replace(text, "");
    }

    private string GetEntryFilePath(EditorTabState state)
    {
        return currentProject == null ? state.Document.FilePath! : projectService.GetEntryPath(currentProject);
    }

    private string? GetWorkingDirectory(EditorTabState state)
    {
        if (currentProject != null)
        {
            return currentProject.ProjectDirectory;
        }
        return state.Document.FilePath == null ? null : Path.GetDirectoryName(state.Document.FilePath);
    }

    private string? GetLibPath()
    {
        if (currentProject?.LibPaths.Count > 0)
        {
            return string.Join(Path.PathSeparator, projectService.GetLibPaths(currentProject));
        }
        return string.IsNullOrWhiteSpace(settings.LibPath) ? compilerService.ResolveLibPath() : settings.LibPath;
    }

    private static int PositionFromLineColumn(string text, int oneBasedLine, int oneBasedColumn)
    {
        int line = 1;
        int column = 1;
        for (int i = 0; i < text.Length; i++)
        {
            if (line == oneBasedLine && column == oneBasedColumn)
            {
                return i;
            }
            if (text[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }
        return text.Length;
    }

    private static bool IsIdentifierPart(char ch)
    {
        return ch == '_' || char.IsLetterOrDigit(ch) || (ch >= '\uAC00' && ch <= '\uD7A3');
    }

    private static (int Start, string Prefix) GetCompletionPrefix(string text, int position)
    {
        int safePosition = Math.Clamp(position, 0, text.Length);
        int start = safePosition;
        while (start > 0 && IsIdentifierPart(text[start - 1]))
        {
            start--;
        }
        return (start, text[start..safePosition]);
    }

    private static bool ShouldDeferCompletionEnter(Scintilla editor)
    {
        if (IsImeCompositionActive(editor))
        {
            return true;
        }

        int position = Math.Min(editor.CurrentPosition, editor.Text.Length);
        (_, string prefix) = GetCompletionPrefix(editor.Text, position);
        return ContainsHangul(prefix);
    }

    private static bool ContainsHangul(string value)
    {
        foreach (char ch in value)
        {
            if (ch is >= '\uAC00' and <= '\uD7A3' or >= '\u1100' and <= '\u11FF' or >= '\u3130' and <= '\u318F')
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsImeCompositionActive(Control control)
    {
        if (!control.IsHandleCreated)
        {
            return false;
        }

        IntPtr context = ImmGetContext(control.Handle);
        if (context == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            return ImmGetCompositionString(context, GcsCompStr, IntPtr.Zero, 0) > 0 ||
                   ImmGetCompositionString(context, GcsResultStr, IntPtr.Zero, 0) > 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            ImmReleaseContext(control.Handle, context);
        }
    }

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr hWnd);

    [DllImport("imm32.dll")]
    private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hImc);

    [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
    private static extern int ImmGetCompositionString(IntPtr hImc, int dwIndex, IntPtr lpBuf, int dwBufLen);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    private static string GetCurrentLineText(Scintilla editor)
    {
        try
        {
            string text = editor.Text;
            int position = Math.Min(editor.CurrentPosition, text.Length);
            int start = text.LastIndexOf('\n', Math.Max(0, position - 1));
            start = start < 0 ? 0 : start + 1;
            int end = text.IndexOf('\n', position);
            end = end < 0 ? text.Length : end;
            return text[start..end];
        }
        catch
        {
            return "";
        }
    }

    private static string GetCurrentLineIndent(Scintilla editor)
    {
        return new string(GetCurrentLineText(editor).TakeWhile(char.IsWhiteSpace).ToArray());
    }

    private Scintilla? CurrentEditor() => CurrentState()?.Editor;

    private EditorTabState? CurrentState()
    {
        return editorTabs.SelectedTab?.Tag as EditorTabState;
    }

    private EditorTabState? StateForEditor(Scintilla editor)
    {
        foreach (TabPage page in editorTabs.TabPages)
        {
            if (page.Tag is EditorTabState state && ReferenceEquals(state.Editor, editor))
            {
                return state;
            }
        }
        return null;
    }

    private void UpdateTabTitle(EditorTabState state)
    {
        string title = (state.Document.IsDirty ? "*" : "") + state.Document.DisplayName;
        if (!string.Equals(state.TabPage.Text, title, StringComparison.Ordinal))
        {
            state.TabPage.Text = title;
            ScheduleEditorTabHeaderFillUpdate();
        }
    }

    private string? Prompt(string title, string label)
    {
        Color background = ThemeBackground();
        Color surface = ThemeSurface();
        Color surfaceAlt = ThemeSurfaceAlt();
        Color border = ThemeBorder();
        Color text = ThemeText();

        using Form form = new()
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(460, 150),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = background,
            ForeColor = text
        };
        Label labelControl = new() { Text = label, Left = 16, Top = 16, Width = 410, ForeColor = text };
        TextBox input = new() { Left = 16, Top = 44, Width = 410, BackColor = surface, ForeColor = text, BorderStyle = BorderStyle.FixedSingle };
        Button ok = new() { Text = "OK", DialogResult = DialogResult.OK, Left = 230, Top = 82, Width = 96 };
        Button cancel = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 332, Top = 82, Width = 96 };
        StyleButton(ok, surfaceAlt, border, text);
        StyleButton(cancel, surfaceAlt, border, text);
        form.Controls.AddRange([labelControl, input, ok, cancel]);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog() == DialogResult.OK ? input.Text : null;
    }

    private sealed class EditorTabState
    {
        public EditorTabState(SkkoaDocument document, Scintilla editor)
        {
            Document = document;
            Editor = editor;
        }

        public SkkoaDocument Document { get; }
        public Scintilla Editor { get; }
        public TabPage TabPage { get; set; } = null!;
        public IReadOnlyList<SkkoaDiagnostic> Diagnostics { get; set; } = [];
        public HashSet<int> Breakpoints { get; } = [];
    }

    private sealed class PrimaryToolStripRenderer : ToolStripProfessionalRenderer
    {
        private readonly bool dark;

        public PrimaryToolStripRenderer(Color primary, bool dark)
            : base(new PrimaryColorTable(primary, dark))
        {
            this.dark = dark;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (!e.Item.Enabled)
            {
                e.TextColor = dark ? ColorTranslator.FromHtml("#777782") : ColorTranslator.FromHtml("#8a8c98");
            }
            else if (dark || e.Item.Selected || e.Item.Pressed)
            {
                e.TextColor = Color.White;
            }
            else
            {
                e.TextColor = ColorTranslator.FromHtml("#202026");
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (!dark)
            {
                base.OnRenderToolStripBorder(e);
                return;
            }

            using Pen pen = new(ColorTranslator.FromHtml("#33333a"));
            Rectangle bounds = new(Point.Empty, e.ToolStrip.Size);
            bounds.Width -= 1;
            bounds.Height -= 1;
            e.Graphics.DrawRectangle(pen, bounds);
        }
    }

    private sealed class PrimaryColorTable : ProfessionalColorTable
    {
        private readonly Color primary;
        private readonly bool dark;

        public PrimaryColorTable(Color primary, bool dark)
        {
            this.primary = primary;
            this.dark = dark;
        }

        public override Color MenuItemSelected => primary;
        public override Color MenuItemBorder => primary;
        public override Color MenuItemSelectedGradientBegin => primary;
        public override Color MenuItemSelectedGradientEnd => primary;
        public override Color MenuItemPressedGradientBegin => primary;
        public override Color MenuItemPressedGradientMiddle => primary;
        public override Color MenuItemPressedGradientEnd => primary;
        public override Color MenuBorder => dark ? ColorTranslator.FromHtml("#33333a") : base.MenuBorder;
        public override Color SeparatorDark => dark ? ColorTranslator.FromHtml("#33333a") : base.SeparatorDark;
        public override Color SeparatorLight => dark ? ColorTranslator.FromHtml("#33333a") : base.SeparatorLight;
        public override Color ToolStripDropDownBackground => dark ? ColorTranslator.FromHtml("#1b1b1f") : Color.White;
        public override Color ImageMarginGradientBegin => dark ? ColorTranslator.FromHtml("#1b1b1f") : Color.White;
        public override Color ImageMarginGradientMiddle => dark ? ColorTranslator.FromHtml("#1b1b1f") : Color.White;
        public override Color ImageMarginGradientEnd => dark ? ColorTranslator.FromHtml("#1b1b1f") : Color.White;
        public override Color ToolStripGradientBegin => dark ? ColorTranslator.FromHtml("#111111") : ColorTranslator.FromHtml("#f4f5f8");
        public override Color ToolStripGradientMiddle => ToolStripGradientBegin;
        public override Color ToolStripGradientEnd => ToolStripGradientBegin;
        public override Color MenuStripGradientBegin => ToolStripGradientBegin;
        public override Color MenuStripGradientEnd => ToolStripGradientBegin;
        public override Color StatusStripGradientBegin => ToolStripGradientBegin;
        public override Color StatusStripGradientEnd => ToolStripGradientBegin;
        public override Color ToolStripBorder => dark ? ColorTranslator.FromHtml("#33333a") : ColorTranslator.FromHtml("#c9ced8");
        public override Color ButtonSelectedBorder => primary;
        public override Color ButtonSelectedGradientBegin => primary;
        public override Color ButtonSelectedGradientMiddle => primary;
        public override Color ButtonSelectedGradientEnd => primary;
        public override Color ButtonPressedBorder => primary;
        public override Color ButtonPressedGradientBegin => primary;
        public override Color ButtonPressedGradientMiddle => primary;
        public override Color ButtonPressedGradientEnd => primary;
        public override Color ButtonCheckedGradientBegin => primary;
        public override Color ButtonCheckedGradientMiddle => primary;
        public override Color ButtonCheckedGradientEnd => primary;
    }
}
