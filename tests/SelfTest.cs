using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace FrontendDeployTool
{
    internal static class SelfTest
    {
        private static int Main(string[] args)
        {
            string root = Path.Combine(Path.GetTempPath(), "FrontendDeployToolSelfTest", Guid.NewGuid().ToString("N"));
            string sourceParent = Path.Combine(root, "source");
            string dist = Path.Combine(sourceParent, "dist");
            string target = Path.Combine(root, "target-site");
            string zip = Path.Combine(root, "package.zip");

            try
            {
                Directory.CreateDirectory(Path.Combine(dist, "assets"));
                File.WriteAllText(Path.Combine(dist, "index.html"), "<html>new version</html>");
                File.WriteAllText(Path.Combine(dist, "assets", "app.js"), "console.log('ok');");
                Directory.CreateDirectory(target);
                File.WriteAllText(Path.Combine(target, "old-only.txt"), "old version");
                ZipFile.CreateFromDirectory(sourceParent, zip, CompressionLevel.Optimal, false);

                DeployLogger logger = new DeployLogger();
                logger.LineWritten += Console.WriteLine;
                DeploymentService service = new DeploymentService(logger);
                DeploymentResult result = service.Deploy(new DeploymentRequest
                {
                    ZipPath = zip,
                    TargetPath = target,
                    ProjectName = "自动测试项目",
                    DeployKind = "主程序",
                    StripSingleRoot = true,
                    CreateBackup = true
                });

                Require(!File.Exists(Path.Combine(target, "old-only.txt")), "旧文件仍然存在");
                Require(File.Exists(Path.Combine(target, "index.html")), "新 index.html 不存在");
                Require(File.Exists(Path.Combine(target, "assets", "app.js")), "新 assets/app.js 不存在");
                Require(File.Exists(result.Backup), "部署前备份不存在");
                Require(result.FileCount == 2, "部署文件数量不正确");
                Require(string.IsNullOrEmpty(result.CleanupWarning), "旧目录未被完整删除");

                string nginxSource = "http {\nserver {\nlocation / {\ntry_files $uri $uri/ /index.html; # } comment\n}\n}\n}";
                string nginxFormatted = NginxFormatter.Format(nginxSource);
                Require(nginxFormatted.Contains("    server {"), "Nginx 一级缩进不正确");
                Require(nginxFormatted.Contains("        location / {"), "Nginx 二级缩进不正确");
                Require(nginxFormatted.Contains("            try_files $uri"), "Nginx 三级缩进不正确");
                Require(nginxFormatted.Contains("        }"), "Nginx 结束括号缩进不正确");

                string chineseConfig = "# 中文注释\nevents {}\n";
                string gbkConfig = Path.Combine(root, "nginx-gbk.conf");
                string utf8Config = Path.Combine(root, "nginx-utf8.conf");
                File.WriteAllText(gbkConfig, chineseConfig, Encoding.GetEncoding(936));
                File.WriteAllText(utf8Config, chineseConfig, new UTF8Encoding(false));
                TextFileContent gbkContent = TextFileCodec.Read(gbkConfig, TextFileCodec.Auto);
                TextFileContent utf8Content = TextFileCodec.Read(utf8Config, TextFileCodec.Auto);
                Require(gbkContent.Text == chineseConfig, "GBK 配置解码后内容不一致");
                Require(gbkContent.EncodingName == TextFileCodec.Gbk, "GBK 配置编码识别错误");
                Require(utf8Content.Text == chineseConfig, "UTF-8 配置解码后内容不一致");
                Require(utf8Content.EncodingName == TextFileCodec.Utf8, "UTF-8 配置编码识别错误");

                if (args.Length > 0)
                {
                    string fakeNginx = args[0];
                    string validConfig = Path.Combine(root, "nginx-valid.conf");
                    string invalidConfig = Path.Combine(root, "nginx-invalid.conf");
                    File.WriteAllText(validConfig, "events {}\nhttp {}\n");
                    File.WriteAllText(invalidConfig, "INVALID_TEST_CONFIG\n");
                    NginxController controller = new NginxController(fakeNginx, validConfig);
                    NginxCommandResult validResult = controller.TestConfig();
                    NginxCommandResult invalidResult = controller.TestConfig(invalidConfig);
                    Require(validResult.Success, "Nginx 有效配置检测失败");
                    Require(!invalidResult.Success, "Nginx 无效配置未被拦截");
                }

                Console.WriteLine("SELF_TEST_OK");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SELF_TEST_FAILED: " + ex);
                return 1;
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    try { Directory.Delete(root, true); }
                    catch { }
                }
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
