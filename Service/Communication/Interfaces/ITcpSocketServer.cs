namespace WpfApp1.Service.Communication.Interfaces
{
    /// <summary>
    /// TCP 服务器接口，定义启动监听、发送数据、停止服务及事件通知。
    /// </summary>
    public interface ITcpSocketServer : IDisposable
    {
        /// <summary>
        /// 服务器是否正在监听。
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 是否有已连接的客户端。
        /// </summary>
        bool HasClient { get; }

        /// <summary>
        /// 客户端连接成功事件。
        /// </summary>
        event Func<Task>? OnClientConnected;

        /// <summary>
        /// 客户端断开连接事件。
        /// </summary>
        event Func<Task>? OnClientDisconnected;

        /// <summary>
        /// 数据接收事件（参数为原始字节数据）。
        /// </summary>
        event Func<byte[], Task>? OnReceived;

        /// <summary>
        /// 错误通知事件。
        /// </summary>
        event Action<Exception>? OnError;

        /// <summary>
        /// 启动监听。
        /// </summary>
        Task StartAsync(int backlog = 100);

        /// <summary>
        /// 向当前连接的客户端发送数据。
        /// </summary>
        Task SendAsync(byte[] data, CancellationToken cancellationToken = default);

        /// <summary>
        /// 停止监听并断开当前客户端。
        /// </summary>
        Task StopAsync();
    }
}