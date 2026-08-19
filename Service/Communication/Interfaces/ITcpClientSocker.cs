using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.Service.Communication.Interfaces
{
    public interface ITcpClientSocker: IDisposable
    {
        bool Connected { get; }
        bool AutoReconnect { get; set; }
        int ReconnectDelayMs { get; set; }
        int MaxReconnectAttempts { get; set; }

        event Func<Task>? OnConnected;
        event Func<Task>? OnDisconnected;
        event Func<byte[], Task>? OnReceived;
        event Action<Exception>? OnError;

        Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);
        Task SendAsync(byte[] data, CancellationToken cancellationToken = default);
        Task DisconnectAsync();
       
    }
}
