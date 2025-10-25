using System;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using System.IO;

namespace MultiModelChat
{
    internal static class Program
    {
        private static string StartupLogPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_startup.log");
        private static void Log(string message)
        {
            try { File.AppendAllText(StartupLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n"); } catch { }
        }

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Log("Entering Main");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Cef.EnableHighDPISupport();
            var settings = new CefSettings();
            settings.LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cef.log");
            settings.LogSeverity = LogSeverity.Verbose;
            settings.CefCommandLineArgs.Add("disable-gpu", "1");
            settings.CefCommandLineArgs.Add("disable-gpu-compositing", "1");
            settings.CefCommandLineArgs.Add("disable-features", "UseOzonePlatform,VaapiVideoDecoder,CanvasOopRasterization");
            settings.BrowserSubprocessPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CefSharp.BrowserSubprocess.exe");
            try
            {
                Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);
                Log("Cef.Initialize succeeded");
            }
            catch (Exception initEx)
            {
                Log("Cef.Initialize failed: " + initEx.Message);
                MessageBox.Show("浏览器初始化失败：" + initEx.Message + "\n请确认已安装 .NET Framework 4.8 与 VC++ 2015-2019 运行库。", "启动错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                if (ex != null)
                {
                    Log("UnhandledException: " + ex.Message);
                    MessageBox.Show("未处理异常：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            try
            {
                Log("Before Application.Run");
                Application.Run(new MainWindow());
                Log("After Application.Run");
            }
            catch (Exception runEx)
            {
                Log("Application.Run exception: " + runEx.Message);
                MessageBox.Show("启动失败：" + runEx.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Log("Cef.Shutdown");
                Cef.Shutdown();
            }
        }
    }
}
