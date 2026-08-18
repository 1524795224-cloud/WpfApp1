using HslCommunication;
using HslCommunication.Profinet.Siemens;
using WpfApp1.Service.Communication.Interfaces;


namespace WpfApp1.Service.Communication
{
    /// <summary>
    /// 数据类型枚举
    /// </summary>
    public enum PlcDataType
    {
        Bool = 0,
        Int16 = 1,      // 16位有符号整数
        Int32 = 2,      // 32位有符号整数
        UInt16 = 3,     // 16位无符号整数
        UInt32 = 4,     // 32位无符号整数
        Float = 5,      // 32位浮点数
        String = 6,     // 字符串
        ByteArray = 7   // 字节数组（二维码/二进制数据）
    }

    /// <summary>
    /// 西门子PLC通信类（线程安全），支持S7-1200和S7-1500
    /// </summary>
    public class Semens:IPlcServers
    {
        private SiemensS7Net plc;
        private readonly object _commLock = new object();
        private bool isConnected = false;

        /// <summary>
        /// 最后一次操作的错误信息
        /// </summary>
        public string LastErrorMessage { get; private set; } = string.Empty;

        /// <summary>
        /// 构造函数，指定PLC型号
        /// </summary>
        /// <param name="plcType">S1200 或 S1500</param>
        public Semens(SiemensPLCS plcType)
        {
            plc = new SiemensS7Net(plcType);
        }

        /// <summary>
        /// 连接PLC（线程安全）
        /// </summary>
        /// <param name="ipAddress">IP地址</param>
        /// <param name="rack">机架号，通常1200/1500为0</param>
        /// <param name="slot">槽号，1200通常1，1500通常1或0</param>
        /// <param name="message">连接结果描述</param>
        /// <returns>操作结果</returns>
        public OutCome Connect(string ipAddress, byte rack, byte slot, out string message)
        {
            message = string.Empty;
            lock (_commLock)
            {
                try
                {
                    plc.IpAddress = ipAddress;
                    plc.Rack = rack;
                    plc.Slot = slot;
                    OperateResult res = plc.ConnectServer();
                    isConnected = res.IsSuccess;
                    if (res.IsSuccess)
                    {
                        message = "连接成功";
                        LastErrorMessage = "";
                        return OutCome.Success;
                    }
                    else
                    {
                        message = res.Message;
                        LastErrorMessage = res.Message;
                        return OutCome.Fail;
                    }
                }
                catch (Exception ex)
                {
                    message = ex.Message;
                    LastErrorMessage = ex.Message;
                    return OutCome.Fail;
                }
            }
        }

        /// <summary>
        /// 断开连接（线程安全）
        /// </summary>
        public void Disconnect()
        {
            lock (_commLock)
            {
                plc?.ConnectClose();
                isConnected = false;
            }
        }

        /// <summary>
        /// 连接状态
        /// </summary>
        public bool IsConnected
        {
            get
            {
                lock (_commLock)
                {
                    return isConnected;
                }
            }
        }

        /// <summary>
        /// 通用读写方法（线程安全，地址自动适配）
        /// </summary>
        /// <param name="isRead">true:读，false:写</param>
        /// <param name="area">区域：DB, M, I, Q 等</param>
        /// <param name="dbNumber">DB块号，非DB区域忽略</param>
        /// <param name="startAddress">
        /// 起始地址：
        /// - Bool类型需以位格式传入，如 "0.0"、"2.3"
        /// - 非Bool类型传数字偏移量，如 "0"、"10"（内部自动拼接 DBW/DBD/DBB 前缀）
        /// </param>
        /// <param name="value">读写值，读时输出，写时输入</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="message">操作结果描述</param>
        /// <param name="length">数组长度（字符串/字节数组有效，默认1）</param>
        /// <returns>操作结果</returns>
        public OutCome ReadWrite(bool isRead, string area, int dbNumber, string startAddress, ref string value,
            PlcDataType dataType, out string message, int length = 1)
        {
            message = string.Empty;

            bool connected;
            lock (_commLock) { connected = isConnected; }
            if (!connected)
            {
                message = "PLC未连接";
                LastErrorMessage = message;
                return OutCome.Fail;
            }

            lock (_commLock)
            {
                try
                {
                    // 核心：根据数据类型构建正确的地址格式
                    string address = BuildAddress(area, dbNumber, startAddress, dataType);
                    return isRead
                        ? ReadValue(address, dataType, length, ref value, out message)
                        : WriteValue(address, dataType, value, length, out message);
                }
                catch (Exception ex)
                {
                    message = ex.Message;
                    LastErrorMessage = message;
                    return OutCome.Fail;
                }
            }
        }



