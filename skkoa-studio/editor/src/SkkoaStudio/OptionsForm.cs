using SkkoaStudio.Core.Settings;

namespace SkkoaStudio;

public sealed class OptionsForm : Form
{
    private readonly ComboBox themeBox = new();
    private readonly TextBox primaryColorBox = new();
    private readonly TextBox fontBox = new();
    private readonly NumericUpDown fontSizeBox = new();
    private readonly NumericUpDown tabSizeBox = new();
    private readonly CheckBox insertSpacesBox = new();
    private readonly CheckBox formatOnEnterBox = new();
    private readonly CheckBox formatOnSaveBox = new();
    private readonly CheckBox diagnosticsOnTypeBox = new();
    private readonly TextBox compilerPathBox = new();
    private readonly TextBox libPathBox = new();

    public OptionsForm(SkkoaStudioSettings source)
    {
        Settings = new SkkoaStudioSettings
        {
            Theme = string.Equals(source.Theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark",
            PrimaryColor = string.IsNullOrWhiteSpace(source.PrimaryColor) ? "#a259ff" : source.PrimaryColor,
            FontFamily = string.IsNullOrWhiteSpace(source.FontFamily) ? "Consolas" : source.FontFamily,
            FontSize = Math.Clamp(source.FontSize, 8, 36),
            TabSize = Math.Clamp(source.TabSize, 1, 12),
            InsertSpaces = source.InsertSpaces,
            FormatOnEnter = source.FormatOnEnter,
            FormatOnSave = source.FormatOnSave,
            DiagnosticsOnType = source.DiagnosticsOnType,
            CompilerPath = source.CompilerPath ?? "",
            LibPath = source.LibPath ?? "",
            RecentFiles = [.. (source.RecentFiles ?? [])],
            RecentProjects = [.. (source.RecentProjects ?? [])]
        };

        Text = "Options";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(640, 430);
        MinimumSize = new Size(640, 430);
        Font = new Font("Segoe UI", 9);

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 3,
            RowCount = 12
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        for (int i = 0; i < 11; i++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        }
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        themeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        themeBox.Items.AddRange(["Dark", "Light"]);
        themeBox.SelectedItem = Settings.Theme;
        primaryColorBox.Text = Settings.PrimaryColor;

        fontBox.Text = Settings.FontFamily;
        fontBox.ReadOnly = true;
        Button fontButton = new() { Text = "Font", Dock = DockStyle.Fill };
        fontButton.Click += (_, _) => PickFont();

        fontSizeBox.Minimum = 8;
        fontSizeBox.Maximum = 36;
        fontSizeBox.DecimalPlaces = 1;
        fontSizeBox.Increment = 0.5m;
        fontSizeBox.Value = (decimal)Settings.FontSize;

        tabSizeBox.Minimum = 1;
        tabSizeBox.Maximum = 12;
        tabSizeBox.Value = Settings.TabSize;

        insertSpacesBox.Text = "Insert spaces";
        insertSpacesBox.Checked = Settings.InsertSpaces;
        formatOnEnterBox.Text = "Format on Enter";
        formatOnEnterBox.Checked = Settings.FormatOnEnter;
        formatOnSaveBox.Text = "Format on Save";
        formatOnSaveBox.Checked = Settings.FormatOnSave;
        diagnosticsOnTypeBox.Text = "Diagnostics on Type";
        diagnosticsOnTypeBox.Checked = Settings.DiagnosticsOnType;

        compilerPathBox.Text = Settings.CompilerPath;
        libPathBox.Text = Settings.LibPath;

        AddRow(root, 0, "Theme", themeBox);
        AddRow(root, 1, "Primary Color", primaryColorBox);
        AddRow(root, 2, "Font", fontBox, fontButton);
        AddRow(root, 3, "Font Size", fontSizeBox);
        AddRow(root, 4, "Tab Size", tabSizeBox);
        AddRow(root, 5, "", insertSpacesBox);
        AddRow(root, 6, "", formatOnEnterBox);
        AddRow(root, 7, "", formatOnSaveBox);
        AddRow(root, 8, "", diagnosticsOnTypeBox);
        AddPathRow(root, 9, "Compiler Path", compilerPathBox, "skkoa.exe|skkoa.exe|All files|*.*");
        AddPathRow(root, 10, "Lib Path", libPathBox, folder: true);

        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 42
        };
        Button ok = new() { Text = "OK", DialogResult = DialogResult.OK, Width = 88 };
        Button cancel = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 88 };
        ok.Click += (_, _) => Commit();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        Controls.Add(root);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public SkkoaStudioSettings Settings { get; private set; }

    private static void AddRow(TableLayoutPanel root, int row, string label, Control control, Control? button = null)
    {
        root.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        control.Dock = DockStyle.Fill;
        root.Controls.Add(control, 1, row);
        if (button != null)
        {
            root.Controls.Add(button, 2, row);
        }
        else
        {
            root.SetColumnSpan(control, 2);
        }
    }

    private static void AddPathRow(TableLayoutPanel root, int row, string label, TextBox textBox, string filter = "All files|*.*", bool folder = false)
    {
        Button browse = new() { Text = "Browse", Dock = DockStyle.Fill };
        browse.Click += (_, _) =>
        {
            try
            {
                if (folder)
                {
                    using FolderBrowserDialog dialog = new();
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        textBox.Text = dialog.SelectedPath;
                    }
                }
                else
                {
                    using OpenFileDialog dialog = new() { Filter = filter };
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        textBox.Text = dialog.FileName;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "경로 선택 창을 열 수 없습니다." + Environment.NewLine + ex.Message,
                    "SKKOA Studio",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        };
        AddRow(root, row, label, textBox, browse);
    }

    private void PickFont()
    {
        Font initialFont;
        try
        {
            initialFont = new Font(Settings.FontFamily, Settings.FontSize);
        }
        catch
        {
            initialFont = new Font("Consolas", 12);
        }

        using FontDialog dialog = new()
        {
            Font = initialFont,
            FixedPitchOnly = false,
            ShowEffects = false
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            fontBox.Text = dialog.Font.FontFamily.Name;
            fontSizeBox.Value = (decimal)dialog.Font.Size;
        }
    }

    private void Commit()
    {
        Settings.Theme = Convert.ToString(themeBox.SelectedItem) ?? "Dark";
        Settings.PrimaryColor = string.IsNullOrWhiteSpace(primaryColorBox.Text) ? "#a259ff" : primaryColorBox.Text.Trim();
        Settings.FontFamily = string.IsNullOrWhiteSpace(fontBox.Text) ? "Consolas" : fontBox.Text;
        Settings.FontSize = (float)fontSizeBox.Value;
        Settings.TabSize = (int)tabSizeBox.Value;
        Settings.InsertSpaces = insertSpacesBox.Checked;
        Settings.FormatOnEnter = formatOnEnterBox.Checked;
        Settings.FormatOnSave = formatOnSaveBox.Checked;
        Settings.DiagnosticsOnType = diagnosticsOnTypeBox.Checked;
        Settings.CompilerPath = compilerPathBox.Text.Trim();
        Settings.LibPath = libPathBox.Text.Trim();
    }
}
