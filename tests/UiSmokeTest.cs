using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace FrontendDeployTool
{
    internal static class UiSmokeTest
    {
        [STAThread]
        private static int Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (MainForm main = new MainForm())
                using (NginxManagerForm nginx = new NginxManagerForm())
                {
                    Require(ContainsText(main.Controls, "Nginx 管理"), "主窗口缺少 Nginx 管理入口");
                    Require(ContainsText(nginx.Controls, "格式化缩进"), "Nginx 窗口缺少格式化按钮");
                    Require(ContainsText(nginx.Controls, "检测并保存"), "Nginx 窗口缺少保存按钮");
                    Require(ContainsText(nginx.Controls, "启用 Nginx"), "Nginx 窗口缺少启用按钮");
                    Require(ContainsText(nginx.Controls, "停用 Nginx"), "Nginx 窗口缺少停用按钮");
                    Require(ContainsText(nginx.Controls, "重启/重载"), "Nginx 窗口缺少重载按钮");
                    Require(ContainsText(nginx.Controls, "按编码重载"), "Nginx 窗口缺少编码重载按钮");
                }
                Console.WriteLine("UI_SMOKE_TEST_OK");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("UI_SMOKE_TEST_FAILED: " + ex);
                return 1;
            }
        }

        private static bool ContainsText(Control.ControlCollection controls, string text)
        {
            foreach (Control control in controls)
            {
                if (string.Equals(control.Text, text, StringComparison.Ordinal))
                {
                    return true;
                }
                if (control.HasChildren && ContainsText(control.Controls, text))
                {
                    return true;
                }
            }
            return false;
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
