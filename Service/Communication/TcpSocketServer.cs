using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WpfApp1.Service.Communication
{
    public class TcpSocketServer : IDisposable
    {
        private readonly IPEndPoint _listenEndpoint;
        private Socket? _listenSocket;
        private Socket? _clientSocket;
        private NetworkStream? _networkStream;
        private CancellationTokenSource? _acceptCts;
        private CancellationTokenSource? _receiveCts;
        private Task? _acceptTask;
        private Task? _receiveTask;
        private readonly int _bufferSize;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        // Events
        public event Func<Task>? OnClientConnected;
        public event Func<Task>? OnClientDisconnected;
        public event Func<byte[], Task>? OnReceived;
        public event Action<Exception>? OnError;

        public bool IsRunning => _listenSocket != null;
        public bool HasClient => _clientSocket?.Connected ?? false;

        public TcpSocketServer(IPAddress listenAddress, int port, int bufferSize = 8192)
        {
            _listenEndpoint = new IPEndPoint(listenAddress, port);
            _bufferSize = bufferSize;
        }

        /// <summary>
        /// 启动监听（异步）。在没有客户端连接时会等待 Accept；一旦有客户端连接，会处理该客户端直到断开，再回到 Accept（实现一对一：同时只服务一个客户端）。
        /// </summary>
        public Task StartAsync(int backlog = 100)
        {
            if (IsRunning) throw new InvalidOperationException("Server already running.");

            _listenSocket = new Socket(_listenEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            _listenSocket.Bind(_listenEndpoint);
            _listenSocket.Listen(backlog);

            _acceptCts = new CancellationTokenSource();
            _acceptTask = Task.Run(() => AcceptLoopAsync(_acceptCts.Token), CancellationToken.None);
            return Task.CompletedTask;
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listenSocket != null)
            {
                try
                {
                    // Wait for a client
                    var accepted = await _listenSocket.AcceptAsync().ConfigureAwait(false);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        accepted.Dispose();
                        break;
                    }

                    // If we already had a client, close the new one (enforce one-client-at-a-time)
                    if (_clientSocket != null && _clientSocket.Connected)
                    {
                        try { accepted.Shutdown(SocketShutdown.Both); } catch { }
                        accepted.Dispose();
                        continue;
                    }

                    _clientSocket = accepted;
                    StartNetworkStreamAndReceiveLoop();

                    if (OnClientConnected != null) await OnClientConnected.Invoke().ConfigureAwait(false);

                    // Wait until the receive loop ends (client disconnects) before accepting next client.
                    if (_receiveTask != null)
                    {
                        try { await _receiveTask.ConfigureAwait(false); } catch { /* swallow; handled in receive loop */ }
                    }

                    // Clean up client references (receive loop does some cleanup, but ensure)
                    try { _networkStream?.Dispose(); } catch { }
                    _networkStream = null;
                    try { _clientSocket?.Dispose(); } catch { }
                    _clientSocket = null;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(ex);
                    // brief delay to avoid tight error loop
                    await Task.Delay(200).ConfigureAwait(false);
                }
            }
        }

        private void StartNetworkStreamAndReceiveLoop()
        {
            if (_clientSocket == null) throw new InvalidOperationException("Client socket is null.");
            _networkStream?.Dispose();
            _networkStream = new NetworkStream(_clientSocket, ownsSocket: true);
            _receiveCts?.Dispose();
            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
        }

        /// <summary>
        /// 向已连接的客户端发送字节（异步）。如果没有客户端会抛 InvalidOperationException。
        /// </summary>
        public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            if (!HasClient || _networkStream == null) throw new InvalidOperationException("No connected client.");
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _networkStream.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
                await _networkStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[_bufferSize];
            try
            {
                while (!cancellationToken.IsCancellationRequested && HasClient && _networkStream != null)
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (bytesRead == 0)
                    {
                        // 客户端已关闭连接
                        break;
                    }

                    var copy = new byte[bytesRead];
                    Array.Copy(buffer, 0, copy, 0, bytesRead);
                    if (OnReceived != null)
                    {
                        try
                        {
                            await OnReceived.Invoke(copy).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            OnError?.Invoke(ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
            finally
            {
                // Clean up client connection
                try { _receiveCts?.Cancel(); } catch { }
                try { _networkStream?.Dispose(); } catch { }
                _networkStream = null;
                try { _clientSocket?.Dispose(); } catch { }
                _clientSocket = null;

                if (OnClientDisconnected != null)
                {
                    try { await OnClientDisconnected.Invoke().ConfigureAwait(false); } catch { /* ignore */ }
                }
            }
        }

        /// <summary>
        /// 停止监听并断开当前客户端（异步）。
        /// </summary>
        public async Task StopAsync()
        {
            // Stop accepting new clients
            try
            {
                _acceptCts?.Cancel();
                if (_acceptTask != null) await _acceptTask.ConfigureAwait(false);
            }
            catch { }

            // Stop receiving from client
            try
            {
                _receiveCts?.Cancel();
                if (_receiveTask != null) await _receiveTask.ConfigureAwait(false);
            }
            catch { }

            try { _networkStream?.Dispose(); } catch { }
            _networkStream = null;

            try
            {
                if (_clientSocket != null)
                {
                    try { _clientSocket.Shutdown(SocketShutdown.Both); } catch { }
                    _clientSocket.Dispose();
                }
            }
            catch { }
            _clientSocket = null;

            try
            {
                if (_listenSocket != null)
                {
                    _listenSocket.Close();
                    _listenSocket.Dispose();
                }
            }
            catch { }
            _listenSocket = null;
        }

        public void Dispose()
        {
            try { _acceptCts?.Cancel(); } catch { }
            try { _receiveCts?.Cancel(); } catch { }
            try { _networkStream?.Dispose(); } catch { }
            try { _clientSocket?.Dispose(); } catch { }
            try { _listenSocket?.Dispose(); } catch { }
            _sendLock.Dispose();
        }
    }
}