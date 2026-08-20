using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.Service.Communication.Sql
{
    public interface IDataStorageProcessor
    {
        /// <summary>
        /// 注册某种实体 Model 的后台消费落库任务
        /// </summary>
        void RegisterType<T>(int batchSize = 50, int flushIntervalMs = 1000) where T : class, new();

        /// <summary>
        /// 将解析好的数据模型推送入队（非阻塞）
        /// </summary>
        void Enqueue<T>(T data) where T : class, new();

        /// <summary>
        /// 停止处理器并强制将内存中剩余数据写入数据库
        /// </summary>
        void Stop();
    }
}
