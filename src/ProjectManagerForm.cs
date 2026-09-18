using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FrontendDeployTool
{
    internal sealed class ProjectManagerForm : Form
    {
        private readonly ProjectStore _store;
        private readonly List<ProjectConfig> _projects;
        private readonly ListBox _projectList;
        private readonly TextBox _nameBox;
        private readonly TextBox _mainPathBox;
        private readonly TextBox _languagePathBox;
        private bool _loading;

        public IList<ProjectConfig> Projects { get { return _projects; } }

        public ProjectManagerForm(ProjectStore store, IList<ProjectConfig> projects)
        {
            _store = store;
            _projects = new List<ProjectConfig>();
            foreach (ProjectConfig project in projects)
            {
                _projects.Add(project.Clone());
            }

            Text = "项目配置";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(880, 540);
            MinimumSize = new Size(800, 500);
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.FromArgb(247, 249, 252);

            _projectList = new ListBox
            {
                Location = new Point(18, 18),
                Size = new Size(235, 405),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
                DisplayMember = "name"
            };
            Controls.Add(_projectList);

            Controls.Add(CreateLabel("项目名称", 280, 22));
            _nameBox = CreateTextBox(280, 47, 550);
            Controls.Add(_nameBox);

            Controls.Add(CreateLabel("主程序部署目录（部署时会完整替换）", 280, 96));
            _mainPathBox = CreateTextBox(280, 121, 455);
            Controls.Add(_mainPathBox);
            Controls.Add(CreateBrowseButton(_mainPathBox, "选择主程序部署目录", 747, 118));

            Controls.Add(CreateLabel("语言包部署目录（没有语言包的项目可留空）", 280, 171));
            _languagePathBox = CreateTextBox(280, 196, 455);
            Controls.Add(_languagePathBox);
            Controls.Add(CreateBrowseButton(_languagePathBox, "选择语言包部署目录", 747, 193));

            Label help = new Label
            {
                Text = "每个项目可以配置两个完全独立的目标目录。\r\n语言包部署不会修改主程序目录；目录也可以直接手动输入。",
                Location = new Point(280, 251),
                Size = new Size(550, 55),
                ForeColor = Color.FromArgb(92, 101, 116)
            };
            Controls.Add(help);

            Button addButton = CreateButton("新增项目", 18, 438, 110);
            addButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            addButton.Click += AddProject;
            Controls.Add(addButton);

            Button deleteButton = CreateButton("删除项目", 143, 438, 110);
            deleteButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            deleteButton.Click += DeleteProject;
            Controls.Add(deleteButton);

            Button saveButton = CreateButton("保存项目", 602, 438, 110);
            saveButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            saveButton.Click += SaveCurrentProject;
            Controls.Add(saveButton);

            Button completeButton = CreateButton("完成", 727, 438, 103);
            completeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            completeButton.Click += Complete;
            Controls.Add(completeButton);

            _projectList.SelectedIndexChanged += ProjectSelectionChanged;
            RefreshProjectList(0);
        }

        private static Label CreateLabel(string text, int x, int y)
        {
            return new Label { Text = text, Location = new Point(x, y), AutoSize = true };
        }

        private TextBox CreateTextBox(int x, int y, int width)
        {
            return new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
        }

        private static Button CreateButton(string text, int x, int y, int width)
        {
            return new Button { Text = text, Location = new Point(x, y), Size = new Size(width, 35) };
        }

        private Button CreateBrowseButton(TextBox target, string title, int x, int y)
        {
            Button button = CreateButton("选择…", x, y, 83);
            button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button.Click += delegate
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = title;
                    dialog.ShowNewFolderButton = true;
                    if (Directory.Exists(target.Text))
                    {
                        dialog.SelectedPath = target.Text;
                    }
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        target.Text = dialog.SelectedPath;
                    }
                }
            };
            return button;
        }

        private void ProjectSelectionChanged(object sender, EventArgs e)
        {
            if (_projectList.SelectedIndex < 0 || _projectList.SelectedIndex >= _projects.Count)
            {
                return;
            }

            _loading = true;
            ProjectConfig project = _projects[_projectList.SelectedIndex];
            _nameBox.Text = project.name;
            _mainPathBox.Text = project.mainPath;
            _languagePathBox.Text = project.languagePath;
            _loading = false;
        }

        private void AddProject(object sender, EventArgs e)
        {
            ProjectConfig project = new ProjectConfig
            {
                id = Guid.NewGuid().ToString("N"),
                name = "新项目",
                mainPath = string.Empty,
                languagePath = string.Empty
            };
            _projects.Add(project);
            RefreshProjectList(_projects.Count - 1);
            _nameBox.SelectAll();
            _nameBox.Focus();
        }

        private void SaveCurrentProject(object sender, EventArgs e)
        {
            if (_projectList.SelectedIndex < 0)
            {
                MessageBox.Show(this, "请先新增或选择一个项目。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                ProjectConfig project = _projects[_projectList.SelectedIndex];
                ApplyEditorToProject(project);
                ValidateAllProjects();
                _store.Save(_projects);
                int selected = _projectList.SelectedIndex;
                RefreshProjectList(selected);
                MessageBox.Show(this, "项目配置已保存。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "配置未保存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void DeleteProject(object sender, EventArgs e)
        {
            int index = _projectList.SelectedIndex;
            if (index < 0)
            {
                return;
            }

            ProjectConfig project = _projects[index];
            DialogResult answer = MessageBox.Show(
                this,
                "确定删除项目配置“" + project.name + "”吗？这不会删除服务器上的项目文件。",
                "删除项目配置",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            _projects.RemoveAt(index);
            _store.Save(_projects);
            RefreshProjectList(Math.Min(index, _projects.Count - 1));
            if (_projects.Count == 0)
            {
                _nameBox.Clear();
                _mainPathBox.Clear();
                _languagePathBox.Clear();
            }
        }

        private void Complete(object sender, EventArgs e)
        {
            if (_projectList.SelectedIndex >= 0 && EditorHasChanges())
            {
                DialogResult answer = MessageBox.Show(
                    this,
                    "当前项目有尚未保存的修改，是否保存后关闭？",
                    "保存修改",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel)
                {
                    return;
                }
                if (answer == DialogResult.Yes)
                {
                    try
                    {
                        ApplyEditorToProject(_projects[_projectList.SelectedIndex]);
                        ValidateAllProjects();
                        _store.Save(_projects);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, ex.Message, "配置未保存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void ApplyEditorToProject(ProjectConfig project)
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text) || string.IsNullOrWhiteSpace(_mainPathBox.Text))
            {
                throw new InvalidOperationException("项目名称和主程序部署目录不能为空。");
            }

            string mainPath = DeploymentService.NormalizePath(_mainPathBox.Text);
            string languagePath = string.IsNullOrWhiteSpace(_languagePathBox.Text)
                ? string.Empty
                : DeploymentService.NormalizePath(_languagePathBox.Text);
            DeploymentService.ValidateTargetPath(mainPath);
            if (!string.IsNullOrWhiteSpace(languagePath))
            {
                DeploymentService.ValidateTargetPath(languagePath);
                if (string.Equals(mainPath, languagePath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("主程序目录和语言包目录不能相同，否则部署语言包会删除主程序。");
                }
            }

            project.name = _nameBox.Text.Trim();
            project.mainPath = mainPath;
            project.languagePath = languagePath;
        }

        private void ValidateAllProjects()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ProjectConfig project in _projects)
            {
                if (string.IsNullOrWhiteSpace(project.name) || string.IsNullOrWhiteSpace(project.mainPath))
                {
                    throw new InvalidOperationException("所有项目都必须填写项目名称和主程序部署目录。");
                }
                if (!names.Add(project.name.Trim()))
                {
                    throw new InvalidOperationException("项目名称不能重复：" + project.name);
                }
            }
        }

        private bool EditorHasChanges()
        {
            if (_loading || _projectList.SelectedIndex < 0)
            {
                return false;
            }
            ProjectConfig project = _projects[_projectList.SelectedIndex];
            return !string.Equals(project.name ?? string.Empty, _nameBox.Text.Trim(), StringComparison.Ordinal) ||
                   !string.Equals(project.mainPath ?? string.Empty, _mainPathBox.Text.Trim(), StringComparison.Ordinal) ||
                   !string.Equals(project.languagePath ?? string.Empty, _languagePathBox.Text.Trim(), StringComparison.Ordinal);
        }

        private void RefreshProjectList(int selectedIndex)
        {
            _loading = true;
            _projectList.DataSource = null;
            _projectList.DataSource = _projects;
            _projectList.DisplayMember = "name";
            if (_projects.Count > 0)
            {
                _projectList.SelectedIndex = Math.Max(0, Math.Min(selectedIndex, _projects.Count - 1));
            }
            _loading = false;
            ProjectSelectionChanged(this, EventArgs.Empty);
        }
    }
}
