using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace FrontendDeployTool
{
    internal sealed class DeploymentService
    {
        private const long MaxExpandedBytes = 20L * 1024L * 1024L * 1024L;
        private const int MaxEntryCount = 100000;
        private readonly DeployLogger _logger;

        public DeploymentService(DeployLogger logger)
        {
            _logger = logger;
        }

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string expanded = Environment.ExpandEnvironmentVariables(path.Trim());
            return Path.GetFullPath(expanded).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static string ValidateTargetPath(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw new InvalidOperationException("部署目录不能为空。");
            }
            if (!Path.IsPathRooted(targetPath))
            {
                throw new InvalidOperationException("部署目录必须是绝对路径。");
            }
            if (targetPath.IndexOfAny(new[] { '*', '?' }) >= 0)
            {
                throw new InvalidOperationException("部署目录不能包含通配符。");
            }

            string target = NormalizePath(targetPath);
            string root = Path.GetPathRoot(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(target, root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("不能把磁盘根目录或共享根目录作为部署目录。");
            }

            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrWhiteSpace(windows) && IsSameOrChild(target, windows))
            {
                throw new InvalidOperationException("不能部署到 Windows 系统目录。");
            }

            string[] exactBlocked =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
            };
            foreach (string blocked in exactBlocked)
            {
                if (!string.IsNullOrWhiteSpace(blocked) &&
                    string.Equals(target, NormalizePath(blocked), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("不能直接清空系统公共目录：" + blocked);
                }
            }

            if (IsSameOrChild(AppPaths.Root, target) || IsSameOrChild(target, AppPaths.Root))
            {
                throw new InvalidOperationException("部署目录不能是工具目录、其父目录或其子目录。");
            }
            if (IsSameOrChild(target, AppPaths.BackupRoot) || IsSameOrChild(AppPaths.BackupRoot, target))
            {
                throw new InvalidOperationException("部署目录不能与备份目录重叠。");
            }

            if (Directory.Exists(target))
            {
                FileAttributes attributes = File.GetAttributes(target);
                if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    throw new InvalidOperationException("部署目录不能是目录联接或符号链接。");
                }
            }
            else if (File.Exists(target))
            {
                throw new InvalidOperationException("目标路径是文件，不是目录。");
            }

            return target;
        }

        public DeploymentResult Deploy(DeploymentRequest request)
        {
            string target = ValidateTargetPath(request.TargetPath);
            string zipPath = NormalizePath(request.ZipPath);
            if (!File.Exists(zipPath))
            {
                throw new FileNotFoundException("找不到所选压缩包。", zipPath);
            }
            if (!string.Equals(Path.GetExtension(zipPath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("目前只支持 ZIP 压缩包。");
            }

            AppPaths.EnsureFolders();
            string workRoot = Path.Combine(Path.GetTempPath(), "FrontendDeployTool", Guid.NewGuid().ToString("N"));
            string extractRoot = Path.Combine(workRoot, "package");
            string stagePath = string.Empty;
            string backupPath = string.Empty;
            bool oldMoved = false;
            bool newTargetCreated = false;

            try
            {
                _logger.Info("正在校验压缩包：" + zipPath);
                string hash = GetSha256(zipPath);
                _logger.Info("SHA-256：" + hash);

                _logger.Info("正在解压到服务器临时目录……");
                ExtractResult extractResult = ExtractSafe(zipPath, extractRoot);
                string deploySource = GetDeploySource(extractRoot, request.StripSingleRoot);
                _logger.Info(string.Format(
                    "解压完成，共 {0} 个文件，{1:N2} MB。",
                    extractResult.FileCount,
                    extractResult.TotalBytes / 1024d / 1024d));
                if (!string.Equals(deploySource, extractRoot, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Info("已自动去掉压缩包最外层目录：" + Path.GetFileName(deploySource));
                }

                string parent = Path.GetDirectoryName(target);
                if (string.IsNullOrWhiteSpace(parent))
                {
                    throw new InvalidOperationException("无法确定部署目录的父目录。");
                }
                Directory.CreateDirectory(parent);

                if (request.CreateBackup && Directory.Exists(target))
                {
                    _logger.Info("正在备份当前版本……");
                    backupPath = CreateBackup(target, request.ProjectName, request.DeployKind);
                    _logger.Success("备份已保存：" + backupPath);
                }

                if (Directory.Exists(target))
                {
                    string targetLeaf = new DirectoryInfo(target).Name;
                    stagePath = Path.Combine(parent, string.Format(".{0}.deploy-old-{1}", targetLeaf, Guid.NewGuid().ToString("N")));
                    _logger.Info("正在移出旧目录（新版本失败时可立即恢复）……");
                    Directory.Move(target, stagePath);
                    oldMoved = true;
                }

                Directory.CreateDirectory(target);
                newTargetCreated = true;
                _logger.Info("正在写入新版本：" + target);
                CopyDirectory(deploySource, target);

                string cleanupWarning = string.Empty;
                if (oldMoved && Directory.Exists(stagePath))
                {
                    _logger.Info("新版本写入成功，正在彻底删除旧版本……");
                    try
                    {
                        Directory.Delete(stagePath, true);
                        oldMoved = false;
                    }
                    catch (Exception cleanupError)
                    {
                        cleanupWarning = "新版本已部署，但旧目录未能删除，请检查文件占用：" + stagePath + "。" + cleanupError.Message;
                        _logger.Warning(cleanupWarning);
                    }
                }

                _logger.Success(string.Format("部署完成：{0} / {1}", request.ProjectName, request.DeployKind));
                return new DeploymentResult
                {
                    Target = target,
                    Backup = backupPath,
                    Hash = hash,
                    FileCount = extractResult.FileCount,
                    CleanupWarning = cleanupWarning
                };
            }
            catch (Exception originalError)
            {
                _logger.Error("部署失败：" + originalError.Message);
                if (oldMoved && Directory.Exists(stagePath))
                {
                    _logger.Warning("正在自动恢复旧版本……");
                    try
                    {
                        if (newTargetCreated && Directory.Exists(target))
                        {
                            Directory.Delete(target, true);
                        }
                        Directory.Move(stagePath, target);
                        oldMoved = false;
                        _logger.Success("旧版本已恢复。");
                    }
                    catch (Exception rollbackError)
                    {
                        _logger.Error("自动恢复失败，旧目录位置：" + stagePath + "；错误：" + rollbackError.Message);
                        throw new InvalidOperationException(
                            "部署失败，并且自动恢复失败。原错误：" + originalError.Message + "；旧目录：" + stagePath,
                            rollbackError);
                    }
                }
                throw;
            }
            finally
            {
                if (Directory.Exists(workRoot))
                {
                    try
                    {
                        Directory.Delete(workRoot, true);
                    }
                    catch
                    {
                        _logger.Warning("临时目录清理失败：" + workRoot);
                    }
                }
            }
        }

        private ExtractResult ExtractSafe(string zipPath, string destination)
        {
            Directory.CreateDirectory(destination);
            string destinationRoot = NormalizePath(destination) + Path.DirectorySeparatorChar;
            int count = 0;
            long totalBytes = 0;

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                if (archive.Entries.Count > MaxEntryCount)
                {
                    throw new InvalidOperationException("压缩包文件数量超过 100000，已停止部署。");
                }

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string entryName = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    if (string.IsNullOrWhiteSpace(entryName) || IsMacMetadata(entryName))
                    {
                        continue;
                    }

                    string destinationPath = Path.GetFullPath(Path.Combine(destination, entryName));
                    if (!destinationPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("压缩包包含越界路径，已拒绝解压：" + entry.FullName);
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    totalBytes += entry.Length;
                    if (totalBytes > MaxExpandedBytes)
                    {
                        throw new InvalidOperationException("压缩包解压后超过 20GB，已停止部署。");
                    }

                    string parent = Path.GetDirectoryName(destinationPath);
                    Directory.CreateDirectory(parent);
                    using (Stream input = entry.Open())
                    using (FileStream output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        input.CopyTo(output);
                    }
                    File.SetLastWriteTime(destinationPath, entry.LastWriteTime.LocalDateTime);
                    count++;
                }
            }

            if (count == 0)
            {
                throw new InvalidOperationException("压缩包中没有可部署的文件。");
            }
            return new ExtractResult { FileCount = count, TotalBytes = totalBytes };
        }

        private static bool IsMacMetadata(string entryName)
        {
            return entryName.StartsWith("__MACOSX" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                   entryName.EndsWith(Path.DirectorySeparatorChar + ".DS_Store", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(entryName, ".DS_Store", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetDeploySource(string extractRoot, bool stripSingleRoot)
        {
            if (!stripSingleRoot)
            {
                return extractRoot;
            }

            string[] files = Directory.GetFiles(extractRoot, "*", SearchOption.TopDirectoryOnly);
            string[] directories = Directory.GetDirectories(extractRoot, "*", SearchOption.TopDirectoryOnly);
            return files.Length == 0 && directories.Length == 1 ? directories[0] : extractRoot;
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                string relative = directory.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                string targetFile = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                File.Copy(file, targetFile, true);
                File.SetLastWriteTimeUtc(targetFile, File.GetLastWriteTimeUtc(file));
            }
        }

        private static string CreateBackup(string target, string projectName, string deployKind)
        {
            string projectPart = SafeFilePart(projectName);
            string kindPart = SafeFilePart(deployKind);
            string folder = Path.Combine(AppPaths.BackupRoot, projectPart, kindPart);
            Directory.CreateDirectory(folder);
            string name = string.Format(
                "{0}_{1}_{2}_{3}.zip",
                projectPart,
                kindPart,
                DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                Guid.NewGuid().ToString("N").Substring(0, 6));
            string backupPath = Path.Combine(folder, name);
            ZipFile.CreateFromDirectory(target, backupPath, CompressionLevel.Optimal, false);
            return backupPath;
        }

        private static string SafeFilePart(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "未命名项目" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalid, '_');
            }
            return result;
        }

        private static string GetSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                {
                    builder.Append(value.ToString("X2"));
                }
                return builder.ToString();
            }
        }

        private static bool IsSameOrChild(string candidate, string parent)
        {
            string candidateFull = NormalizePath(candidate) + Path.DirectorySeparatorChar;
            string parentFull = NormalizePath(parent) + Path.DirectorySeparatorChar;
            return candidateFull.StartsWith(parentFull, StringComparison.OrdinalIgnoreCase);
        }
    }
}
