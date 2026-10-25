using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("毛玻璃")]
[assembly: AssemblyDescription("毛玻璃 - 屏幕隐私遮挡工具")]
[assembly: AssemblyProduct("毛玻璃")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace FrostGlass
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew = false;
            using (Mutex mutex = new Mutex(true, "FrostGlass.SingleInstance.Mutex", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "毛玻璃已经在运行。\n\n它可以在屏幕右下角的系统托盘中找到，\n也可以按 Ctrl+Alt+G 显示或隐藏。",
                        "毛玻璃",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
                }
                catch
                {
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += Application_ThreadException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                AppSettings settings = AppSettings.Load();
                using (TrayAppContext context = new TrayAppContext(settings))
                {
                    Application.Run(context);
                }

                GC.KeepAlive(mutex);
            }
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Log.Write("界面线程发生未处理异常", e.Exception);
            MessageBox.Show("毛玻璃遇到了一点问题：\n" + e.Exception.Message +
                "\n\n程序会继续运行。", "毛玻璃",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Write("程序发生未处理异常", e.ExceptionObject as Exception);
        }
    }
}
