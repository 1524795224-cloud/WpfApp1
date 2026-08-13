using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
namespace WpfApp1.Service.Communication
{
public class TcpSocketClient : IDisposable
{
private Socket? _socket;
private NetworkStream? _networkStream;
private readonly int _bufferSize;
private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
private CancellationTokenSource? _receiveCts;
private Task? _receiveTask;

public bool Connected => _socket?.Connected ?? false;

    // Events
    public event Func<Task>? OnConnected;
        public event Func<Task>? OnDisconnected;
            public event Func<byte[], Task>? OnReceived;
                public event Action<Exception>? OnError;

                    // Auto reconnect settings
                    public bool AutoReconnect { get; set; } = false;
                    public int ReconnectDelayMs { get; set; } = 2000;
                    public int MaxReconnectAttempts { get; set; } = 5;

                    public TcpSocketClient(int bufferSize = 8192)
                    {
                    _bufferSize = bufferSize;
                    }

                    /// <summary>
                        /// 异步连接到远端主机。
                    /// </summary>
                    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
                    {
                    var ipAddresses = await Dns.GetHostAddressesAsync(host);
                    Exception? lastEx = null;

                    for (int i = 0; i < ipAddresses.Length; i++)
                      {
                      cancellationToken.ThrowIfCancellationRequested();
                      var ip = ipAddresses[i];

                      // 创建 socket
                      _socket?.Dispose();
                      _socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                      {
                      NoDelay = true
                      };

                      try
                      {
                      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                      var connectTask = _socket.ConnectAsync(ip, port);
                      var completed = await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, linkedCts.Token));
                      // If connectTask completed successfully, just await it to propagate exceptions
                      await connectTask;
                      // connected
                      StartNetworkStreamAndReceiveLoop();
                      if (OnConnected != null) await OnConnected.Invoke();
                      return;
                      }
                      catch (Exception ex)
                      {
                      lastEx = ex;
                      _socket?.Dispose();
                      _socket = null;
                      // try next address
                      }
                      }

                      throw lastEx ?? new SocketException((int)SocketError.NotConnected);
                      }

                      private void StartNetworkStreamAndReceiveLoop()
                      {
                      if (_socket == null) throw new InvalidOperationException("Socket is null when starting receive loop.");
                      _networkStream?.Dispose();
                      _networkStream = new NetworkStream(_socket, ownsSocket: true);
                      _receiveCts?.Dispose();
                      _receiveCts = new CancellationTokenSource();
                      _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
                        }

                        /// <summary>
                            /// 发送数据（异步）。保持发送顺序。
                        /// </summary>
                        public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
                        {
                        if (!Connected || _networkStream == null) throw new InvalidOperationException("Not connected.");
                        await _sendLock.WaitAsync(cancellationToken);
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

                        /// <summary>
                            /// 接收循环：读取流并触发 OnReceived 事件。按原样提供流字节，调用者负责协议解析（例如长度前缀）。
                        /// </summary>
                        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
                        {
                        var buffer = new byte[_bufferSize];
                        try
                        {
                        while (!cancellationToken.IsCancellationRequested && Connected && _networkStream != null)
                        {
                        int bytesRead = 0;
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
                        // Remote closed
                        await HandleDisconnectAsync();
                        if (AutoReconnect)
                        {
                        await TryAutoReconnectLoop(cancellationToken);
                        }
                        break;
                        }

                        // copy the actual bytes and invoke handler
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
                        await HandleDisconnectAsync();
                        if (AutoReconnect)
                        {
                        try
                        {
                        await TryAutoReconnectLoop(cancellationToken);
                        }
                        catch { /* swallow here; TryAutoReconnectLoop will raise events or exceptions if wanted */ }
                        }
                        }
                        }

                        private async Task TryAutoReconnectLoop(CancellationToken externalCancellation)
                        {
                        int attempts = 0;
                        var originalEndpoint = _socket?.RemoteEndPoint as IPEndPoint;
                        if (originalEndpoint == null) return;

                        Exception? lastEx = null;
                        while (AutoReconnect && (MaxReconnectAttempts <= 0 || attempts < MaxReconnectAttempts) && !externalCancellation.IsCancellationRequested)
                          {
                          attempts++;
                          try
                          {
                          await Task.Delay(ReconnectDelayMs, externalCancellation).ConfigureAwait(false);
                          await ConnectAsync(originalEndpoint.Address.ToString(), originalEndpoint.Port, externalCancellation).ConfigureAwait(false);
                          // Connected successfully; exit
                          return;
                          }
                          catch (Exception ex)
                          {
                          lastEx = ex;
                          OnError?.Invoke(ex);
                          }
                          }

                          // if we get here, reconnection failed
                          if (lastEx != null) OnError?.Invoke(lastEx);
                          }

                          private async Task HandleDisconnectAsync()
                          {
                          try
                          {
                          _receiveCts?.Cancel();
                          _networkStream?.Dispose();
                          _networkStream = null;
                          _socket?.Dispose();
                          _socket = null;
                          }
                          catch { /* ignore */ }

                          if (OnDisconnected != null) await OnDisconnected.Invoke().ConfigureAwait(false);
                          }

                        /// <summary>
                            /// 主动断开连接并停止接收。
                        /// </summary>
                        public async Task DisconnectAsync()
                        {
                        try
                        {
                        _receiveCts?.Cancel();
                        if (_networkStream != null)
                        {
                        await _networkStream.FlushAsync().ConfigureAwait(false);
                        }
                        }
                        catch { }

                        try
                        {
                        _networkStream?.Dispose();
                        _networkStream = null;
                        _socket?.Shutdown(SocketShutdown.Both);
                        }
                        catch { }
                        finally
                        {
                        _socket?.Dispose();
                        _socket = null;
                        }

                        if (OnDisconnected != null) await OnDisconnected.Invoke().ConfigureAwait(false);
                        }

                        public void Dispose()
                        {
                        try { _receiveCts?.Cancel(); } catch { }
                        try { _networkStream?.Dispose(); } catch { }
                        try { _socket?.Dispose(); } catch { }
                        _sendLock.Dispose();
                        }
                        }
                        }