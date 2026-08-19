using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WpfApp1.Service.Communication.Interfaces;

namespace WpfApp1.Service.Communication
{
    public class TcpSocketServer : ITcpSocketServer
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
        private readonly int _readTimeoutMs;
        private readonly int _writeTimeoutMs;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        // Events
        public event Func<Task>? OnClientConnected;
        public event Func<Task>? OnClientDisconnected;
        public event Func<byte[], Task>? OnReceived;
        public event Action<Exception>? OnError;

        public bool IsRunning => _listenSocket != null;
        public bool HasClient => _clientSocket?.Connected ?? false;

        /// <summary>
        /// 构造 TCP 服务器
        /// </summary>
        /// <param name="listenAddress">监听地址</param>
        /// <param name="port">监听端口</param>
        /// <param name="bufferSize">接收缓冲区大小</param>
        /// <param name="readTimeoutMs">读取超时（毫秒），-1 或 0 表示无限</param>
        /// <param name="writeTimeoutMs">写入超时（毫秒），-1 或 0 表示无限</param>
        public TcpSocketServer(IPAddress listenAddress, int port, int bufferSize = 8192,
                               int readTimeoutMs = -1, int writeTimeoutMs = -1)
        {
            _listenEndpoint = new IPEndPoint(listenAddress, port);
            _bufferSize = bufferSize;
            _readTimeoutMs = readTimeoutMs <= 0 ? -1 : readTimeoutMs;
            _writeTimeoutMs = writeTimeoutMs <= 0 ? -1 : writeTimeoutMs;
        }

        /// <summary>
        /// 启动监听（异步）。支持单客户端连接，断开后自动接受下一个。
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
                    var accepted = await _listenSocket.AcceptAsync().ConfigureAwait(false);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        accepted.Dispose();
                        break;
                    }

                    // 如果已有客户端，则拒绝新连接
                    if (_clientSocket != null && _clientSocket.Connected)
                    {
                        try { accepted.Shutdown(SocketShutdown.Both); } catch { }
                        accepted.Dispose();
                        continue;
                    }

                    _clientSocket = accepted;
                    StartNetworkStreamAndReceiveLoop();

                    if (OnClientConnected != null) await OnClientConnected.Invoke().ConfigureAwait(false);

                    // 等待接收循环结束（客户端断开）
                    if (_receiveTask != null)
                    {
                        try { await _receiveTask.ConfigureAwait(false); } catch { /* 忽略 */ }
                    }

                    // 清理客户端引用
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
                    await Task.Delay(200).ConfigureAwait(false);
                }
            }
        }

        private void StartNetworkStreamAndReceiveLoop()
        {
            if (_clientSocket == null) throw new InvalidOperationException("Client socket is null.");
            _networkStream?.Dispose();
            _networkStream = new NetworkStream(_clientSocket, ownsSocket: true);

            // 设置同步超时（对异步方法不影响，但保留）
            if (_readTimeoutMs > 0) _networkStream.ReadTimeout = _readTimeoutMs;
            if (_writeTimeoutMs > 0) _networkStream.WriteTimeout = _writeTimeoutMs;

            _receiveCts?.Dispose();
            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
        }

        /// <summary>
        /// 向已连接的客户端发送数据，支持写入超时。
        /// </summary>
        public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            if (!HasClient || _networkStream == null) throw new InvalidOperationException("No connected client.");

            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (_writeTimeoutMs > 0) cts.CancelAfter(_writeTimeoutMs);

                await _networkStream.WriteAsync(data, 0, data.Length, cts.Token).ConfigureAwait(false);
                await _networkStream.FlushAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 外部取消，重新抛出
                throw;
            }
            catch (OperationCanceledException) when (_writeTimeoutMs > 0)
            {
                var timeoutEx = new TimeoutException($"Write timed out after {_writeTimeoutMs} ms.");
                OnError?.Invoke(timeoutEx);
                throw timeoutEx;
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>
        /// 接收循环，每次读取应用读取超时。
        /// </summary>
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
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        if (_readTimeoutMs > 0) cts.CancelAfter(_readTimeoutMs);

                        bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // 外部取消，退出循环
                        break;
                    }
                    catch (OperationCanceledException) when (_readTimeoutMs > 0)
                    {
                        // 读取超时，断开客户端
                        var timeoutEx = new TimeoutException($"Read timed out after {_readTimeoutMs} ms.");
                        OnError?.Invoke(timeoutEx);
                        break; // 退出循环，触发清理
                    }

                    if (bytesRead == 0)
                    {
                        // 客户端关闭连接
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
                // 清理当前客户端连接
                try { _receiveCts?.Cancel(); } catch { }
                try { _networkStream?.Dispose(); } catch { }
                _networkStream = null;
                try { _clientSocket?.Dispose(); } catch { }
                _clientSocket = null;

                if (OnClientDisconnected != null)
                {
                    try { await OnClientDisconnected.Invoke().ConfigureAwait(false); } catch { /* 忽略 */ }
                }
            }
        }

        /// <summary>
        /// 停止监听并断开当前客户端（异步）。
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                _acceptCts?.Cancel();
                if (_acceptTask != null) await _acceptTask.ConfigureAwait(false);
            }
            catch { }

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