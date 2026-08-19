using HslCommunication;
using HslCommunication.Profinet.Siemens;
using System;
using System.Threading;
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
    /// 西门子PLC通信类（线程安全），支持S7-1200和S7-1500，带后台轮询与自动重连
    /// </summary>
    public class Semens : IPlcServers, IDisposable
    {
        private SiemensS7Net plc;
        private readonly object _commLock = new object();
        private bool isConnected = false;

        // 轮询与重连定时器
        private Timer? _pollingTimer;
        private Timer? _reconnectTimer;

        // 配置参数
        private readonly int _pollingIntervalMs;
        private readonly int _reconnectIntervalMs;
        private string _ipAddress = string.Empty;
        private byte _rack;
        private byte _slot;
        private bool _isDisposed;

        /// <summary>
        /// 心跳/检测地址，用于轮询测试连接状态（默认 M0.0，可更改）
        /// </summary>
        public string HeartbeatAddress { get; set; } = "M0.0";

        /// <summary>
        /// 连接状态改变事件
        /// </summary>
        public event Action<bool>? ConnectionStatusChanged;

        /// <summary>
        /// 最后一次操作的错误信息
        /// </summary>
        public string LastErrorMessage { get; private set; } = string.Empty;

        /// <summary>
        /// 构造函数，指定PLC型号、轮询间隔与重连间隔
        /// </summary>
        /// <param name="plcType">S1200 或 S1500</param>
        /// <param name="pollingIntervalMs">轮询间隔（毫秒），默认1000ms</param>
        /// <param name="reconnectIntervalMs">重连间隔（毫秒），默认5000ms</param>
        public Semens(SiemensPLCS plcType, int pollingIntervalMs = 1000, int reconnectIntervalMs = 5000)
        {
            plc = new SiemensS7Net(plcType);
            _pollingIntervalMs = pollingIntervalMs;
            _reconnectIntervalMs = reconnectIntervalMs;

            // 初始化定时器（暂不启动）
            _pollingTimer = new Timer(OnPollingTick, null, Timeout.Infinite, Timeout.Infinite);
            _reconnectTimer = new Timer(OnReconnectTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// 连接PLC（线程安全）
        /// </summary>
        public OutCome Connect(string ipAddress, byte rack, byte slot, out string message)
        {
            message = string.Empty;
            lock (_commLock)
            {
                _ipAddress = ipAddress;
                _rack = rack;
                _slot = slot;

                return ConnectInternal(out message);
            }
        }

        /// <summary>
        /// 内部连接逻辑（已被 lock 保护）
        /// </summary>
        private OutCome ConnectInternal(out string message)
        {
            try
            {
                plc.IpAddress = _ipAddress;
                plc.Rack = _rack;
                plc.Slot = _slot;

                OperateResult res = plc.ConnectServer();
                bool previousState = isConnected;
                isConnected = res.IsSuccess;

                if (res.IsSuccess)
                {
                    message = "连接成功";
                    LastErrorMessage = "";

                    // 停止重连定时器，启动轮询定时器
                    _reconnectTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                    _pollingTimer?.Change(0, _pollingIntervalMs);

                    if (!previousState)
                    {
                        ConnectionStatusChanged?.Invoke(true);
                    }
                    return OutCome.Success;
                }
                else
                {
                    message = res.Message;
                    LastErrorMessage = res.Message;

                    // 连接失败，触发自动重连机制
                    StartReconnect();

                    if (previousState)
                    {
                        ConnectionStatusChanged?.Invoke(false);
                    }
                    return OutCome.Fail;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                LastErrorMessage = ex.Message;
                StartReconnect();
                return OutCome.Fail;
            }
        }

        /// <summary>
        /// 后台轮询回调（检测连接健康度）
        /// </summary>
        private void OnPollingTick(object? state)
        {
            if (_isDisposed) return;

            lock (_commLock)
            {
                if (!isConnected) return;

                // 通过读取简单地址检测连接
                var result = plc.ReadBool(HeartbeatAddress);
                if (!result.IsSuccess)
                {
                    // 通信中断，标记未连接并启动重连
                    isConnected = false;
                    LastErrorMessage = $"轮询通信失败: {result.Message}";

                    _pollingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                    StartReconnect();

                    ConnectionStatusChanged?.Invoke(false);
                }
            }
        }

        /// <summary>
        /// 后台自动重连回调
        /// </summary>
        private void OnReconnectTick(object? state)
        {
            if (_isDisposed) return;

            lock (_commLock)
            {
                if (isConnected) return;

                ConnectInternal(out _);
            }
        }

        /// <summary>
        /// 启动重连定时器
        /// </summary>
        private void StartReconnect()
        {
            _pollingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _reconnectTimer?.Change(_reconnectIntervalMs, _reconnectIntervalMs);
        }

        /// <summary>
        /// 断开连接（线程安全）
        /// </summary>
        public void Disconnect()
        {
            lock (_commLock)
            {
                // 停止所有定时器
                _pollingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _reconnectTimer?.Change(Timeout.Infinite, Timeout.Infinite);

                plc?.ConnectClose();
                bool previousState = isConnected;
                isConnected = false;

                if (previousState)
                {
                    ConnectionStatusChanged?.Invoke(false);
                }
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
        public OutCome ReadWrite(bool isRead, string area, int dbNumber, string startAddress, ref string value,
            PlcDataType dataType, out string message, int length = 1)
        {
            message = string.Empty;

            lock (_commLock)
            {
                if (!isConnected)
                {
                    message = "PLC未连接";
                    LastErrorMessage = message;
                    return OutCome.Fail;
                }

                try
                {
                    string address = BuildAddress(area, dbNumber, startAddress, dataType);
                    OutCome result = isRead
                        ? ReadValue(address, dataType, length, ref value, out message)
                        : WriteValue(address, dataType, value, length, out message);

                    // 如果读写失败且属于通信异常，触发重连机制
                    if (result == OutCome.Fail && isConnected)
                    {
                        var ping = plc.ReadBool(HeartbeatAddress);
                        if (!ping.IsSuccess)
                        {
                            isConnected = false;
                            StartReconnect();
                            ConnectionStatusChanged?.Invoke(false);
                        }
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    message = ex.Message;
                    LastErrorMessage = message;
                    return OutCome.Fail;
                }
            }
        }

        private string BuildAddress(string area, int dbNumber, string startAddress, PlcDataType dataType)
        {
            string areaUpper = area.ToUpper().Trim();
            bool isBit = (dataType == PlcDataType.Bool);

            if (areaUpper == "DB")
            {
                if (isBit)
                {
                    return $"DB{dbNumber}.DBX{startAddress}";
                }
                else
                {
                    string prefix = dataType switch
                    {
                        PlcDataType.Int16 or PlcDataType.UInt16 => "DBW",
                        PlcDataType.Int32 or PlcDataType.UInt32 or PlcDataType.Float => "DBD",
                        PlcDataType.String or PlcDataType.ByteArray => "DBB",
                        _ => ""
                    };
                    return $"DB{dbNumber}.{prefix}{startAddress}";
                }
            }
            else
            {
                return $"{areaUpper}{startAddress}";
            }
        }

        private OutCome ReadValue(string address, PlcDataType dataType, int length, ref string value, out string message)
        {
            message = string.Empty;
            try
            {
                switch (dataType)
                {
                    case PlcDataType.Bool:
                        var bRes = plc.ReadBool(address);
                        if (bRes.IsSuccess) { value = bRes.Content.ToString(); LastErrorMessage = ""; return OutCome.Success; }
                        message = bRes.Message; break;

                    case PlcDataType.Int16:
                        var i16Res = plc.ReadInt16(address);
                        if (i16Res.IsSuccess) { value = i16Res.Content.ToString(); LastErrorMessage = ""; return OutCome.Success; }
                        message = i16Res.Message; break;

                    case PlcDataType.Int32:
                        var i32Res = plc.ReadInt32(address);
                        if (i32Res.IsSuccess) { value = i32Res.Content.ToString(); LastErrorMessage = ""; return OutCome.Success; }
                        message = i32Res.Message; break;

                    case PlcDataType.UInt16:
                        var ui16Res = plc.ReadUInt16(address);
                        if (ui16Res.IsSuccess) { value = ui16Res.Content.ToString(); LastErrorMessage = ""; return OutCome.Success; }
                        message = ui16Res.Message; break;

                    case PlcDataType.UInt32:
                        var ui32Res = plc.ReadUInt32(address);
                        if (ui32Res.IsSuccess) { value = ui32Res.Content.ToString(); LastErrorMessage = ""; return OutCome.Success; }
                        message = ui32Res.Message; break;

                    case PlcDataType.Float:
                        var fRes = plc.ReadFloat(address);
                        if (fRes.IsSuccess) { value = fRes.Content.ToString(); LastErrorMessage = ""; return OutCome.Success; }
                        message = fRes.Message; break;

                    case PlcDataType.String:
                        var sRes = plc.ReadString(address, (ushort)length);
                        if (sRes.IsSuccess) { value = sRes.Content; LastErrorMessage = ""; return OutCome.Success; }
                        message = sRes.Message; break;

                    case PlcDataType.ByteArray:
                        var byteRes = plc.Read(address, (ushort)length);
                        if (byteRes.IsSuccess) { value = Convert.ToBase64String(byteRes.Content); LastErrorMessage = ""; return OutCome.Success; }
                        message = byteRes.Message; break;

                    default:
                        message = "不支持的数据类型"; break;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }

            LastErrorMessage = message;
            return OutCome.Fail;
        }

        private OutCome WriteValue(string address, PlcDataType dataType, string value, int length, out string message)
        {
            message = string.Empty;
            try
            {
                OperateResult result;
                switch (dataType)
                {
                    case PlcDataType.Bool:
                        if (!bool.TryParse(value, out bool bVal)) { message = "写入值无法转换为bool"; break; }
                        result = plc.Write(address, bVal);
                        if (result.IsSuccess) { LastErrorMessage = ""; return OutCome.Success; }
                        message = result.Message; break;

                    case PlcDataType.Int16:
                        if (!short.TryParse(value, out short sVal)) { message = "写入值无法转换为Int16"; break; }
                        result = plc.Write(address, sVal);
                        if (result.IsSuccess) { LastErrorMessage = ""; return OutCome.Success; }
                        message = result.Message; break;

                    case PlcDataType.Int32:
                        if (!int.TryParse(value, out int iVal)) { message = "写入值无法转换为Int32"; break; }
                        result = plc.Write(address, iVal);
                        if (result.IsSuccess) { LastErrorMessage = ""; return OutCome.Success; }
                        message = result.Message; break;

                    case PlcDataType.UInt16:
                        if (!ushort.TryParse(value, out ushort usVal)) { message = "写入值无法转换为UInt16"; break; }
                        result = plc.Write(address, usVal);
                        if (result.IsSuccess) { LastErrorMessage = ""; return OutCome.Success; }
                        message = result.Message; break;

                    case PlcDataType.UInt32:
                        if (!uint.TryParse(value, out uint uiVal)) { message = "写入值无法转换为UInt32"; break; }
                        result = plc.Write(address, uiVal);
                        if (result.IsSuccess) { LastErrorMessage = ""; return OutCome.Success; }
                        message = result.Message; break;

                    case PlcDataType.Float:
                        if (!float.TryParse(value, out float fVal)) { message = "写入值无法转换为Float"; break; }
                        result = plc.Write(address, fVal);
                        if (result.IsSuccess) { LastErrorMessage = ""; return OutCome.Success; }
                        message = result.Message; break;

                    case PlcDataType.String:
                        result = plc.Write(address, value);
                        if (result.IsSuccess) { LastErrorMessage = ""; return OutCome.Success; }
                        message = result.Message; break;

                    case PlcDataType.ByteArray:
                        byte[] bytes;
                        try { bytes = Convert.FromBase64String(value); }
                        catch { message = "写入值无法从Base64转换为字节数组"; break; }
                        result = plc.Write(address, bytes);
                        if (result.IsSuccess) { LastErrorMessage = ""; return OutCome.Success; }
                        message = result.Message; break;

                    default:
                        message = "不支持的数据类型"; break;
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
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            Disconnect();

            _pollingTimer?.Dispose();
            _reconnectTimer?.Dispose();
        }
    }
}