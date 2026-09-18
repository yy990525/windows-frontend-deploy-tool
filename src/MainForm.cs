using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FrontendDeployTool
{
    internal sealed class MainForm : Form
    {
        private readonly ProjectStore _store = new ProjectStore();
        private readonly DeployLogger _logger = new DeployLogger();
        private List<ProjectConfig> _projects = new List<ProjectConfig>();
        private ComboBox _projectCombo;
        private ComboBox _typeCombo;
        private TextBox _targetBox;
        private TextBox _zipBox;
        private CheckBox _backupCheck;
        private CheckBox _stripCheck;
        private Button _deployButton;
        private Button _manageButton;
        private Button _browseButton;
        private TextBox _logBox;
        private Label _statusLabel;
        private bool _deploying;

        public MainForm()
        {
            Text = "前端项目部署工具";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1000, 720);
            MinimumSize = new Size(920, 670);
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.FromArgb(244, 247, 251);
            AllowDrop = true;
            AutoScaleMode = AutoScaleMode.Dpi;

            BuildInterface();
            _logger.LineWritten += AppendLog;
            LoadProjects(null);
            _logger.Info("部署工具已启动。");
            if (_projects.Count == 0)
            {
                _logger.Warning("尚未配置项目，请点击“管理项目”添加第一个项目。");
            }
        }

        private void BuildInterface()
        {
            Label title = new Label
            {
                Text = "前端项目快速部署",
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                Location = new Point(28, 20),
                AutoSize = true
            };
            Controls.Add(title);

            Label subtitle = new Label
            {
                Text = "选择项目和 ZIP，自动解压并完整替换服务器上的旧目录",
                Location = new Point(31, 60),
                AutoSize = true,
                ForeColor = Color.FromArgb(92, 101, 116)
            };
            Controls.Add(subtitle);

            bool isAdmin = IsAdministrator();
            Label adminLabel = new Label
            {
                Text = isAdmin ? "● 已使用管理员权限" : "● 当前为普通权限",
                ForeColor = isAdmin ? Color.FromArgb(30, 135, 76) : Color.FromArgb(186, 108, 0),
                Location = new Point(745, 27),
                Size = new Size(210, 23),
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            Controls.Add(adminLabel);

            Button nginxButton = CreateButton("Nginx 管理", 811, 57, 143, 31);
            nginxButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            nginxButton.Click += delegate
            {
                using (NginxManagerForm dialog = new NginxManagerForm())
                {
                    dialog.ShowDialog(this);
                }
            };
            Controls.Add(nginxButton);

            Panel card = new Panel
            {
                Location = new Point(28, 95),
                Size = new Size(926, 312),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(card);

            card.Controls.Add(CreateLabel("项目", 22, 22));
            _projectCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(22, 47),
                Size = new Size(575, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                DisplayMember = "name"
            };
            _projectCombo.SelectedIndexChanged += delegate { RefreshTarget(); };
            card.Controls.Add(_projectCombo);

            _manageButton = CreateButton("管理项目", 616, 44, 125, 34);
            _manageButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _manageButton.Click += ManageProjects;
            card.Controls.Add(_manageButton);

            Button elevateButton = CreateButton("管理员重启", 758, 44, 143, 34);
            elevateButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            elevateButton.Enabled = !isAdmin;
            elevateButton.Click += RestartElevated;
            card.Controls.Add(elevateButton);

            card.Controls.Add(CreateLabel("部署内容", 22, 95));
            _typeCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(22, 120),
                Size = new Size(185, 28)
            };
            _typeCombo.Items.Add("主程序");
            _typeCombo.Items.Add("语言包");
            _typeCombo.SelectedIndex = 0;
            _typeCombo.SelectedIndexChanged += delegate { RefreshTarget(); };
            card.Controls.Add(_typeCombo);

            card.Controls.Add(CreateLabel("目标目录", 230, 95));
            _targetBox = new TextBox
            {
                Location = new Point(230, 120),
                Size = new Size(671, 28),
                ReadOnly = true,
                BackColor = Color.FromArgb(247, 249, 252),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            card.Controls.Add(_targetBox);

            card.Controls.Add(CreateLabel("部署压缩包（也可把 ZIP 直接拖到窗口）", 22, 169));
            _zipBox = new TextBox
            {
                Location = new Point(22, 194),
                Size = new Size(719, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            card.Controls.Add(_zipBox);

            _browseButton = CreateButton("选择 ZIP…", 758, 191, 143, 34);
            _browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _browseButton.Click += BrowseZip;
            card.Controls.Add(_browseButton);

            _backupCheck = new CheckBox
            {
                Text = "部署前备份当前版本",
                Checked = true,
                Location = new Point(22, 251),
                AutoSize = true
            };
            card.Controls.Add(_backupCheck);

            _stripCheck = new CheckBox
            {
                Text = "只有一个最外层文件夹时自动去掉该层（适合 dist.zip）",
                Checked = true,
                Location = new Point(230, 251),
                AutoSize = true
            };
            card.Controls.Add(_stripCheck);

            _deployButton = CreateButton("开始部署", 758, 244, 143, 44);
            _deployButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _deployButton.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            _deployButton.BackColor = Color.FromArgb(38, 103, 231);
            _deployButton.ForeColor = Color.White;
            _deployButton.FlatStyle = FlatStyle.Flat;
            _deployButton.FlatAppearance.BorderSize = 0;
            _deployButton.Click += async delegate { await DeployAsync(); };
            card.Controls.Add(_deployButton);

            Label logLabel = CreateLabel("部署日志", 28, 428);
            Controls.Add(logLabel);

            Button openBackupButton = CreateButton("打开备份目录", 819, 419, 135, 33);
            openBackupButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            openBackupButton.Click += delegate
            {
                AppPaths.EnsureFolders();
                Process.Start("explorer.exe", AppPaths.BackupRoot);
            };
            Controls.Add(openBackupButton);

            _logBox = new TextBox
            {
                Location = new Point(28, 459),
                Size = new Size(926, 174),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(22, 27, 34),
                ForeColor = Color.FromArgb(218, 225, 234),
                Font = new Font("Consolas", 9F)
            };
            Controls.Add(_logBox);

            _statusLabel = new Label
            {
                Text = "就绪",
                Location = new Point(28, 646),
                Size = new Size(926, 22),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.FromArgb(92, 101, 116)
            };
            Controls.Add(_statusLabel);

            DragEnter += MainDragEnter;
            DragDrop += MainDragDrop;
            FormClosing += MainFormClosing;
        }

        private static Label CreateLabel(string text, int x, int y)
        {
            return new Label { Text = text, Location = new Point(x, y), AutoSize = true };
        }

        private static Button CreateButton(string text, int x, int y, int width, int height)
        {
            return new Button { Text = text, Location = new Point(x, y), Size = new Size(width, height) };
        }

        private void LoadProjects(string selectedId)
        {
            _projects = _store.Load();
            _projectCombo.DataSource = null;
            _projectCombo.DataSource = _projects;
            _projectCombo.DisplayMember = "name";
            if (_projects.Count > 0)
            {
                int selectedIndex = 0;
                if (!string.IsNullOrWhiteSpace(selectedId))
                {
                    for (int i = 0; i < _projects.Count; i++)
                    {
                        if (string.Equals(_projects[i].id, selectedId, StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex = i;
                            break;
                        }
                    }
                }
                _projectCombo.SelectedIndex = selectedIndex;
            }
            RefreshTarget();
        }

        private void RefreshTarget()
        {
            ProjectConfig project = SelectedProject;
            if (project == null)
            {
                _targetBox.Text = string.Empty;
                return;
            }
            _targetBox.Text = string.Equals(Convert.ToString(_typeCombo.SelectedItem), "语言包", StringComparison.Ordinal)
                ? project.languagePath
                : project.mainPath;
        }

        private ProjectConfig SelectedProject
        {
            get { return _projectCombo.SelectedItem as ProjectConfig; }
        }

        private void ManageProjects(object sender, EventArgs e)
        {
            string selectedId = SelectedProject == null ? null : SelectedProject.id;
            using (ProjectManagerForm dialog = new ProjectManagerForm(_store, _projects))
            {
                dialog.ShowDialog(this);
            }
            LoadProjects(selectedId);
        }

        private void BrowseZip(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择前端部署 ZIP";
                dialog.Filter = "ZIP 压缩包 (*.zip)|*.zip";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _zipBox.Text = dialog.FileName;
                }
            }
        }

        private async Task DeployAsync()
        {
            if (_deploying)
            {
                return;
            }

            ProjectConfig project = SelectedProject;
            if (project == null)
            {
                MessageBox.Show(this, "请先点击“管理项目”添加项目。", "尚未配置项目", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string kind = Convert.ToString(_typeCombo.SelectedItem);
            if (string.Equals(kind, "语言包", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(_targetBox.Text))
            {
                MessageBox.Show(this, "这个项目没有配置语言包目录。", "没有语言包目录", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(_zipBox.Text))
            {
                MessageBox.Show(this, "请选择要部署的 ZIP 压缩包。", "未选择文件", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string target;
            try
            {
                target = DeploymentService.ValidateTargetPath(_targetBox.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "目标目录不安全", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string confirm = string.Format(
                "项目：{0}\r\n内容：{1}\r\n目标：{2}\r\n部署前备份：{3}\r\n\r\n确认后将完整替换目标目录，原目录会被删除。是否继续？",
                project.name,
                kind,
                target,
                _backupCheck.Checked ? "是" : "否");
            if (MessageBox.Show(this, confirm, "确认部署", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            DeploymentRequest request = new DeploymentRequest
            {
                ZipPath = _zipBox.Text,
                TargetPath = target,
                ProjectName = project.name,
                DeployKind = kind,
                StripSingleRoot = _stripCheck.Checked,
                CreateBackup = _backupCheck.Checked
            };

            SetDeploying(true);
            try
            {
                _logger.Info(string.Format("开始部署：项目={0}，内容={1}，目标={2}", project.name, kind, target));
                DeploymentService service = new DeploymentService(_logger);
                DeploymentResult result = await Task.Run(delegate { return service.Deploy(request); });
                _statusLabel.Text = "部署成功：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                string message = string.Format(
                    "部署成功。\r\n\r\n目标：{0}\r\n文件数：{1}\r\nSHA-256：{2}",
                    result.Target,
                    result.FileCount,
                    result.Hash);
                if (!string.IsNullOrWhiteSpace(result.Backup))
                {
                    message += "\r\n备份：" + result.Backup;
                }
                if (!string.IsNullOrWhiteSpace(result.CleanupWarning))
                {
                    message += "\r\n\r\n注意：" + result.CleanupWarning;
                }
                MessageBox.Show(this, message, "部署完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "部署失败：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                MessageBox.Show(
                    this,
                    "部署失败：\r\n" + ex.Message + "\r\n\r\n请查看日志。若提示权限不足，请使用“管理员重启”。",
                    "部署失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                SetDeploying(false);
            }
        }

        private void SetDeploying(bool value)
        {
            _deploying = value;
            _deployButton.Enabled = !value;
            _manageButton.Enabled = !value;
            _browseButton.Enabled = !value;
            _projectCombo.Enabled = !value;
            _typeCombo.Enabled = !value;
            UseWaitCursor = value;
            if (value)
            {
                _statusLabel.Text = "正在部署，请勿关闭窗口……";
            }
        }

        private void AppendLog(string line)
        {
            if (IsDisposed)
            {
                return;
            }
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLog), line);
                return;
            }
            _logBox.AppendText(line + Environment.NewLine);
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.ScrollToCaret();
        }

        private void MainDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void MainDragDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data == null ? null : e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0 && string.Equals(Path.GetExtension(files[0]), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                _zipBox.Text = files[0];
            }
            else
            {
                MessageBox.Show(this, "请拖入一个 ZIP 压缩包。", "文件类型不支持", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void RestartElevated(object sender, EventArgs e)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = AppPaths.Root
                };
                Process.Start(info);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "管理员启动失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_deploying)
            {
                MessageBox.Show(this, "部署进行中，请等待部署结束后再关闭。", "无法关闭", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
            }
        }

        private static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
