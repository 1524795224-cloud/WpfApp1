using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace WpfApp1.Service.Communication.Sql
{
    /// <summary>
    /// 高性能后台通用数据异步落库处理器
    /// 基于 Channel 实现生产者-消费者模式，支持多 Model 自动批量刷盘
    /// </summary>
    public class DataStorageProcessor: IDataStorageProcessor
    {
        private readonly ISqlsugarHelper _sqlSugarHelper;
        // 存储不同 Model 类型对应的 Channel 消费通道
        private readonly ConcurrentDictionary<Type, object> _channels = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly List<Task> _consumerTasks = new();

        public DataStorageProcessor(ISqlsugarHelper sqlSugarHelper)
        {
            _sqlSugarHelper = sqlSugarHelper ?? throw new ArgumentNullException(nameof(sqlSugarHelper));
        }

        /// <summary>
        /// 注册某种实体 Model 的后台消费落库任务
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="batchSize">单次批量写入的最大条数（默认 50 条）</param>
        /// <param name="flushIntervalMs">定时强制刷盘间隔时间（毫秒，默认 1000ms）</param>
        public void RegisterType<T>(int batchSize = 50, int flushIntervalMs = 1000) where T : class, new()
        {
            var channel = GetOrCreateChannel<T>();

            // 启动独立消费后台 Task
            var consumerTask = Task.Run(() => StartConsumerLoopAsync(channel, batchSize, flushIntervalMs, _cts.Token));
            _consumerTasks.Add(consumerTask);
        }

        /// <summary>
        /// 将解析好的数据模型推送入队（耗时 < 0.01ms，绝不阻塞通信和 UI 线程）
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="data">要保存的实体对象</param>
        public void Enqueue<T>(T data) where T : class, new()
        {
            if (data == null) return;

            var channel = GetOrCreateChannel<T>();
            channel.Writer.TryWrite(data);
        }

        /// <summary>
        /// 停止处理器并强行将内存队列中剩余的所有数据写入数据库（建议在 App.OnExit 中调用）
        /// </summary>
        public void Stop()
        {
            try
            {
                // 1. 标记所有 Channel 不再接收新数据（优雅告知消费者写完即止）
                foreach (var channelObj in _channels.Values)
                {
                    var writerProp = channelObj.GetType().GetProperty("Writer");
                    var writer = writerProp?.GetValue(channelObj);
                    writer?.GetType().GetMethod("Complete")?.Invoke(writer, null);
                }

                // 2. 等待所有消费任务处理完毕退出（不主动调用 _cts.Cancel，靠 Complete 自然结束）
                var allTasks = Task.WhenAll(_consumerTasks);

                // 最多等待 3 秒收尾，避免无限卡死主线程
                allTasks.Wait(TimeSpan.FromSeconds(3));
            }
            catch (AggregateException ex)
            {
                // 过滤掉正常取消或关闭时抛出的异常，防止程序崩溃
                ex.Handle(e => e is OperationCanceledException || e is TaskCanceledException);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataStorageProcessor] 停止服务时发生异常: {ex.Message}");
            }
            finally
            {
                _cts.Dispose();
            }
        }

        #region 私有核心逻辑

        /// <summary>
        /// 获取或创建指定 T 类型的 Channel 缓冲区
        /// </summary>
        private Channel<T> GetOrCreateChannel<T>() where T : class, new()
        {
            return (Channel<T>)_channels.GetOrAdd(typeof(T), _ => Channel.CreateUnbounded<T>(new UnboundedChannelOptions
            {
                SingleWriter = false, // 支持多线程写入
                SingleReader = true   // 单后台 Task 读取，内部无锁
            }));
        }

        /// <summary>
        /// 核心消费循环：满足 batchSize 或超时 interval 自动批量写库
        /// </summary>
        private async Task StartConsumerLoopAsync<T>(Channel<T> channel, int batchSize, int flushIntervalMs, CancellationToken cancellationToken) where T : class, new()
        {
            var reader = channel.Reader;
            var buffer = new List<T>(batchSize);

            try
            {
                // 当 Channel 还有数据或尚未调用 Complete() 时，持续监听读取
                while (await reader.WaitToReadAsync(cancellationToken))
                {
                    // 1. 尽量一次性读取 batchSize 条数据装入 buffer
                    while (buffer.Count < batchSize && reader.TryRead(out var item))
                    {
                        buffer.Add(item);
                    }

                    // 2. 攒满一批，立即刷盘
                    if (buffer.Count >= batchSize)
                    {
                        FlushToDatabase(buffer);
                        buffer.Clear();
                    }

                    // 3. 攒不满但通道里暂时没新数据了，延时等待（防止低频数据残留在内存中）
                    if (buffer.Count > 0 && reader.Count == 0)
                    {
                        await Task.Delay(flushIntervalMs, cancellationToken);

                        // 延时结束后将残留的数据刷入数据库
                        if (buffer.Count > 0)
                        {
                            FlushToDatabase(buffer);
                            buffer.Clear();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常响应取消信号，不做异常抛出
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataStorageProcessor] 消费循环发生异常: {ex.Message}");
            }
            finally
            {
                // 【核心兜底】：当循环结束（应用关闭或通道 Complete）时，将 buffer 里的剩余数据全部刷盘
                if (buffer.Count > 0)
                {
                    FlushToDatabase(buffer);
                    buffer.Clear();
                }
            }
        }

        /// <summary>
        /// 执行真正的 SqlSugar 批量写入
        /// </summary>
        private void FlushToDatabase<T>(List<T> buffer) where T : class, new()
        {
            try
            {
                _sqlSugarHelper.InsertRange(buffer);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataStorageProcessor] 批量写入 {typeof(T).Name} 失败, 数量:{buffer.Count}, 异常: {ex.Message}");
            }
        }

        #endregion
    }
}