using System.Windows;
using WpfApp1.Models;
using WpfApp1.Service.Communication.Sql;
using SqlSugar;
using WpfApp1.Service.Communication;
using Microsoft.Extensions.DependencyInjection;
namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static DataStorageProcessor StorageProcessor { get; private set; }
        public static Semens Semens { get; private set; }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Semens = new Semens(HslCommunication.Profinet.Siemens.SiemensPLCS.S1200,2000,2500);
            //创建数据库和表，并初始化后台存储处理器
            DbCreate();
           
        }

        protected override void OnExit(ExitEventArgs e)
        {
            StorageProcessor?.Stop();
            Semens.Disconnect();
            base.OnExit(e);
        }
        /// <summary>
        /// 创建数据库和表，并初始化后台存储处理器
        /// </summary>
        private void DbCreate()
        {
            // 1. 初始化 SqlSugar 数据库帮助类
            var sqlSugarHelper = new SqlsugarHelper("DeviceMonitorDb", DbType.SqlServer, ".");
            sqlSugarHelper.CreateDatabase();

            // 自动创建 HightSettingModel 对应的表
            sqlSugarHelper.CreateTables(typeof(HightSettingModel));

            // 2. 初始化后台存储处理器，并注册落库规则（如：满 20 条或每 1 秒批量落库一次）
            StorageProcessor = new DataStorageProcessor(sqlSugarHelper);
            StorageProcessor.RegisterType<HightSettingModel>(batchSize: 1, flushIntervalMs: 1000);
        }
    }

}
