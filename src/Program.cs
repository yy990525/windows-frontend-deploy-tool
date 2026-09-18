using System;
using System.Threading;
using System.Windows.Forms;

namespace FrontendDeployTool
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "FrontendDeployTool.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("部署工具已经在运行。", "前端项目部署工具", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                try
                {
                    AppPaths.EnsureFolders();
                    Application.Run(new MainForm());
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "程序启动失败：\r\n" + ex.Message,
                        "前端项目部署工具",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }
    }
}
