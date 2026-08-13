using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using WpfApp1.StaticClasses.Log;

namespace WpfApp1.Service.Communication
{
    public abstract class SerialPortChannelBase : ICommunicationChannel, IDisposable
    {
        private TaskCompletionSource<string> _currentTcs;
        private SerialPort _serialPort;

        public string MyPortName { get; }
        public int MyBaudRate { get; }
        public Parity MyParity { get; }
        public StopBits MyStopBits { get; }
        public int MyDataBits { get; }
        public string Mdescribl { get; }
        public bool IsConnected { get; set; } = false;

        // 接收缓冲区
        public string receiveBuffer;

        private readonly object _lock = new object();
        private readonly int _readTimeOut;
        private readonly int _writeTimeOut;

        public event EventHandler<string> Received;

        public SerialPortChannelBase(string description, string myportName, int mybaurate,
                                     int readTimeout = 500, int writeTimeOut = 500,
                                     Parity parity = Parity.None, int databit = 8,
                                     StopBits stopBits = StopBits.One
                                    )
        {
            MyPortName = myportName;
            MyBaudRate = mybaurate;
            MyParity = parity;
            MyStopBits = stopBits;
            MyDataBits = databit;
            _readTimeOut = readTimeout;
            _writeTimeOut = writeTimeOut;
            Mdescribl = description;
        }
       
        public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (IsConnected)
                return Task.FromResult(true);

            return Task.Run(() =>
            {
                lock (_lock)
                {
                    if (IsConnected) // 双重检查
                        return true;

                    try
                    {
                        _serialPort = new SerialPort(MyPortName, MyBaudRate, MyParity, MyDataBits, MyStopBits)
                        {
                            WriteTimeout = _writeTimeOut,
                            ReadTimeout = _readTimeOut
                        };
                        _serialPort.ReceivedBytesThreshold = 1;
                        _serialPort.DataReceived += OnDataReceived;
                        _serialPort.Open();
                        IsConnected = true;
                        Logger.Info($"{Mdescribl}串口连接成功");
                        return true;
                    }
                    catch (Exception e)
                    {
                        // 安全解绑事件（如果端口对象已创建）
                        if (_serialPort != null)
                        {
                            _serialPort.DataReceived -= OnDataReceived;
                            try { _serialPort.Close(); } catch { /* 忽略关闭时的异常 */ }
                            _serialPort.Dispose();
                            _serialPort = null;
                        }
                        IsConnected = false;
                        Logger.Error($"{Mdescribl}串口连接失败:{e.Message}");
                        return false;
                    }
                }
            }, cancellationToken);
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen || !IsConnected) return;
            try
            {
                var data = _serialPort.ReadExisting();
                receiveBuffer = data;
                OnReceive(receiveBuffer);

                lock (_lock)
                {
                    _currentTcs?.TrySetResult(receiveBuffer);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"{Mdescribl}串口接收数据失败：{ex.Message}");
                SafeClosePort();
            }
        }

        /// <summary>
        /// 安全关闭串口，释放资源
        /// </summary>
        private void SafeClosePort()
        {
            lock (_lock)
            {
                IsConnected = false;
                if (_serialPort != null)
                {
                    _serialPort.DataReceived -= OnDataReceived;
                    try
                    {
                        if (_serialPort.IsOpen)
                            _serialPort.Close();
                    }
                    catch
                    {
                        // 关闭异常忽略
                    }
                    _serialPort.Dispose();
                    _serialPort = null;
                }
                // 把等待中的任务置为取消
                _currentTcs?.TrySetCanceled();
                _currentTcs = null;
            }
        }

        public Task DisconnectAsync()
        {
            SafeClosePort();
            return Task.CompletedTask;
        }

        public virtual async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            // 原代码条件写反，修复：串口为空 或者未连接直接返回空
            if (_serialPort == null || !IsConnected)
                return string.Empty;

            lock (_lock)
            {
                receiveBuffer = string.Empty;
                // 每次发送新建Tcs，不能复用旧的
                _currentTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            try
            {
                _serialPort.WriteLine(command);

                // 带超时等待，不使用死循环轮询CPU
                using var ctsTimeout = new CancellationTokenSource(_readTimeOut);
                using var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ctsTimeout.Token);

                var waitTask = _currentTcs.Task;
                var completedTask = await Task.WhenAny(waitTask, Task.Delay(-1, combined.Token));

                if (completedTask != waitTask)
                {
                    Logger.Info($"{Mdescribl}读取超时,读取时间为{_readTimeOut}ms");
                    lock (_lock)
                    {
                        _currentTcs.TrySetCanceled();
                    }
                    return string.Empty;
                }

                return await waitTask;
            }
            catch (OperationCanceledException)
            {
                Logger.Warning($"{Mdescribl}发送命令被取消");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.Error($"{Mdescribl}发送命令异常：{ex.Message}");
                SafeClosePort();
                throw;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                SafeClosePort();
                Received = null;
            }
        }

        
        public virtual void OnReceive(string message)
        {
            Received?.Invoke(this, message);
        }
    }
}