using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.Service.Communication
{
    public class HightTcpClient : TcpSocketClient
    {
        /// <summary>
        /// 构造高度专用 TCP 客户端，可自定义缓冲区大小和超时
        /// </summary>
        public HightTcpClient(int bufferSize = 8192, int readTimeoutMs = -1, int writeTimeoutMs = -1)
            : base(bufferSize, readTimeoutMs, writeTimeoutMs)
        {
            // 可以在这里添加 HightTcpClient 特有的初始化逻辑
            // 例如：默认开启自动重连等
            AutoReconnect = true;
        }
       
    }
}