        /// <summary>
        /// 构建Hsl通信所需的完整地址（自动添加DBX/DBW/DBD/DBB前缀）
        /// </summary>
        private string BuildAddress(string area, int dbNumber, string startAddress, PlcDataType dataType)
        {
            string areaUpper = area.ToUpper().Trim();
            bool isBit = (dataType == PlcDataType.Bool);

            if (areaUpper == "DB")
            {
                if (isBit)
                {
                    // Bool类型：DB1.DBX0.0
                    return $"DB{dbNumber}.DBX{startAddress}";
                }
                else
                {
                    // 非Bool类型：根据数据宽度选择前缀
                    string prefix = dataType switch
                    {
                        PlcDataType.Int16 or PlcDataType.UInt16 => "DBW",
                        PlcDataType.Int32 or PlcDataType.UInt32 or PlcDataType.Float => "DBD",
                        PlcDataType.String or PlcDataType.ByteArray => "DBB",
                        _ => "" // 理论上不会执行
                    };
                    return $"DB{dbNumber}.{prefix}{startAddress}";
                }
            }
            else
            {
                // M/I/Q等区域：Bool -> M0.0，非Bool -> M0
                // 库会自动处理位/字/双字，不需要额外前缀
                return $"{areaUpper}{startAddress}";
            }
        }

