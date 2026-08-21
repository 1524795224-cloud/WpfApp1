using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using System.Runtime.InteropServices;
using System.Windows;
using WpfApp1.Models;
using WpfApp1.Service.Communication;
using WpfApp1.Service.Communication.Log;
using WpfApp1.Service.Communication.Sql;
namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static DataStorageProcessor StorageProcessor { get; private set; }
        public static Semens Semens { get; private set; }
        public static Logger Logger { get; private set; }

        #region Win32 API

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);

        private delegate bool ConsoleEventDelegate(int eventType);
        private static ConsoleEventDelegate _consoleHandler;

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern bool DeleteMenu(IntPtr hMenu, uint uPosition, uint uFlags);

        // 更稳定的方式：修改窗口样式移除关闭按钮
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const uint SC_CLOSE = 0xF060;
        private const uint MF_BYCOMMAND = 0x00000000;

        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x00080000;

        private IntPtr _consoleHandle;

        #endregion
        protected override void OnStartup(StartupEventArgs e)
        {
          
            base.OnStartup(e);
            // 初始化日志（先于其他所有操作）
            Logger = new Logger(AppConfiguration.Current.LoggingConfig);
            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                if (ev.ExceptionObject is Exception ex)
                    Logger.Error($"未处理异常: {ex.Message}\n{ex.StackTrace}");
            };

            DispatcherUnhandledException += (s, ev) =>
            {
                Logger.Error($"UI线程未处理异常: {ev.Exception.Message}\n{ev.Exception.StackTrace}");
                ev.Handled = true; // 阻止应用崩溃
            };
            #region 初始化控制台
            AllocConsole();
            _consoleHandle = GetConsoleWindow();

            DisableConsoleCloseButton();

            _consoleHandler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(_consoleHandler, true);

            Logger.Debug("程序启动，控制台已显示。");
            #endregion
            Logger.Startup("🚀 应用程序启动开始");
            Logger.Startup($"启动参数: {string.Join(" ", e.Args)}");
            Logger.Startup($"工作目录: {Environment.CurrentDirectory}");
            Semens = new Semens(HslCommunication.Profinet.Siemens.SiemensPLCS.S1200,2000,2500);
            //创建数据库和表，并初始化后台存储处理器
            DbCreate();
           
        }

        protected override void OnExit(ExitEventArgs e)
        {
            StorageProcessor?.Stop();
            Semens.Disconnect();
            Logger?.Info($"应用程序退出，退出码: {e.ApplicationExitCode}");
            Logger?.Dispose();
            base.OnExit(e);
        }
        /// <summary>
        /// 创建数据库和表，并初始化后台存储处理器
        /// </summary>
        private void DbCreate()
        {
            // 1. 初始化 SqlSugar 数据库帮助类
            var sqlSugarHelper = new SqlsugarHelper("DeviceMonitorDb", DbType.Sqlite, ".");
            sqlSugarHelper.CreateDatabase();

            // 自动创建 HightSettingModel 对应的表
            sqlSugarHelper.CreateTables(typeof(HightSettingModel));

            // 2. 初始化后台存储处理器，并注册落库规则（如：满 20 条或每 1 秒批量落库一次）
            StorageProcessor = new DataStorageProcessor(sqlSugarHelper);
            StorageProcessor.RegisterType<HightSettingModel>(batchSize: 1, flushIntervalMs: 1000);
        }
        #region 控制台显示控制

        private void ShowConsole()
        {
            if (_consoleHandle == IntPtr.Zero)
                _consoleHandle = GetConsoleWindow();
            if (_consoleHandle != IntPtr.Zero)
                ShowWindow(_consoleHandle, SW_SHOW);
        }

        private void HideConsole()
        {
            if (_consoleHandle == IntPtr.Zero)
                _consoleHandle = GetConsoleWindow();
            if (_consoleHandle != IntPtr.Zero)
                ShowWindow(_consoleHandle, SW_HIDE);
        }

        private void DisableConsoleCloseButton()
        {
            if (_consoleHandle == IntPtr.Zero)
                return;

            try
            {
                int style = GetWindowLong(_consoleHandle, GWL_STYLE);
                SetWindowLong(_consoleHandle, GWL_STYLE, style & ~WS_SYSMENU);
            }
            catch (Exception ex)
            {
                Logger?.Warning($"DisableConsoleCloseButton 失败: {ex.Message}");
            }
        }

        #endregion
        #region 控制台事件回调

        private bool ConsoleEventCallback(int eventType)
        {
            const int CTRL_C_EVENT = 0;
            const int CTRL_BREAK_EVENT = 1;
            const int CTRL_CLOSE_EVENT = 2;
            const int CTRL_LOGOFF_EVENT = 5;
            const int CTRL_SHUTDOWN_EVENT = 6;

            switch (eventType)
            {
                case CTRL_CLOSE_EVENT:
                    HideConsole();
                    return true;
                default:
                    return false;
            }
        }

        #endregion
    }
}


