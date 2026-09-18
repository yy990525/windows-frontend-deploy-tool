using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FrontendDeployTool
{
    internal sealed class NginxManagerForm : Form
    {
        private readonly SettingsStore _settingsStore = new SettingsStore();
        private AppSettings _settings;
        private TextBox _exeBox;
        private TextBox _configBox;
        private RichTextBox _editor;
        private TextBox _operationLog;
        private Label _statusLabel;
        private Button _saveButton;
        private Button _startButton;
        private Button _stopButton;
        private Button _reloadButton;
        private Button _undoFormatButton;
        private string _beforeFormatText;
        private ComboBox _encodingCombo;
        private Encoding _configEncoding = new UTF8Encoding(false, true);
        private bool _loadingEditor;
        private bool _dirty;
        private bool _operating;

        public NginxManagerForm()
        {
            Text = "Nginx 管理";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 790);
            MinimumSize = new Size(960, 690);
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.FromArgb(244, 247, 251);
            AutoScaleMode = AutoScaleMode.Dpi;

            BuildInterface();
            LoadSettings();
            FormClosing += OnFormClosing;
        }

        private void BuildInterface()
        {
            Label title = new Label
            {
                Text = "Nginx 配置与服务管理",
                Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
                Location = new Point(24, 18),
                AutoSize = true
            };
            Controls.Add(title);

            _statusLabel = new Label
            {
                Text = "● 未检测",
                Location = new Point(835, 25),
                Size = new Size(220, 25),
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                ForeColor = Color.FromArgb(92, 101, 116)
            };
            Controls.Add(_statusLabel);

            Controls.Add(CreateLabel("nginx.exe", 24, 68));
            _exeBox = CreatePathBox(125, 64, 790);
            Controls.Add(_exeBox);
            Button browseExe = CreateButton("选择…", 931, 62, 124, 32);
            browseExe.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browseExe.Click += BrowseExe;
            Controls.Add(browseExe);

            Controls.Add(CreateLabel("nginx.conf", 24, 108));
            _configBox = CreatePathBox(125, 104, 650);
            Controls.Add(_configBox);
            Button browseConfig = CreateButton("选择…", 790, 102, 90, 32);
            browseConfig.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browseConfig.Click += BrowseConfig;
            Controls.Add(browseConfig);
            Button loadButton = CreateButton("加载配置", 891, 102, 79, 32);
            loadButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            loadButton.Click += delegate { LoadConfigIntoEditor(); };
            Controls.Add(loadButton);
            Button externalButton = CreateButton("记事本", 981, 102, 74, 32);
            externalButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            externalButton.Click += OpenInNotepad;
            Controls.Add(externalButton);

            Panel toolbar = new Panel
            {
                Location = new Point(24, 148),
                Size = new Size(1031, 43),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(toolbar);

            Button formatButton = CreateButton("格式化缩进", 8, 5, 118, 31);
            formatButton.Click += FormatEditor;
            toolbar.Controls.Add(formatButton);

            _saveButton = CreateButton("检测并保存", 135, 5, 118, 31);
            _saveButton.Click += async delegate { await SaveConfigAsync(); };
            toolbar.Controls.Add(_saveButton);

            Button testButton = CreateButton("检测配置", 262, 5, 105, 31);
            testButton.Click += async delegate { await TestConfigAsync(); };
            toolbar.Controls.Add(testButton);

            _undoFormatButton = CreateButton("撤销格式化", 376, 5, 105, 31);
            _undoFormatButton.Enabled = false;
            _undoFormatButton.Click += UndoFormat;
            toolbar.Controls.Add(_undoFormatButton);

            _startButton = CreateButton("启用 Nginx", 596, 5, 120, 31);
            _startButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _startButton.Click += async delegate { await RunNginxActionAsync("启用"); };
            toolbar.Controls.Add(_startButton);

            _stopButton = CreateButton("停用 Nginx", 726, 5, 120, 31);
            _stopButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _stopButton.Click += async delegate { await RunNginxActionAsync("停用"); };
            toolbar.Controls.Add(_stopButton);

            _reloadButton = CreateButton("重启/重载", 856, 5, 160, 31);
            _reloadButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _reloadButton.BackColor = Color.FromArgb(38, 103, 231);
            _reloadButton.ForeColor = Color.White;
            _reloadButton.FlatStyle = FlatStyle.Flat;
            _reloadButton.FlatAppearance.BorderSize = 0;
            _reloadButton.Click += async delegate { await RunNginxActionAsync("重载"); };
            toolbar.Controls.Add(_reloadButton);

            Label editorLabel = CreateLabel("nginx.conf 编辑器（格式化不会自动保存）", 24, 206);
            Controls.Add(editorLabel);

            Label encodingLabel = CreateLabel("编码", 713, 206);
            encodingLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Controls.Add(encodingLabel);

            _encodingCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(756, 201),
                Size = new Size(145, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _encodingCombo.Items.AddRange(TextFileCodec.SupportedNames);
            _encodingCombo.SelectedItem = TextFileCodec.Auto;
            Controls.Add(_encodingCombo);

            Button reloadEncodingButton = CreateButton("按编码重载", 912, 199, 143, 31);
            reloadEncodingButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            reloadEncodingButton.Click += delegate { LoadConfigIntoEditor(false); };
            Controls.Add(reloadEncodingButton);

            _editor = new RichTextBox
            {
                Location = new Point(24, 231),
                Size = new Size(1031, 355),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Consolas", 10F),
                AcceptsTab = true,
                WordWrap = false,
                DetectUrls = false,
                BackColor = Color.FromArgb(252, 253, 255),
                BorderStyle = BorderStyle.FixedSingle
            };
            _editor.TextChanged += delegate
            {
                if (!_loadingEditor)
                {
                    _dirty = true;
                    UpdateWindowTitle();
                }
            };
            Controls.Add(_editor);

            Label logLabel = CreateLabel("操作结果", 24, 602);
            logLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(logLabel);

            _operationLog = new TextBox
            {
                Location = new Point(24, 627),
                Size = new Size(1031, 105),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(22, 27, 34),
                ForeColor = Color.FromArgb(218, 225, 234),
                Font = new Font("Consolas", 9F)
            };
            Controls.Add(_operationLog);
        }

        private static Label CreateLabel(string text, int x, int y)
        {
            return new Label { Text = text, Location = new Point(x, y), AutoSize = true };
        }

        private static TextBox CreatePathBox(int x, int y, int width)
        {
            return new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
        }

        private static Button CreateButton(string text, int x, int y, int width, int height)
        {
            return new Button { Text = text, Location = new Point(x, y), Size = new Size(width, height) };
        }

        private void LoadSettings()
        {
            _settings = _settingsStore.Load();
            AutoDetectNginx(_settings);
            _exeBox.Text = _settings.nginxExePath;
            _configBox.Text = _settings.nginxConfigPath;
            SaveSettingsFromBoxes();
            if (File.Exists(_configBox.Text))
            {
                LoadConfigIntoEditor(true);
            }
            RefreshStatus();
        }

        private static void AutoDetectNginx(AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.nginxExePath))
            {
                string[] candidates =
                {
                    Path.Combine(AppPaths.Root, "nginx.exe"),
                    @"C:\nginx\nginx.exe"
                };
                foreach (string candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        settings.nginxExePath = candidate;
                        break;
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(settings.nginxConfigPath) && File.Exists(settings.nginxExePath))
            {
                string candidate = Path.Combine(Path.GetDirectoryName(settings.nginxExePath), "conf", "nginx.conf");
                if (File.Exists(candidate))
                {
                    settings.nginxConfigPath = candidate;
                }
            }
        }

        private void BrowseExe(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择 nginx.exe";
                dialog.Filter = "Nginx 程序 (nginx.exe)|nginx.exe|EXE 文件 (*.exe)|*.exe";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _exeBox.Text = dialog.FileName;
                    if (string.IsNullOrWhiteSpace(_configBox.Text))
                    {
                        string config = Path.Combine(Path.GetDirectoryName(dialog.FileName), "conf", "nginx.conf");
                        if (File.Exists(config))
                        {
                            _configBox.Text = config;
                        }
                    }
                    SaveSettingsFromBoxes();
                    RefreshStatus();
                }
            }
        }

        private void BrowseConfig(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择 nginx.conf";
                dialog.Filter = "Nginx 配置 (nginx.conf)|nginx.conf|CONF 文件 (*.conf)|*.conf|所有文件 (*.*)|*.*";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    if (_dirty && !ConfirmDiscardChanges())
                    {
                        return;
                    }
                    _configBox.Text = dialog.FileName;
                    _encodingCombo.SelectedItem = TextFileCodec.Auto;
                    SaveSettingsFromBoxes();
                    LoadConfigIntoEditor(true);
                    RefreshStatus();
                }
            }
        }

        private void LoadConfigIntoEditor()
        {
            LoadConfigIntoEditor(true);
        }

        private void LoadConfigIntoEditor(bool forceAutoDetect)
        {
            if (_dirty && !ConfirmDiscardChanges())
            {
                return;
            }
            string config = _configBox.Text.Trim();
            if (!File.Exists(config))
            {
                MessageBox.Show(this, "找不到 nginx.conf。", "加载失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _loadingEditor = true;
            try
            {
                string requestedEncoding = forceAutoDetect
                    ? TextFileCodec.Auto
                    : Convert.ToString(_encodingCombo.SelectedItem);
                TextFileContent content = TextFileCodec.Read(config, requestedEncoding);
                _configEncoding = content.Encoding;
                _editor.Text = content.Text;
                _editor.SelectionStart = 0;
                _editor.SelectionLength = 0;
                _dirty = false;
                _beforeFormatText = null;
                _undoFormatButton.Enabled = false;
                if (_encodingCombo.Items.Contains(content.EncodingName))
                {
                    _encodingCombo.SelectedItem = content.EncodingName;
                }
                SaveSettingsFromBoxes();
                AppendOperation("已加载：" + config + "；编码：" + content.EncodingName);
            }
            finally
            {
                _loadingEditor = false;
                UpdateWindowTitle();
            }
        }

        private void FormatEditor(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_editor.Text))
            {
                return;
            }
            int selection = _editor.SelectionStart;
            _beforeFormatText = _editor.Text;
            _editor.Text = NginxFormatter.Format(_editor.Text);
            _editor.SelectionStart = Math.Min(selection, _editor.TextLength);
            _editor.ScrollToCaret();
            _dirty = true;
            _undoFormatButton.Enabled = true;
            UpdateWindowTitle();
            AppendOperation("已格式化编辑器缩进，尚未保存。可点击“撤销格式化”恢复。");
        }

        private void UndoFormat(object sender, EventArgs e)
        {
            if (_beforeFormatText == null)
            {
                return;
            }
            _editor.Text = _beforeFormatText;
            _beforeFormatText = null;
            _undoFormatButton.Enabled = false;
            _dirty = true;
            UpdateWindowTitle();
            AppendOperation("已撤销最近一次格式化。");
        }

        private async Task SaveConfigAsync()
        {
            if (_operating)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(_editor.Text))
            {
                MessageBox.Show(this, "配置内容不能为空。", "无法保存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string exe = _exeBox.Text.Trim();
            string config = _configBox.Text.Trim();
            if (!File.Exists(exe) || !File.Exists(config))
            {
                MessageBox.Show(this, "请先选择有效的 nginx.exe 和 nginx.conf。", "无法保存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetOperating(true);
            string tempConfig = config + ".deploy-tool-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                string selectedEncoding = Convert.ToString(_encodingCombo.SelectedItem);
                if (!string.IsNullOrWhiteSpace(selectedEncoding) && selectedEncoding != TextFileCodec.Auto)
                {
                    _configEncoding = TextFileCodec.GetEncoding(selectedEncoding);
                }
                TextFileCodec.Write(tempConfig, _editor.Text, _configEncoding);
                NginxController controller = new NginxController(exe, config);
                NginxCommandResult test = await Task.Run(delegate { return controller.TestConfig(tempConfig); });
                AppendOperation(test.Output);
                if (!test.Success)
                {
                    MessageBox.Show(this, "配置检测失败，原 nginx.conf 没有被覆盖。\r\n\r\n" + test.Output, "未保存", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string backupName = "nginx.conf." + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak";
                string backupPath = Path.Combine(AppPaths.NginxConfigBackupRoot, backupName);
                Directory.CreateDirectory(AppPaths.NginxConfigBackupRoot);
                File.Copy(config, backupPath, true);
                File.Copy(tempConfig, config, true);
                _dirty = false;
                UpdateWindowTitle();
                SaveSettingsFromBoxes();
                AppendOperation("配置检测通过并已保存。编码：" + Convert.ToString(_encodingCombo.SelectedItem) + "；备份：" + backupPath);
                MessageBox.Show(this, "配置检测通过并已保存。\r\n编码：" + Convert.ToString(_encodingCombo.SelectedItem) + "\r\n\r\n备份：" + backupPath, "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendOperation("保存失败：" + ex.Message);
                MessageBox.Show(this, "保存失败：\r\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try { if (File.Exists(tempConfig)) File.Delete(tempConfig); }
                catch { }
                SetOperating(false);
            }
        }

        private async Task TestConfigAsync()
        {
            if (_operating)
            {
                return;
            }
            try
            {
                SaveSettingsFromBoxes();
                NginxController controller = CreateController();
                SetOperating(true);
                NginxCommandResult result = await Task.Run(delegate { return controller.TestConfig(); });
                AppendOperation(result.Output);
                MessageBox.Show(
                    this,
                    result.Output,
                    result.Success ? "配置检测通过" : "配置检测失败",
                    MessageBoxButtons.OK,
                    result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                AppendOperation("配置检测失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "配置检测失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetOperating(false);
                RefreshStatus();
            }
        }

        private async Task RunNginxActionAsync(string action)
        {
            if (_operating)
            {
                return;
            }
            if (_dirty && (action == "启用" || action == "重载"))
            {
                MessageBox.Show(this, "编辑器中有未保存的修改。请先执行“检测并保存”。", "尚未保存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                SaveSettingsFromBoxes();
                NginxController controller = CreateController();
                SetOperating(true);
                AppendOperation("正在执行：" + action + " Nginx");
                NginxCommandResult result = await Task.Run(delegate
                {
                    if (action == "启用") return controller.Start();
                    if (action == "停用") return controller.Stop();
                    return controller.Reload();
                });
                AppendOperation(result.Output);
                MessageBox.Show(
                    this,
                    result.Output,
                    result.Success ? action + "完成" : action + "失败",
                    MessageBoxButtons.OK,
                    result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                AppendOperation(action + "失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, action + "失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetOperating(false);
                RefreshStatus();
            }
        }

        private void OpenInNotepad(object sender, EventArgs e)
        {
            string config = _configBox.Text.Trim();
            if (!File.Exists(config))
            {
                MessageBox.Show(this, "找不到 nginx.conf。", "无法打开", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Process.Start("notepad.exe", "\"" + config + "\"");
        }

        private NginxController CreateController()
        {
            NginxController controller = new NginxController(_exeBox.Text.Trim(), _configBox.Text.Trim());
            controller.ValidateFiles();
            return controller;
        }

        private void SaveSettingsFromBoxes()
        {
            _settings.nginxExePath = _exeBox.Text.Trim();
            _settings.nginxConfigPath = _configBox.Text.Trim();
            _settingsStore.Save(_settings);
        }

        private void RefreshStatus()
        {
            try
            {
                if (!File.Exists(_exeBox.Text.Trim()))
                {
                    _statusLabel.Text = "● 未配置 nginx.exe";
                    _statusLabel.ForeColor = Color.FromArgb(92, 101, 116);
                    return;
                }
                NginxController controller = new NginxController(_exeBox.Text.Trim(), _configBox.Text.Trim());
                bool running = controller.IsRunning();
                _statusLabel.Text = running ? "● Nginx 运行中" : "● Nginx 已停用";
                _statusLabel.ForeColor = running ? Color.FromArgb(30, 135, 76) : Color.FromArgb(186, 108, 0);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "● 状态检测失败";
                _statusLabel.ForeColor = Color.FromArgb(190, 55, 55);
                AppendOperation("状态检测失败：" + ex.Message);
            }
        }

        private void SetOperating(bool value)
        {
            _operating = value;
            _saveButton.Enabled = !value;
            _startButton.Enabled = !value;
            _stopButton.Enabled = !value;
            _reloadButton.Enabled = !value;
            UseWaitCursor = value;
        }

        private void AppendOperation(string message)
        {
            string line = string.Format("{0:HH:mm:ss}  {1}", DateTime.Now, message);
            _operationLog.AppendText(line + Environment.NewLine);
            _operationLog.SelectionStart = _operationLog.TextLength;
            _operationLog.ScrollToCaret();
        }

        private bool ConfirmDiscardChanges()
        {
            return MessageBox.Show(
                this,
                "编辑器中有未保存的修改，确定放弃吗？",
                "放弃修改",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        private void UpdateWindowTitle()
        {
            Text = _dirty ? "Nginx 管理 *" : "Nginx 管理";
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_operating)
            {
                MessageBox.Show(this, "Nginx 操作进行中，请稍后关闭。", "无法关闭", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }
            if (_dirty && !ConfirmDiscardChanges())
            {
                e.Cancel = true;
            }
        }
    }
}