        /// <summary>
        /// 执行读操作（在锁内调用）
        /// </summary>
        private OutCome ReadValue(string address, PlcDataType dataType, int length, ref string value, out string message)
        {
            message = string.Empty;
            try
            {
                switch (dataType)
                {
                    case PlcDataType.Bool:
                        var bRes = plc.ReadBool(address);
                        if (bRes.IsSuccess)
                        {
                            value = bRes.Content.ToString();
                            LastErrorMessage = "";
                            return OutCome.Success;
                        }
                        message = bRes.Message;
                        break;

                    case PlcDataType.Int16:
                        var i16Res = plc.ReadInt16(address);
                        if (i16Res.IsSuccess)
                        {
                            value = i16Res.Content.ToString();
                            LastErrorMessage = "";
                            return OutCome.Success;
                        }
                        message = i16Res.Message;
                        break;

                    case PlcDataType.Int32:
                        var i32Res = plc.ReadInt32(address);
                        if (i32Res.IsSuccess)
                        {
                            value = i32Res.Content.ToString();
                            LastErrorMessage = "";
                            return OutCome.Success;
                        }
                        message = i32Res.Message;
                        break;

                    case PlcDataType.UInt16:
                        var ui16Res = plc.ReadUInt16(address);
                        if (ui16Res.IsSuccess)
                        {
                            value = ui16Res.Content.ToString();
                            LastErrorMessage = "";
                            return OutCome.Success;
                        }
                        message = ui16Res.Message;
                        break;

                    case PlcDataType.UInt32:
                        var ui32Res = plc.ReadUInt32(address);
                        if (ui32Res.IsSuccess)
                        {
                            value = ui32Res.Content.ToString();
                            LastErrorMessage = "";
                            return OutCome.Success;
                        }
                        message = ui32Res.Message;
                        break;

                    case PlcDataType.Float:
                        var fRes = plc.ReadFloat(address);
                        if (fRes.IsSuccess)
                        {
                            value = fRes.Content.ToString();
                            LastErrorMessage = "";
                            return OutCome.Success;
                        }
                        message = fRes.Message;
                        break;

                    case PlcDataType.String:
                        var sRes = plc.ReadString(address, (ushort)length);
                        if (sRes.IsSuccess)
                        {
                            value = sRes.Content;
                            LastErrorMessage = "";
                            return OutCome.Success;
                        }
                        message = sRes.Message;
                        break;

                    case PlcDataType.ByteArray:
                        var byteRes = plc.Read(address, (ushort)length);
                        if (byteRes.IsSuccess)
                        {
                            value = Convert.ToBase64String(byteRes.Content);
                            LastErrorMessage = "";
                            return OutCome.Success;
                        }
                        message = byteRes.Message;
                        break;

                    default:
                        message = "不支持的数据类型";
                        break;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }

            LastErrorMessage = message;
            return OutCome.Fail;
        }

        /// <summary>
        /// 执行写操作（在锁内调用）
        /// </summary>
        private OutCome WriteValue(string address, PlcDataType dataType, string value, int length, out string message)
        {
            message = string.Empty;
            try
            {
                OperateResult result;
                switch (dataType)
                {
                    case PlcDataType.Bool:
                        if (!bool.TryParse(value, out bool bVal))
                        {
                            message = "写入值无法转换为bool";
                            break;
                        }
                        result = plc.Write(address, bVal);
                        if (result.IsSuccess)
                        {
                            LastErrorMessage = "";
                            return OutCome.Success;
                        }
                        message = result.Message;
                        break;

                    case PlcDataType.Int16:
                        if (!short.TryParse(value, out short sVal))
                        {
                            message = "写入值无法转换为Int16";
                            break;
                        }
                        result = plc.Write(address, sVal);
                        if (result.IsSuccess)
                        {
                            LastErrorMessage = "";
                            return OutCome.Success;
                        }
                        message = result.Message;
                        break;

                    case PlcDataType.Int32:
                        if (!int.TryParse(value, out int iVal))
                        {
                            message = "写入值无法转换为Int32";
                            break;
                        }
                        result = plc.Write(address, iVal);
                        if (result.IsSuccess)
                        {
                            LastErrorMessage = "";
                            return OutCome.Success;
                        }
                        message = result.Message;
                        break;

                    case PlcDataType.UInt16:
                        if (!ushort.TryParse(value, out ushort usVal))
                        {
                            message = "写入值无法转换为UInt16";
                            break;
                        }
                        result = plc.Write(address, usVal);
                        if (result.IsSuccess)
                        {
                            LastErrorMessage = "";
                            return OutCome.Success;
                        }
                        message = result.Message;
                        break;

                    case PlcDataType.UInt32:
                        if (!uint.TryParse(value, out uint uiVal))
                        {
                            message = "写入值无法转换为UInt32";
                            break;
                        }
                        result = plc.Write(address, uiVal);
                        if (result.IsSuccess)
                        {
                            LastErrorMessage = "";
                            return OutCome.Success;
                        }
                        message = result.Message;
                        break;

                    case PlcDataType.Float:
                        if (!float.TryParse(value, out float fVal))
                        {
                            message = "写入值无法转换为Float";
                            break;
                        }
                        result = plc.Write(address, fVal);
                        if (result.IsSuccess)
                        {
                            LastErrorMessage = "";
                            return OutCome.Success;
                        }
                        message = result.Message;
                        break;

                    case PlcDataType.String:
                        result = plc.Write(address, value);
                        if (result.IsSuccess)
                        {
                            LastErrorMessage = "";
                            return OutCome.Success;
                        }
                        message = result.Message;
                        break;

                    case PlcDataType.ByteArray:
                        byte[] bytes;
                        try
                        {
                            bytes = Convert.FromBase64String(value);
                        }
                        catch
                        {
                            message = "写入值无法从Base64转换为字节数组";
                            break;
                        }
                        result = plc.Write(address, bytes);
                        if (result.IsSuccess)
                        {
                            LastErrorMessage = "";
                            return OutCome.Success;
                        }
                        message = result.Message;
                        break;

                    default:
                        message = "不支持的数据类型";
                        break;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }

            LastErrorMessage = message;
            return OutCome.Fail;
        }
    }
}
