using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WpfApp1.Service.Communication
{
    /// <summary>
    /// 串口通讯接口，定义打开、关闭、发送及事件通知。
    /// </summary>
    public interface ISocketSerialport : IDisposable
    {
        #region Events

        /// <summary>
        /// 收到原始字节数组。
        /// </summary>
        event EventHandler<SocketSerialport.DataReceivedEventArgs> DataReceived;

        /// <summary>
        /// 收到解码后的字符串。
        /// </summary>
        event EventHandler<SocketSerialport.TextReceivedEventArgs> TextReceived;

        /// <summary>
        /// 按行拆分后的字符串。
        /// </summary>
        event EventHandler<SocketSerialport.LineReceivedEventArgs> LineReceived;

        /// <summary>
        /// 串口错误事件。
        /// </summary>
        event EventHandler<SocketSerialport.SerialPortErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// 串口打开/关闭状态变化事件。
        /// </summary>
        event EventHandler<bool> OpenStateChanged;

        #endregion

        #region Properties

        /// <summary>
        /// 串口是否已打开。
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// 串口名称（如 COM1）。
        /// </summary>
        string PortName { get; set; }

        /// <summary>
        /// 波特率。
        /// </summary>
        int BaudRate { get; set; }

        /// <summary>
        /// 文本编码。
        /// </summary>
        Encoding TextEncoding { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// 异步打开串口。
        /// </summary>
        Task OpenAsync(CancellationToken cancellation = default);

        /// <summary>
        /// 异步关闭串口。
        /// </summary>
        Task CloseAsync(CancellationToken cancellation = default);

        /// <summary>
        /// 异步发送字节数组。
        /// </summary>
        Task SendAsync(byte[] buffer, int offset = 0, int count = -1, CancellationToken cancellation = default);

        /// <summary>
        /// 异步发送字符串。
        /// </summary>
        Task SendAsync(string text, CancellationToken cancellation = default);

        /// <summary>
        /// 异步发送一行文本（自动附加换行符）。
        /// </summary>
        Task SendLineAsync(string line, CancellationToken cancellation = default);

        #endregion
    }
}