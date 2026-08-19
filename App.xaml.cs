using System.Configuration;
using System.Windows;
using WpfApp1.Models;
using WpfApp1.Service.Communication.Sql;
using SqlSugar;
namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static DataStorageProcessor StorageProcessor { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 初始化 SqlSugar 数据库帮助类
            var sqlSugarHelper = new SqlsugarHelper("DeviceMonitorDb", DbType.SqlServer, ".");
            sqlSugarHelper.CreateDatabase();

            // 自动创建 HightSettingModel 对应的表
            sqlSugarHelper.CreateTables(typeof(HightSettingModel));

            // 2. 初始化后台存储处理器，并注册落库规则（如：满 20 条或每 1 秒批量落库一次）
            StorageProcessor = new DataStorageProcessor(sqlSugarHelper);
            StorageProcessor.RegisterType<HightSettingModel>(batchSize: 1, flushIntervalMs: 1000);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            StorageProcessor?.Stop();
            base.OnExit(e);
        }
    }

}
