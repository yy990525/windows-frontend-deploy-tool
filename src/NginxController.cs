using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace FrontendDeployTool
{
    internal sealed class NginxCommandResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; }
    }

    internal sealed class NginxController
    {
        private readonly string _exePath;
        private readonly string _configPath;

        public NginxController(string exePath, string configPath)
        {
            _exePath = Path.GetFullPath(exePath);
            _configPath = Path.GetFullPath(configPath);
        }

        public void ValidateFiles()
        {
            if (!File.Exists(_exePath))
            {
                throw new FileNotFoundException("找不到 nginx.exe。", _exePath);
            }
            if (!string.Equals(Path.GetFileName(_exePath), "nginx.exe", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("请选择 nginx.exe 文件。");
            }
            if (!File.Exists(_configPath))
            {
                throw new FileNotFoundException("找不到 nginx.conf。", _configPath);
            }
        }

        public NginxCommandResult TestConfig()
        {
            return TestConfig(_configPath);
        }

        public NginxCommandResult TestConfig(string configPath)
        {
            ValidateExecutable();
            return RunAndWait("-t " + CommonArguments(configPath), 30000);
        }

        public NginxCommandResult Start()
        {
            ValidateFiles();
            NginxCommandResult test = TestConfig();
            if (!test.Success)
            {
                return test;
            }
            if (IsRunning())
            {
                return new NginxCommandResult { Success = true, ExitCode = 0, Output = "Nginx 已经处于运行状态。" };
            }

            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = _exePath,
                Arguments = CommonArguments(_configPath),
                WorkingDirectory = Path.GetDirectoryName(_exePath),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process process = Process.Start(info);
            Thread.Sleep(900);
            bool exited = process.HasExited;
            int exitCode = exited ? process.ExitCode : 0;
            process.Dispose();
            if (exited && exitCode != 0)
            {
                return new NginxCommandResult
                {
                    Success = false,
                    ExitCode = exitCode,
                    Output = "Nginx 启动后立即退出，退出码：" + exitCode + "。请检查 Nginx error.log。"
                };
            }
            return new NginxCommandResult { Success = true, ExitCode = 0, Output = "Nginx 启动命令已执行。" };
        }

        public NginxCommandResult Stop()
        {
            ValidateFiles();
            if (!IsRunning())
            {
                return new NginxCommandResult { Success = true, ExitCode = 0, Output = "Nginx 当前未运行。" };
            }
            return RunAndWait("-s stop " + CommonArguments(_configPath), 15000);
        }

        public NginxCommandResult Reload()
        {
            ValidateFiles();
            NginxCommandResult test = TestConfig();
            if (!test.Success)
            {
                return test;
            }
            if (!IsRunning())
            {
                return Start();
            }
            return RunAndWait("-s reload " + CommonArguments(_configPath), 15000);
        }

        public bool IsRunning()
        {
            string processName = Path.GetFileNameWithoutExtension(_exePath);
            Process[] processes = Process.GetProcessesByName(processName);
            try
            {
                foreach (Process process in processes)
                {
                    try
                    {
                        string runningPath = process.MainModule.FileName;
                        if (string.Equals(Path.GetFullPath(runningPath), _exePath, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // 权限不足时无法读取 MainModule；同名进程仍作为运行状态提示。
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
        }

        private void ValidateExecutable()
        {
            if (!File.Exists(_exePath))
            {
                throw new FileNotFoundException("找不到 nginx.exe。", _exePath);
            }
            if (!string.Equals(Path.GetFileName(_exePath), "nginx.exe", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("请选择 nginx.exe 文件。");
            }
        }

        private NginxCommandResult RunAndWait(string arguments, int timeoutMilliseconds)
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = _exePath,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(_exePath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (Process process = Process.Start(info))
            {
                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    try { process.Kill(); }
                    catch { }
                    return new NginxCommandResult
                    {
                        Success = false,
                        ExitCode = -1,
                        Output = "Nginx 命令执行超时。"
                    };
                }

                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();
                string output = (standardOutput + Environment.NewLine + standardError).Trim();
                if (string.IsNullOrWhiteSpace(output))
                {
                    output = process.ExitCode == 0 ? "命令执行成功。" : "命令执行失败，退出码：" + process.ExitCode;
                }
                return new NginxCommandResult
                {
                    Success = process.ExitCode == 0,
                    ExitCode = process.ExitCode,
                    Output = output
                };
            }
        }

        private string CommonArguments(string configPath)
        {
            string prefix = Path.GetDirectoryName(_exePath).Replace('\\', '/') + "/";
            string config = Path.GetFullPath(configPath).Replace('\\', '/');
            return "-p " + Quote(prefix) + " -c " + Quote(config);
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
