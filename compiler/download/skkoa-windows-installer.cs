using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace SkkoaInstaller
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }
    }

    internal sealed class InstallerForm : Form
    {
        private readonly TextBox installRootBox;
        private readonly TextBox binDirBox;
        private readonly TextBox toolchainRootBox;
        private readonly TextBox baseUrlBox;
        private readonly TextBox logBox;
        private readonly CheckBox installToolchainBox;
        private readonly CheckBox updatePathBox;
        private readonly Button installButton;
        private readonly Button cancelButton;
        private readonly ProgressBar progressBar;

        private volatile bool isInstalling;

        public InstallerForm()
        {
            Text = "SKKOA; LTW Windows Installer";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 620);
            Size = new Size(820, 680);
            Font = new Font("Segoe UI", 9F);
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string defaultInstallRoot = Path.Combine(localAppData, "SKKOA");
            string defaultBinDir = Path.Combine(localAppData, "Programs\\SKKOA\\bin");
            string defaultToolchainRoot = Path.Combine(defaultInstallRoot, "toolchain");

            installRootBox = new TextBox { Text = defaultInstallRoot, Dock = DockStyle.Fill };
            binDirBox = new TextBox { Text = defaultBinDir, Dock = DockStyle.Fill };
            toolchainRootBox = new TextBox { Text = defaultToolchainRoot, Dock = DockStyle.Fill };
            baseUrlBox = new TextBox { Text = "https://skkoa.toyotech.dev/compiler", Dock = DockStyle.Fill };

            installToolchainBox = new CheckBox
            {
                Text = "MSYS2 GCC와 NASM 도구를 자동으로 설치하거나 확인",
                Checked = true,
                AutoSize = true
            };
            updatePathBox = new CheckBox
            {
                Text = "사용자 PATH에 skkoa 명령 추가",
                Checked = true,
                AutoSize = true
            };

            installButton = new Button { Text = "설치", Width = 120, Height = 34 };
            cancelButton = new Button { Text = "닫기", Width = 120, Height = 34 };
            progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Marquee,
                Visible = false,
                MarqueeAnimationSpeed = 35
            };
            logBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = false,
                Font = new Font("Consolas", 9F)
            };

            installButton.Click += InstallButton_Click;
            cancelButton.Click += CancelButton_Click;

            Controls.Add(BuildLayout());
            AppendLog("SKKOA; LTW Windows 설치 프로그램");
            AppendLog("설치 위치와 옵션을 확인한 뒤 설치를 누르세요.");
        }

        private Control BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18),
                ColumnCount = 1,
                RowCount = 6
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label title = new Label
            {
                Text = "SKKOA; LTW 설치",
                Font = new Font(Font.FontFamily, 18F, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6)
            };
            Label subtitle = new Label
            {
                Text = "Starter Kit with Korean Oriented Architecture;\r\nLanguage to Write 컴파일러와 실행 도구를 Windows에 설치합니다.",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 16)
            };

            Panel header = new Panel { Dock = DockStyle.Top, Height = 88 };
            title.Location = new Point(0, 0);
            subtitle.Location = new Point(1, 36);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(BuildLocationGroup(), 0, 1);
            root.Controls.Add(BuildSettingsGroup(), 0, 2);
            root.Controls.Add(BuildLogGroup(), 0, 3);
            root.Controls.Add(progressBar, 0, 4);
            root.Controls.Add(BuildButtonRow(), 0, 5);

            return root;
        }

        private Control BuildLocationGroup()
        {
            GroupBox group = new GroupBox
            {
                Text = "설치 위치",
                Dock = DockStyle.Top,
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 0, 12),
                Height = 152
            };

            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));

            AddPathRow(table, 0, "소스/빌드 위치", installRootBox);
            AddPathRow(table, 1, "명령 설치 위치", binDirBox);
            AddPathRow(table, 2, "도구 설치 위치", toolchainRootBox);

            group.Controls.Add(table);
            return group;
        }

        private Control BuildSettingsGroup()
        {
            GroupBox group = new GroupBox
            {
                Text = "설정",
                Dock = DockStyle.Top,
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 0, 12),
                Height = 132
            };

            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            table.Controls.Add(installToolchainBox, 0, 0);
            table.SetColumnSpan(installToolchainBox, 2);
            table.Controls.Add(updatePathBox, 0, 1);
            table.SetColumnSpan(updatePathBox, 2);

            Label urlLabel = new Label
            {
                Text = "다운로드 URL",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            table.Controls.Add(urlLabel, 0, 2);
            table.Controls.Add(baseUrlBox, 1, 2);

            group.Controls.Add(table);
            return group;
        }

        private Control BuildLogGroup()
        {
            GroupBox group = new GroupBox
            {
                Text = "진행 상황",
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 0, 10)
            };
            group.Controls.Add(logBox);
            return group;
        }

        private Control BuildButtonRow()
        {
            FlowLayoutPanel row = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false
            };
            row.Controls.Add(cancelButton);
            row.Controls.Add(installButton);
            return row;
        }

        private void AddPathRow(TableLayoutPanel table, int row, string labelText, TextBox box)
        {
            Label label = new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Button browse = new Button
            {
                Text = "찾기",
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 3, 0, 3)
            };
            browse.Click += delegate { BrowseForFolder(box); };

            table.Controls.Add(label, 0, row);
            table.Controls.Add(box, 1, row);
            table.Controls.Add(browse, 2, row);
        }

        private void BrowseForFolder(TextBox target)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "설치 위치를 선택하세요.";
                dialog.SelectedPath = ExpandEnvironment(target.Text.Trim());
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    target.Text = dialog.SelectedPath;
                }
            }
        }

        private void InstallButton_Click(object sender, EventArgs e)
        {
            if (isInstalling)
            {
                return;
            }

            string installRoot = ExpandEnvironment(installRootBox.Text.Trim());
            string binDir = ExpandEnvironment(binDirBox.Text.Trim());
            string toolchainRoot = ExpandEnvironment(toolchainRootBox.Text.Trim());
            string baseUrl = baseUrlBox.Text.Trim().TrimEnd('/');

            if (!ValidatePath("소스/빌드 위치", installRoot) ||
                !ValidatePath("명령 설치 위치", binDir) ||
                !ValidatePath("도구 설치 위치", toolchainRoot) ||
                !ValidateUrl(baseUrl))
            {
                return;
            }

            installRootBox.Text = installRoot;
            binDirBox.Text = binDir;
            toolchainRootBox.Text = toolchainRoot;
            baseUrlBox.Text = baseUrl;

            bool installToolchain = installToolchainBox.Checked;
            bool updatePath = updatePathBox.Checked;
            isInstalling = true;
            SetInstallingState(true);
            Thread worker = new Thread(new ThreadStart(delegate
            {
                RunInstaller(installRoot, binDir, toolchainRoot, baseUrl, installToolchain, updatePath);
            }));
            worker.IsBackground = true;
            worker.Start();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            if (!isInstalling)
            {
                Close();
            }
        }

        private void RunInstaller(string installRoot, string binDir, string toolchainRoot, string baseUrl, bool installToolchain, bool updatePath)
        {
            int exitCode = -1;
            try
            {
                ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | (SecurityProtocolType)3072;

                AppendLog("");
                AppendLog("설치를 시작합니다.");
                AppendLog("소스/빌드 위치: " + installRoot);
                AppendLog("명령 설치 위치: " + binDir);
                AppendLog("도구 설치 위치: " + toolchainRoot);

                string scriptPath = ResolveScriptPath(baseUrl);
                string powershell = ResolvePowerShellPath();

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = powershell,
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(scriptPath),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                psi.EnvironmentVariables["SKKOA_BASE_URL"] = baseUrl;
                psi.EnvironmentVariables["SKKOA_INSTALL_ROOT"] = installRoot;
                psi.EnvironmentVariables["SKKOA_BIN_DIR"] = binDir;
                psi.EnvironmentVariables["SKKOA_TOOLCHAIN_ROOT"] = toolchainRoot;
                if (!installToolchain)
                {
                    psi.EnvironmentVariables["SKKOA_SKIP_TOOLCHAIN_INSTALL"] = "1";
                }
                if (!updatePath)
                {
                    psi.EnvironmentVariables["SKKOA_SKIP_PATH_UPDATE"] = "1";
                }

                using (Process process = new Process())
                {
                    process.StartInfo = psi;
                    process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) AppendLog(e.Data);
                    };
                    process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) AppendLog(e.Data);
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                }

                if (exitCode == 0)
                {
                    AppendLog("");
                    AppendLog("설치가 끝났습니다. 새 터미널에서 skkoa hello.koa 를 실행하세요.");
                }
                else
                {
                    AppendLog("");
                    AppendLog("설치가 실패했습니다. 종료 코드: " + exitCode);
                }
            }
            catch (Exception ex)
            {
                AppendLog("");
                AppendLog("설치 중 오류가 발생했습니다.");
                AppendLog(ex.Message);
            }
            finally
            {
                BeginInvoke(new Action(delegate
                {
                    isInstalling = false;
                    SetInstallingState(false);
                }));
            }
        }

        private string ResolveScriptPath(string baseUrl)
        {
            string localScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skkoa-windows.ps1");
            if (File.Exists(localScript))
            {
                AppendLog("로컬 설치 스크립트 사용: " + localScript);
                return localScript;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "skkoa-installer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string tempScript = Path.Combine(tempDir, "skkoa-windows.ps1");
            string scriptUrl = baseUrl.TrimEnd('/') + "/download/skkoa-windows.ps1";

            AppendLog("설치 스크립트 다운로드: " + scriptUrl);
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("User-Agent", "SKKOA; LTW installer");
                client.DownloadFile(scriptUrl, tempScript);
            }
            return tempScript;
        }

        private static string ResolvePowerShellPath()
        {
            string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string powershell = Path.Combine(systemRoot, "System32\\WindowsPowerShell\\v1.0\\powershell.exe");
            return File.Exists(powershell) ? powershell : "powershell.exe";
        }

        private bool ValidatePath(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                MessageBox.Show(this, label + "를 입력하세요.", "SKKOA; LTW", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (value.IndexOf('"') >= 0)
            {
                MessageBox.Show(this, label + "에는 큰따옴표를 사용할 수 없습니다.", "SKKOA; LTW", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (!Path.IsPathRooted(value))
            {
                MessageBox.Show(this, label + "는 절대 경로여야 합니다.", "SKKOA; LTW", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private bool ValidateUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                MessageBox.Show(this, "다운로드 URL은 http 또는 https 주소여야 합니다.", "SKKOA; LTW", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private void SetInstallingState(bool installing)
        {
            installButton.Enabled = !installing;
            cancelButton.Enabled = !installing;
            installRootBox.Enabled = !installing;
            binDirBox.Enabled = !installing;
            toolchainRootBox.Enabled = !installing;
            baseUrlBox.Enabled = !installing;
            installToolchainBox.Enabled = !installing;
            updatePathBox.Enabled = !installing;
            progressBar.Visible = installing;
        }

        private void AppendLog(string message)
        {
            if (logBox.InvokeRequired)
            {
                logBox.BeginInvoke(new Action<string>(AppendLog), message);
                return;
            }
            logBox.AppendText(message + Environment.NewLine);
        }

        private static string ExpandEnvironment(string value)
        {
            return Environment.ExpandEnvironmentVariables(value);
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
