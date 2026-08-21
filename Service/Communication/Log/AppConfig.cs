using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.Service.Communication.Log
{
    public class AppConfig
    {

        /// <summary>环境名称</summary>
        public string Environment { get; set; } = "Development";

        /// <summary>日志配置</summary>
        public LoggerConfig LoggingConfig { get; set; } =
            new LoggerConfig
            {
                EnableConsole = true,
                LogDirectory = "Logs",
                MinLogLevel = LogLevel.Debug,
                LogFileName = "app.log",
                MaxFileSizeMB = 10
            };


    }
    public static class AppConfiguration
    {
        #region 公共属性
        /// <summary>
        /// 当前应用程序配置
        /// </summary>
        public static AppConfig Current { get; set; } = new AppConfig();
        #endregion
    }
}
