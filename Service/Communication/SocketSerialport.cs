using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WpfApp1.Service.Communication
{
    /// <summary>
    /// 串口通讯封装，自动在创建时捕获 SynchronizationContext（UI 线程）。
    /// 支持异步打开/关闭/发送，字节/文本/按行回调，自定义错误事件参数。
    /// </summary>
    public class SocketSerialport : ISocketSerialport
    {
        private readonly SerialPort _port;
        private readonly SynchronizationContext _syncContext;
        private readonly object _syncLock = new object();
        private readonly StringBuilder _lineBuffer = new StringBuilder();
        private bool _disposed;

        /// <summary>
        /// 构造串口客户端
        /// </summary>
        /// <param name="portName">串口号</param>
        /// <param name="baudRate">波特率</param>
        /// <param name="parity">校验位</param>
        /// <param name="dataBits">数据位</param>
        /// <param name="stopBits">停止位</param>
        /// <param name="encoding">文本编码（默认 UTF8）</param>
        /// <param name="newLine">换行符（默认 \r\n）</param>
        /// <param name="readTimeoutMs">读取超时毫秒（-1 或 0 表示无限，默认无限）</param>
        /// <param name="writeTimeoutMs">写入超时毫秒（-1 或 0 表示无限，默认 2000）</param>
        public SocketSerialport(string portName = "COM1",
                                int baudRate = 9600,
                                Parity parity = Parity.None,
                                int dataBits = 8,
                                StopBits stopBits = StopBits.One,
                                Encoding encoding = null,
                                string newLine = "\r\n",
                                int readTimeoutMs = -1,
                                int writeTimeoutMs = 2000)     // ← 新增超时参数
        {
            _syncContext = SynchronizationContext.Current;
            _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
            {
                Encoding = encoding ?? Encoding.UTF8,
                NewLine = newLine ?? "\r\n",
                // 应用超时设置（-1 或 0 转为 InfiniteTimeout）
                ReadTimeout = readTimeoutMs <= 0 ? SerialPort.InfiniteTimeout : readTimeoutMs,
                WriteTimeout = writeTimeoutMs <= 0 ? SerialPort.InfiniteTimeout : writeTimeoutMs
            };

            _port.DataReceived += Port_DataReceived;
            _port.ErrorReceived += Port_ErrorReceived;
        }

        #region Events & EventArgs
        //收到原始字节数组
        public event EventHandler<DataReceivedEventArgs> DataReceived;
        //收到解码后的字符串
        public event EventHandler<TextReceivedEventArgs> TextReceived;
        //按行拆分后的字符串
        public event EventHandler<LineReceivedEventArgs> LineReceived;
        public event EventHandler<SerialPortErrorEventArgs> ErrorOccurred;
        public event EventHandler<bool> OpenStateChanged;

        // 字节类型
        public class DataReceivedEventArgs : EventArgs
        {
            public byte[] Data { get; }
            public DataReceivedEventArgs(byte[] data) => Data = data;
        }

        // 原始文本类型
        public class TextReceivedEventArgs : EventArgs
        {
            public string Text { get; }
            public TextReceivedEventArgs(string text) => Text = text;
        }

        // 按行类型
        public class LineReceivedEventArgs : EventArgs
        {
            public string Line { get; }
            public LineReceivedEventArgs(string line) => Line = line;
        }

        // 自定义错误事件参数（避免依赖 SerialErrorReceivedEventArgs 的构造）
        public class SerialPortErrorEventArgs : EventArgs
        {
            public SerialError Error { get; }
            public Exception Exception { get; }

            public SerialPortErrorEventArgs(SerialError error, Exception exception = null)
            {
                Error = error;
                Exception = exception;
            }
        }

        #endregion

        #region Properties

        public bool IsOpen => !_disposed && _port?.IsOpen == true;

        public string PortName
        {
            get => _port.PortName;
            set
            {
                if (IsOpen) throw new InvalidOperationException("不能在打开时修改 PortName");
                _port.PortName = value;
            }
        }

        public int BaudRate
        {
            get => _port.BaudRate;
            set
            {
                if (IsOpen) throw new InvalidOperationException("不能在打开时修改 BaudRate");
                _port.BaudRate = value;
            }
        }

        public Encoding TextEncoding
        {
            get => _port.Encoding;
            set => _port.Encoding = value ?? Encoding.UTF8;
        }

        #endregion

        #region Open / Close

        public Task OpenAsync(CancellationToken cancellation = default)
        {
            ThrowIfDisposed();
            return Task.Run(() =>
            {
                cancellation.ThrowIfCancellationRequested();
                lock (_syncLock)
                {
                    if (!_port.IsOpen)
                    {
                        _port.Open();
                        PostToSync(() => OpenStateChanged?.Invoke(this, true));
                    }
                }
            }, cancellation);
        }

        public Task CloseAsync(CancellationToken cancellation = default)
        {
            ThrowIfDisposed();
            return Task.Run(() =>
            {
                cancellation.ThrowIfCancellationRequested();
                lock (_syncLock)
                {
                    if (_port.IsOpen)
                    {
                        _port.Close();
                        PostToSync(() => OpenStateChanged?.Invoke(this, false));
                    }
                }
            }, cancellation);
        }

        #endregion

        #region Send

        public Task SendAsync(byte[] buffer, int offset = 0, int count = -1, CancellationToken cancellation = default)
        {
            ThrowIfDisposed();
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (count == -1) count = buffer.Length - offset;
            return Task.Run(() =>
            {
                cancellation.ThrowIfCancellationRequested();
                if (!IsOpen) throw new InvalidOperationException("串口未打开");
                lock (_syncLock)
                {
                    _port.Write(buffer, offset, count);   // 写入超时由 SerialPort.WriteTimeout 控制
                }
            }, cancellation);
        }

        public Task SendAsync(string text, CancellationToken cancellation = default)
        {
            ThrowIfDisposed();
            if (text == null) throw new ArgumentNullException(nameof(text));
            return Task.Run(() =>
            {
                cancellation.ThrowIfCancellationRequested();
                if (!IsOpen) throw new InvalidOperationException("串口未打开");
                lock (_syncLock)
                {
                    _port.Write(text);   // 写入超时由 SerialPort.WriteTimeout 控制
                }
            }, cancellation);
        }

        public Task SendLineAsync(string line, CancellationToken cancellation = default)
        {
            ThrowIfDisposed();
            if (line == null) throw new ArgumentNullException(nameof(line));
            return Task.Run(() =>
            {
                cancellation.ThrowIfCancellationRequested();
                if (!IsOpen) throw new InvalidOperationException("串口未打开");
                lock (_syncLock)
                {
                    _port.WriteLine(line);   // 写入超时由 SerialPort.WriteTimeout 控制
                }
            }, cancellation);
        }

        #endregion

        #region Handlers

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                int bytes = _port.BytesToRead;
                if (bytes <= 0) return;

                byte[] buffer = new byte[bytes];
                int read = _port.Read(buffer, 0, bytes);   // 读取超时由 SerialPort.ReadTimeout 控制

                // 字节回调
                var dataCopy = new byte[read];
                Array.Copy(buffer, dataCopy, read);
                PostToSync(() => DataReceived?.Invoke(this, new DataReceivedEventArgs(dataCopy)));

                // 文本回调
                string text = _port.Encoding.GetString(buffer, 0, read);
                PostToSync(() => TextReceived?.Invoke(this, new TextReceivedEventArgs(text)));

                // 按行解析并触发 LineReceived
                if (!string.IsNullOrEmpty(text))
                {
                    lock (_lineBuffer)
                    {
                        _lineBuffer.Append(text);
                        string nl = _port.NewLine ?? "\r\n";
                        string content = _lineBuffer.ToString();
                        int idx;
                        while ((idx = content.IndexOf(nl, StringComparison.Ordinal)) >= 0)
                        {
                            string line = content.Substring(0, idx);
                            content = content.Substring(idx + nl.Length);
                            PostToSync(() => LineReceived?.Invoke(this, new LineReceivedEventArgs(line)));
                        }
                        _lineBuffer.Clear();
                        _lineBuffer.Append(content);
                    }
                }
            }
            catch (Exception ex)
            {
                // 将异常以 ErrorOccurred 通知（携带 Exception）
                PostToSync(() => ErrorOccurred?.Invoke(this, new SerialPortErrorEventArgs(SerialError.RXOver, ex)));
            }
        }

        private void Port_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            // 将 SerialErrorReceivedEventArgs 转换为自定义事件参数再通知
            PostToSync(() => ErrorOccurred?.Invoke(this, new SerialPortErrorEventArgs(e.EventType)));
        }

        #endregion

        #region Helpers

        private void PostToSync(Action action)
        {
            if (action == null) return;
            if (_syncContext != null)
            {
                _syncContext.Post(_ => action(), null);
            }
            else
            {
                // 若未在 UI 线程创建，则在线程池回调（订阅者需注意线程）
                ThreadPool.QueueUserWorkItem(_ => action());
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SocketSerialport));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _port.DataReceived -= Port_DataReceived;
                _port.ErrorReceived -= Port_ErrorReceived;

                lock (_syncLock)
                {
                    if (_port.IsOpen)
                    {
                        try { _port.Close(); } catch { /* ignore */ }
                    }
                    _port.Dispose();
                }
            }
            finally
            {
                PostToSync(() => OpenStateChanged?.Invoke(this, false));
            }
        }

        #endregion
    }
}