using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace FrontendDeployTool
{
    internal sealed class ProjectConfig
    {
        public string id { get; set; }
        public string name { get; set; }
        public string mainPath { get; set; }
        public string languagePath { get; set; }

        public ProjectConfig Clone()
        {
            return new ProjectConfig
            {
                id = id,
                name = name,
                mainPath = mainPath,
                languagePath = languagePath
            };
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(name) ? "未命名项目" : name;
        }
    }

    internal static class AppPaths
    {
        public static readonly string Root = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        public static readonly string ConfigFile = Path.Combine(Root, "projects.json");
        public static readonly string LogRoot = Path.Combine(Root, "logs");
        public static readonly string BackupRoot = Path.Combine(Root, "backups");
        public static readonly string NginxConfigBackupRoot = Path.Combine(Root, "nginx-config-backups");
        public static readonly string SettingsFile = Path.Combine(Root, "settings.json");

        public static void EnsureFolders()
        {
            Directory.CreateDirectory(LogRoot);
            Directory.CreateDirectory(BackupRoot);
            Directory.CreateDirectory(NginxConfigBackupRoot);
        }
    }

    internal sealed class AppSettings
    {
        public string nginxExePath { get; set; }
        public string nginxConfigPath { get; set; }
    }

    internal sealed class SettingsStore
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public AppSettings Load()
        {
            if (!File.Exists(AppPaths.SettingsFile))
            {
                AppSettings empty = new AppSettings { nginxExePath = string.Empty, nginxConfigPath = string.Empty };
                Save(empty);
                return empty;
            }

            string json = File.ReadAllText(AppPaths.SettingsFile, Encoding.UTF8);
            AppSettings settings;
            try
            {
                settings = _serializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("settings.json 格式错误：" + ex.Message, ex);
            }
            settings.nginxExePath = settings.nginxExePath ?? string.Empty;
            settings.nginxConfigPath = settings.nginxConfigPath ?? string.Empty;
            return settings;
        }

        public void Save(AppSettings settings)
        {
            string json = _serializer.Serialize(settings);
            File.WriteAllText(AppPaths.SettingsFile, json, new UTF8Encoding(false));
        }
    }

    internal sealed class ProjectStore
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public List<ProjectConfig> Load()
        {
            if (!File.Exists(AppPaths.ConfigFile))
            {
                File.WriteAllText(AppPaths.ConfigFile, "[]", new UTF8Encoding(false));
                return new List<ProjectConfig>();
            }

            string json = File.ReadAllText(AppPaths.ConfigFile, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<ProjectConfig>();
            }

            List<ProjectConfig> projects;
            try
            {
                projects = _serializer.Deserialize<List<ProjectConfig>>(json) ?? new List<ProjectConfig>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("projects.json 格式错误：" + ex.Message, ex);
            }

            foreach (ProjectConfig project in projects)
            {
                if (string.IsNullOrWhiteSpace(project.id))
                {
                    project.id = Guid.NewGuid().ToString("N");
                }
                project.name = project.name ?? string.Empty;
                project.mainPath = project.mainPath ?? string.Empty;
                project.languagePath = project.languagePath ?? string.Empty;
            }
            return projects;
        }

        public void Save(IList<ProjectConfig> projects)
        {
            string json = _serializer.Serialize(projects);
            File.WriteAllText(AppPaths.ConfigFile, json, new UTF8Encoding(false));
        }
    }

    internal sealed class DeploymentRequest
    {
        public string ZipPath { get; set; }
        public string TargetPath { get; set; }
        public string ProjectName { get; set; }
        public string DeployKind { get; set; }
        public bool StripSingleRoot { get; set; }
        public bool CreateBackup { get; set; }
    }

    internal sealed class DeploymentResult
    {
        public string Target { get; set; }
        public string Backup { get; set; }
        public string Hash { get; set; }
        public int FileCount { get; set; }
        public string CleanupWarning { get; set; }
    }

    internal sealed class ExtractResult
    {
        public int FileCount { get; set; }
        public long TotalBytes { get; set; }
    }

    internal sealed class DeployLogger
    {
        public event Action<string> LineWritten;

        public void Info(string message) { Write("INFO", message); }
        public void Warning(string message) { Write("WARN", message); }
        public void Error(string message) { Write("ERROR", message); }
        public void Success(string message) { Write("SUCCESS", message); }

        private void Write(string level, string message)
        {
            string line = string.Format("{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}", DateTime.Now, level, message);
            try
            {
                AppPaths.EnsureFolders();
                string logPath = Path.Combine(AppPaths.LogRoot, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                File.AppendAllText(logPath, line + Environment.NewLine, new UTF8Encoding(false));
            }
            catch
            {
                // 日志失败不能影响部署和回滚。
            }

            Action<string> handler = LineWritten;
            if (handler != null)
            {
                handler(line);
            }
        }
    }
}
