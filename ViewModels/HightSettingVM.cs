using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using WpfApp1.Models;
using WpfApp1.Service.Communication;
using WpfApp1.Service.Communication.Interfaces;
using WpfApp1.Service.Communication.Sql;


namespace WpfApp1.ViewModels
{
    public class HightSettingVM:ModelPropertyBase
    {
        private bool _isTesting = false;
        private string _host = "127.0.0.1";
        private int _port = 9000;
        public readonly HightSettingModel _model;

        public ITcpClientSocker tcpSocket;     
        private IPlcServers plc;
        public IDataStorageProcessor dataStorage;

        private CancellationTokenSource _cts = new();
        byte[] startCommand=Encoding.UTF8.GetBytes("IDN?");

        public HightSettingVM(IPlcServers _plc, IDataStorageProcessor dataStorageProcessor, ITcpClientSocker _tcp)
        {
            plc = _plc;
            dataStorage= dataStorageProcessor;
            tcpSocket= _tcp;
            _model = new HightSettingModel();
            tcpSocket = new HightTcpClient(1024);
            tcpSocket.OnConnected +=async () => { await Task.Delay(1);Message = "高度通讯连接成功"; };
            tcpSocket.OnDisconnected += async() => { await Task.Delay(1); Message = "高度通讯断开连接"; };                    
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => HightTestStationAsync(_cts.Token), CancellationToken.None);

        }
        private async Task HightTestStationAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!plc.IsConnected)
                    {
                        await Task.Delay(1000, token);
                        continue;
                    }

                    string startValue = "";
                    var result = plc.ReadWrite(
                        isRead: true,
                        area: "DB",
                        dbNumber: 100,
                        startAddress: "0.0",   // 启动信号地址
                        ref startValue,
                        PlcDataType.Bool,
                        out string msg);

                    if (result == OutCome.Success && startValue == "True")
                    {
                        await ExecuteTestAsync();   // 等待整个测试完成
                    }

                    await Task.Delay(300, token);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"轮询异常: {ex.Message}");
                    await Task.Delay(1000, token);
                }
            }
        }

        // 替换 ExecuteTestAsync 方法中的 handler 定义和相关事件订阅/移除
        // 原代码：Action<byte[],Task>? handler = null;
        // 修正为：Func<byte[], Task>? handler = null;

        private async Task ExecuteTestAsync()
        {
            // 防止重入
            if (_isTesting) return;
            _isTesting = true;

            // 用于等待接收数据的 TaskCompletionSource
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

            // 定义临时事件处理器
            Func<byte[], Task>? handler = null;
            handler = (data) =>
            {
                // 只处理本次测试的数据（可根据需要添加过滤，例如检查数据长度或标识）
                tcs.TrySetResult(data);
                tcpSocket.OnReceived -= handler;  // 立即移除自身，避免重复
                return Task.CompletedTask;
            };

            // 订阅事件
            tcpSocket.OnReceived += handler;

            try
            {
                // 发送测试指令
                await tcpSocket.SendAsync(startCommand);

                // 等待数据，设置超时（例如 5 秒）
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                byte[] receivedData = await tcs.Task.WaitAsync(timeoutCts.Token);

                // 解析数据
                await DataAnnalysis(receivedData);

                // 判断测试结果（示例：假设 PIN1X == "OK" 表示通过）
                bool isOK = PIN1X == "OK";  // 根据实际逻辑修改

                // 写结果到 PLC
                string resultAddress = isOK ? "0.1" : "0.2";   // 结果地址
                string writeValue = "True";
                var writeResult = plc.ReadWrite(
                    isRead: false,
                    area: "DB",
                    dbNumber: 100,
                    startAddress: resultAddress,
                    ref writeValue,
                    PlcDataType.Bool,
                    out string writeMsg);

                if (writeResult != OutCome.Success)
                {
                    Message = "PLC 结果写回失败: " + writeMsg;
                }
            }
            catch (OperationCanceledException)
            {
                Message = "测试超时";
                // 可写超时结果到 PLC（可选）
            }
            catch (Exception ex)
            {
                Message = ex.Message;
            }
            finally
            {
                // 确保事件已移除（如果还没移除）
                tcpSocket.OnReceived -= handler;

                // 复位启动信号，通知 PLC 测试完成
                string startValue = "False";
                plc.ReadWrite(false, "DB", 100, "0.0", ref startValue, PlcDataType.Bool, out _);

                _isTesting = false;
            }
        }

        //接收到数据后解析数据，更新UI属性
        private async Task DataAnnalysis(byte[] s)
        {
            string data = Encoding.ASCII.GetString(s);
            string[] strings = data.Split(',');
            //逻辑处理不要放在Invock里
            string pin1X = strings[0];
            string pin2X = strings[1];
            string pin3X = strings[2];
            string pin1Y = strings[3];
            string pin2Y = strings[4];
            string pin3Y = strings[5];
            var recordModel = new HightSettingModel
            {
                PIN1X = strings[0],
                PIN2X = strings[1],
                PIN3X = strings[2],
                PIN1Y = strings[3],
                PIN2Y = strings[4],
                PIN3Y = strings[5],
                DateTime=DateTime.Now,
                EndResult=true,
                ProductionName="AGS110"
            };
            IsRunning=true;
            dataStorage.Enqueue(recordModel);
            // 在 UI 线程更新属性
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                PIN1X = pin1X;
                PIN2X = pin2X;
                PIN3X = pin3X;
                PIN1Y = pin1Y;
                PIN2Y = pin2Y;
                PIN3Y = pin3Y;
               
            });
        }
        //启动TCP连接命令
        public ICommand StartTcp
        { 
            get
            { 
                return new RelayCommand(Connect,IsCanExecute); 
            }
        }

        private bool IsCanExecute()
        {
           if(tcpSocket.Connected) return false;
           else return true;
        }

        private void Connect()
        {
           _=tcpSocket.ConnectAsync(_host, _port);
        }
        #region 测试项
        public string PIN1X
        {
            get { return _model.PIN1X; }
            set { _model.PIN1X = value;
                OnPropertyChanged(nameof(PIN1X));
               /* OnPropertyChanged(nameof(Pin1Color))*/; }
        }
        public string PIN2X
        {
            get { return _model.PIN2X; }
            set { _model.PIN2X = value; OnPropertyChanged(); }
        }
        public string PIN3X
        {
            get { return _model.PIN3X; }
            set { _model.PIN3X = value; OnPropertyChanged(); }
        }
        public string PIN1Y
        {
            get { return _model.PIN1Y; }
            set { _model.PIN1Y = value; OnPropertyChanged(); }
        }
        public string PIN2Y
        {
            get { return _model.PIN2Y; }
            set { _model.PIN2Y = value; OnPropertyChanged(); }
        }
        public string PIN3Y
        {
            get { return _model.PIN3Y; }
            set { _model.PIN3Y = value; OnPropertyChanged(); }
        }
        #endregion
        #region 各测试项结果
        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                _isRunning = value;
                OnPropertyChanged(nameof(IsRunning));
            }
        }
        #endregion
        public string Message
        {
            get { return _model.Message; }
            set { 
                _model.Message = value; OnPropertyChanged();
                //将数据保存到D:Log//文件名为当天日期.txt，内容为具体时间和Message内容
                _ =LogService.WriteAsync(value);
            }
        }
       
    }
}
