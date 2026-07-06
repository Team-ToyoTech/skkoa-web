namespace SkkoaStudio;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;
    private MenuStrip menuStrip = null!;
    private ToolStrip toolStrip = null!;
    private StatusStrip statusStrip = null!;
    private SplitContainer mainSplit = null!;
    private SplitContainer editorOutputSplit = null!;
    private TreeView projectTree = null!;
    private TabControl editorTabs = null!;
    private Panel editorTabHeaderFill = null!;
    private TabControl bottomTabs = null!;
    private Panel bottomTabHeaderFill = null!;
    private ToolStrip debugToolStrip = null!;
    private ToolStripButton debugContinueButton = null!;
    private ToolStripButton debugStepButton = null!;
    private ToolStripButton debugStepOverButton = null!;
    private ToolStripButton debugStepOutButton = null!;
    private ToolStripButton debugStopButton = null!;
    private DataGridView problemsGrid = null!;
    private TextBox outputBox = null!;
    private TextBox consoleBox = null!;
    private TextBox consoleInputBox = null!;
    private Button consoleSendButton = null!;
    private TextBox debugBox = null!;
    private ListView watchList = null!;
    private ListBox completionList = null!;
    private ToolStripStatusLabel lineStatus = null!;
    private ToolStripStatusLabel encodingStatus = null!;
    private ToolStripStatusLabel tabStatus = null!;
    private ToolStripStatusLabel themeStatus = null!;
    private ToolStripStatusLabel diagnosticsStatus = null!;
    private ToolStripStatusLabel buildStatus = null!;
    private ToolStripMenuItem recentFilesMenu = null!;
    private ToolStripMenuItem recentProjectsMenu = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        menuStrip = new MenuStrip();
        toolStrip = new ToolStrip();
        statusStrip = new StatusStrip();
        mainSplit = new SplitContainer();
        editorOutputSplit = new SplitContainer();
        projectTree = new TreeView();
        editorTabs = new DarkTabControl();
        editorTabHeaderFill = new Panel();
        bottomTabs = new DarkTabControl();
        bottomTabHeaderFill = new Panel();
        problemsGrid = new DataGridView();
        outputBox = new TextBox();
        consoleBox = new TextBox();
        consoleInputBox = new TextBox();
        consoleSendButton = new Button();
        debugBox = new TextBox();
        watchList = new ListView();
        completionList = new ListBox();
        lineStatus = new ToolStripStatusLabel();
        encodingStatus = new ToolStripStatusLabel();
        tabStatus = new ToolStripStatusLabel();
        themeStatus = new ToolStripStatusLabel();
        diagnosticsStatus = new ToolStripStatusLabel();
        buildStatus = new ToolStripStatusLabel();

        SuspendLayout();

        menuStrip.Items.AddRange(new ToolStripItem[]
        {
            BuildFileMenu(),
            BuildEditMenu(),
            BuildViewMenu(),
            BuildBuildMenu(),
            BuildDebugMenu(),
            BuildToolsMenu(),
            BuildHelpMenu()
        });
        menuStrip.Dock = DockStyle.Top;
        menuStrip.BackColor = ColorTranslator.FromHtml("#111111");
        menuStrip.ForeColor = ColorTranslator.FromHtml("#f2f2f2");
        menuStrip.Font = new Font("Segoe UI", 10.5f);

        toolStrip.Items.AddRange(new ToolStripItem[]
        {
            ToolButton("New", (_, _) => NewFile()),
            ToolButton("Open", (_, _) => OpenFile()),
            ToolButton("Save", (_, _) => SaveFile()),
            new ToolStripSeparator(),
            ToolButton("Check", async (_, _) => await CheckCurrentAsync()),
            ToolButton("Compile", async (_, _) => await CompileCurrentAsync()),
            ToolButton("Run", async (_, _) => await RunCurrentAsync()),
            ToolButton("Debug", (_, _) => StartDebugging()),
            ToolButton("Stop", (_, _) => StopActiveProcess()),
            new ToolStripSeparator(),
            ToolButton("Theme", (_, _) => ToggleTheme())
        });
        toolStrip.Dock = DockStyle.Top;
        toolStrip.BackColor = ColorTranslator.FromHtml("#111111");
        toolStrip.ForeColor = ColorTranslator.FromHtml("#f2f2f2");
        toolStrip.GripStyle = ToolStripGripStyle.Hidden;
        toolStrip.ImageScalingSize = new Size(24, 24);
        toolStrip.Font = new Font("Segoe UI", 10.5f);
        toolStrip.Padding = new Padding(8, 5, 8, 5);

        mainSplit.Dock = DockStyle.Fill;
        mainSplit.BackColor = ColorTranslator.FromHtml("#33333a");
        mainSplit.FixedPanel = FixedPanel.Panel1;
        mainSplit.Paint += SplitContainer_Paint;
        mainSplit.Panel1.Controls.Add(projectTree);
        mainSplit.Panel2.Controls.Add(editorOutputSplit);

        projectTree.Dock = DockStyle.Fill;
        projectTree.BackColor = ColorTranslator.FromHtml("#15151a");
        projectTree.ForeColor = ColorTranslator.FromHtml("#f2f2f2");
        projectTree.BorderStyle = BorderStyle.None;
        projectTree.HideSelection = false;
        projectTree.NodeMouseDoubleClick += ProjectTree_NodeMouseDoubleClick;

        editorOutputSplit.Dock = DockStyle.Fill;
        editorOutputSplit.BackColor = ColorTranslator.FromHtml("#33333a");
        editorOutputSplit.Orientation = Orientation.Horizontal;
        editorOutputSplit.Paint += SplitContainer_Paint;
        editorOutputSplit.Panel1.Controls.Add(editorTabs);
        editorOutputSplit.Panel1.Controls.Add(editorTabHeaderFill);
        editorOutputSplit.Panel2.Controls.Add(bottomTabs);
        editorOutputSplit.Panel2.Controls.Add(bottomTabHeaderFill);

        editorTabs.Dock = DockStyle.Fill;
        editorTabs.Appearance = TabAppearance.FlatButtons;
        editorTabs.SizeMode = TabSizeMode.Fixed;
        editorTabs.ItemSize = new Size(128, 28);
        editorTabs.SelectedIndexChanged += (_, _) => ActiveEditorChanged();
        editorTabs.MouseDown += EditorTabs_MouseDown;

        editorTabHeaderFill.BackColor = ColorTranslator.FromHtml("#111111");
        editorTabHeaderFill.Height = 35;
        editorTabHeaderFill.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        editorTabHeaderFill.Visible = false;

        bottomTabs.Dock = DockStyle.Fill;
        bottomTabs.Appearance = TabAppearance.FlatButtons;
        bottomTabs.SizeMode = TabSizeMode.Fixed;
        bottomTabs.ItemSize = new Size(120, 28);
        bottomTabs.BackColor = ColorTranslator.FromHtml("#111111");
        bottomTabs.TabPages.Add(BuildProblemsPage());
        bottomTabs.TabPages.Add(BuildTextPage("Output", outputBox));
        bottomTabs.TabPages.Add(BuildConsolePage());
        bottomTabs.TabPages.Add(BuildDebugPage());

        bottomTabHeaderFill.BackColor = ColorTranslator.FromHtml("#111111");
        bottomTabHeaderFill.Height = 35;
        bottomTabHeaderFill.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        bottomTabHeaderFill.Visible = false;

        completionList.Visible = false;
        completionList.BackColor = ColorTranslator.FromHtml("#1b1b1f");
        completionList.ForeColor = ColorTranslator.FromHtml("#f2f2f2");
        completionList.IntegralHeight = false;
        completionList.Height = 180;
        completionList.Width = 260;
        completionList.DoubleClick += (_, _) => InsertSelectedCompletion();
        completionList.KeyDown += CompletionList_KeyDown;

        statusStrip.Items.AddRange(new ToolStripItem[]
        {
            lineStatus,
            encodingStatus,
            tabStatus,
            themeStatus,
            diagnosticsStatus,
            buildStatus
        });
        statusStrip.Dock = DockStyle.Bottom;
        statusStrip.BackColor = ColorTranslator.FromHtml("#111111");
        statusStrip.ForeColor = ColorTranslator.FromHtml("#f2f2f2");
        statusStrip.Font = new Font("Segoe UI", 9.5f);

        Controls.Add(mainSplit);
        Controls.Add(statusStrip);
        Controls.Add(toolStrip);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;
        MinimumSize = new Size(1024, 720);
        Text = "SKKOA Studio";
        BackColor = ColorTranslator.FromHtml("#111111");
        ForeColor = ColorTranslator.FromHtml("#f2f2f2");
        WindowState = FormWindowState.Maximized;

        ResumeLayout(false);
        PerformLayout();
    }

    private ToolStripMenuItem BuildFileMenu()
    {
        ToolStripMenuItem menu = new("File");
        menu.DropDownItems.Add(MenuItem("New File", (_, _) => NewFile(), Keys.Control | Keys.N));
        menu.DropDownItems.Add(MenuItem("Open File", (_, _) => OpenFile(), Keys.Control | Keys.O));
        recentFilesMenu = new ToolStripMenuItem("Recent Files");
        menu.DropDownItems.Add(recentFilesMenu);
        menu.DropDownItems.Add(MenuItem("Save", (_, _) => SaveFile(), Keys.Control | Keys.S));
        menu.DropDownItems.Add(MenuItem("Save As", (_, _) => SaveFileAs()));
        menu.DropDownItems.Add(new ToolStripSeparator());
        menu.DropDownItems.Add(MenuItem("New Project", (_, _) => NewProject()));
        menu.DropDownItems.Add(MenuItem("Open Project", (_, _) => OpenProject()));
        recentProjectsMenu = new ToolStripMenuItem("Recent Projects");
        menu.DropDownItems.Add(recentProjectsMenu);
        menu.DropDownItems.Add(new ToolStripSeparator());
        menu.DropDownItems.Add(MenuItem("Close", (_, _) => CloseActiveTab(), Keys.Control | Keys.W));
        menu.DropDownItems.Add(MenuItem("Exit", (_, _) => Close()));
        return menu;
    }

    private ToolStripMenuItem BuildEditMenu()
    {
        ToolStripMenuItem menu = new("Edit");
        menu.DropDownItems.Add(MenuItem("Undo", (_, _) => CurrentEditor()?.Undo(), Keys.Control | Keys.Z));
        menu.DropDownItems.Add(MenuItem("Redo", (_, _) => CurrentEditor()?.Redo(), Keys.Control | Keys.Y));
        menu.DropDownItems.Add(new ToolStripSeparator());
        menu.DropDownItems.Add(MenuItem("Cut", (_, _) => CurrentEditor()?.Cut(), Keys.Control | Keys.X));
        menu.DropDownItems.Add(MenuItem("Copy", (_, _) => CurrentEditor()?.Copy(), Keys.Control | Keys.C));
        menu.DropDownItems.Add(MenuItem("Paste", (_, _) => CurrentEditor()?.Paste(), Keys.Control | Keys.V));
        menu.DropDownItems.Add(new ToolStripSeparator());
        menu.DropDownItems.Add(MenuItem("Find", (_, _) => FindText(), Keys.Control | Keys.F));
        menu.DropDownItems.Add(MenuItem("Replace", (_, _) => ReplaceText(), Keys.Control | Keys.H));
        menu.DropDownItems.Add(MenuItem("Find All", (_, _) => FindAllText()));
        menu.DropDownItems.Add(new ToolStripSeparator());
        menu.DropDownItems.Add(MenuItem("Format Document", (_, _) => FormatDocument(), Keys.Control | Keys.Shift | Keys.F));
        menu.DropDownItems.Add(MenuItem("Format Selection", (_, _) => FormatSelection()));
        menu.DropDownItems.Add(MenuItem("Toggle Comment", (_, _) => ToggleComment(), Keys.Control | Keys.OemQuestion));
        return menu;
    }

    private ToolStripMenuItem BuildViewMenu()
    {
        ToolStripMenuItem menu = new("View");
        menu.DropDownItems.Add(MenuItem("Project Explorer", (_, _) => mainSplit.Panel1Collapsed = !mainSplit.Panel1Collapsed));
        menu.DropDownItems.Add(MenuItem("Problems", (_, _) => bottomTabs.SelectedIndex = 0));
        menu.DropDownItems.Add(MenuItem("Output", (_, _) => bottomTabs.SelectedIndex = 1));
        menu.DropDownItems.Add(MenuItem("Console", (_, _) => bottomTabs.SelectedIndex = 2));
        menu.DropDownItems.Add(MenuItem("Debug", (_, _) => bottomTabs.SelectedIndex = 3));
        menu.DropDownItems.Add(new ToolStripSeparator());
        menu.DropDownItems.Add(MenuItem("Light Theme", (_, _) => SetTheme("Light")));
        menu.DropDownItems.Add(MenuItem("Dark Theme", (_, _) => SetTheme("Dark")));
        return menu;
    }

    private ToolStripMenuItem BuildBuildMenu()
    {
        ToolStripMenuItem menu = new("Build");
        menu.DropDownItems.Add(MenuItem("Check", async (_, _) => await CheckCurrentAsync(), Keys.F6));
        menu.DropDownItems.Add(MenuItem("Compile", async (_, _) => await CompileCurrentAsync(), Keys.Control | Keys.F7));
        menu.DropDownItems.Add(MenuItem("Run", async (_, _) => await RunCurrentAsync(), Keys.F5));
        menu.DropDownItems.Add(MenuItem("Clean", (_, _) => CleanBuildOutput()));
        return menu;
    }

    private ToolStripMenuItem BuildDebugMenu()
    {
        ToolStripMenuItem menu = new("Debug");
        menu.DropDownItems.Add(MenuItem("Start Debugging", (_, _) => StartDebugging(), Keys.F9));
        menu.DropDownItems.Add(MenuItem("Continue", (_, _) => ContinueDebugging(), Keys.F5));
        menu.DropDownItems.Add(MenuItem("Step Into", (_, _) => StepIntoDebug(), Keys.F11));
        menu.DropDownItems.Add(MenuItem("Step Over", (_, _) => StepOverDebug(), Keys.F10));
        menu.DropDownItems.Add(MenuItem("Step Out", (_, _) => StepOutDebug(), Keys.Shift | Keys.F11));
        menu.DropDownItems.Add(MenuItem("Stop", (_, _) => StopDebugging(), Keys.Shift | Keys.F5));
        return menu;
    }

    private ToolStripMenuItem BuildToolsMenu()
    {
        ToolStripMenuItem menu = new("Tools");
        menu.DropDownItems.Add(MenuItem("Options", (_, _) => ShowOptions()));
        menu.DropDownItems.Add(MenuItem("Install/Repair Toolchain", async (_, _) => await InstallToolchainAsync()));
        return menu;
    }

    private ToolStripMenuItem BuildHelpMenu()
    {
        ToolStripMenuItem menu = new("Help");
        menu.DropDownItems.Add(MenuItem("SKKOA Documentation", (_, _) => OpenDocumentation()));
        menu.DropDownItems.Add(MenuItem("About", (_, _) => ShowAbout()));
        return menu;
    }

    private TabPage BuildProblemsPage()
    {
        TabPage page = new("Problems") { UseVisualStyleBackColor = false };
        problemsGrid.Dock = DockStyle.Fill;
        problemsGrid.ReadOnly = true;
        problemsGrid.AllowUserToAddRows = false;
        problemsGrid.AllowUserToDeleteRows = false;
        problemsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        problemsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        problemsGrid.Columns.Add("Severity", "Severity");
        problemsGrid.Columns.Add("Code", "Code");
        problemsGrid.Columns.Add("Message", "Message");
        problemsGrid.Columns.Add("File", "File");
        problemsGrid.Columns.Add("Line", "Line");
        problemsGrid.Columns.Add("Column", "Column");
        problemsGrid.CellDoubleClick += ProblemsGrid_CellDoubleClick;
        page.Controls.Add(problemsGrid);
        return page;
    }

    private static TabPage BuildTextPage(string title, TextBox box)
    {
        TabPage page = new(title) { UseVisualStyleBackColor = false };
        box.Dock = DockStyle.Fill;
        box.Multiline = true;
        box.ReadOnly = true;
        box.ScrollBars = ScrollBars.Vertical;
        box.WordWrap = true;
        box.Font = new Font("Consolas", 10);
        page.Controls.Add(box);
        return page;
    }

    private TabPage BuildConsolePage()
    {
        TabPage page = new("Console") { UseVisualStyleBackColor = false };
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        consoleBox.Multiline = true;
        consoleBox.ReadOnly = true;
        consoleBox.ScrollBars = ScrollBars.Vertical;
        consoleBox.WordWrap = true;
        consoleBox.Font = new Font("Consolas", 10);
        consoleBox.Dock = DockStyle.Fill;
        consoleInputBox.Dock = DockStyle.Fill;
        consoleInputBox.KeyDown += ConsoleInputBox_KeyDown;
        consoleSendButton.Text = "Send";
        consoleSendButton.Dock = DockStyle.Fill;
        consoleSendButton.Click += (_, _) => SendConsoleInput();
        layout.Controls.Add(consoleBox, 0, 0);
        layout.SetColumnSpan(consoleBox, 2);
        layout.Controls.Add(consoleInputBox, 0, 1);
        layout.Controls.Add(consoleSendButton, 1, 1);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildDebugPage()
    {
        TabPage page = new("Debug") { UseVisualStyleBackColor = false };
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        debugToolStrip = new ToolStrip
        {
            Dock = DockStyle.Fill,
            GripStyle = ToolStripGripStyle.Hidden,
            ImageScalingSize = new Size(32, 32),
            Font = new Font("Segoe UI", 10f),
            Padding = new Padding(8, 5, 8, 5),
            Visible = false
        };
        debugContinueButton = DebugToolButton("Continue", (_, _) => ContinueDebugging());
        debugStepButton = DebugToolButton("Step", (_, _) => StepIntoDebug());
        debugStepOverButton = DebugToolButton("Step Over", (_, _) => StepOverDebug());
        debugStepOutButton = DebugToolButton("Step Out", (_, _) => StepOutDebug());
        debugStopButton = DebugToolButton("Stop", (_, _) => StopDebugging());
        debugToolStrip.Items.AddRange(new ToolStripItem[]
        {
            debugContinueButton,
            debugStepButton,
            debugStepOverButton,
            debugStepOutButton,
            new ToolStripSeparator(),
            debugStopButton
        });

        SplitContainer split = new() { Dock = DockStyle.Fill };
        split.Paint += SplitContainer_Paint;
        ConfigureNestedSplit(split, desiredDistance: 360, panel1MinSize: 140, panel2MinSize: 180);
        debugBox.Dock = DockStyle.Fill;
        debugBox.Multiline = true;
        debugBox.ReadOnly = true;
        debugBox.ScrollBars = ScrollBars.Vertical;
        debugBox.WordWrap = true;
        debugBox.Font = new Font("Consolas", 10);
        watchList.Dock = DockStyle.Fill;
        watchList.View = View.Details;
        watchList.Columns.Add("Name", 150);
        watchList.Columns.Add("Value", 260);
        split.Panel1.Controls.Add(watchList);
        split.Panel2.Controls.Add(debugBox);
        layout.Controls.Add(debugToolStrip, 0, 0);
        layout.Controls.Add(split, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private static ToolStripMenuItem MenuItem(string text, EventHandler handler, Keys shortcut = Keys.None)
    {
        ToolStripMenuItem item = new(text);
        item.Click += handler;
        if (shortcut != Keys.None)
        {
            item.ShortcutKeys = shortcut;
        }
        return item;
    }

    private static ToolStripButton ToolButton(string text, EventHandler handler)
    {
        ToolStripButton button = new(text)
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            Image = CreateToolbarIcon(text, 24),
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ToolTipText = text,
            AutoToolTip = true,
            AutoSize = true,
            Padding = new Padding(6, 2, 6, 2),
            Margin = new Padding(3, 1, 3, 1)
        };
        button.Click += handler;
        return button;
    }

    private static ToolStripButton DebugToolButton(string text, EventHandler handler)
    {
        ToolStripButton button = new(text)
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            Image = CreateToolbarIcon(text, 32),
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ToolTipText = text,
            AutoToolTip = true,
            AutoSize = true,
            Padding = new Padding(8, 2, 8, 2),
            Margin = new Padding(4, 1, 4, 1)
        };
        button.Click += handler;
        return button;
    }
}
