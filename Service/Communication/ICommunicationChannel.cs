
namespace WpfApp1.Service.Communication
{
    public interface ICommunicationChannel : IDisposable
    {
        event EventHandler<string> Received;
        bool IsConnected { get; }
        // 连接与断开
        Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

        Task DisconnectAsync();

        // 发送命令并返回原始响应字符串
        Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default);

       
    }
}
