using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.Service.Communication.Interfaces
{
    public interface IPlcServers
    {
        /// <summary>
        /// 最后一次操作的错误信息
        /// </summary>
        string LastErrorMessage { get; }

        /// <summary>
        /// PLC连接状态
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 连接PLC
        /// </summary>
        OutCome Connect(string ipAddress, byte rack, byte slot, out string message);

        /// <summary>
        /// 断开连接
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 通用读写方法
        /// </summary>
        OutCome ReadWrite(bool isRead, string area, int dbNumber, string startAddress, ref string value,
            PlcDataType dataType, out string message, int length = 1);
    }
}
