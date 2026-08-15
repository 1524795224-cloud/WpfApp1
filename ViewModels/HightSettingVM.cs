using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WpfApp1.Models;
using WpfApp1.Service.Communication;


namespace WpfApp1.ViewModels
{
    public class HightSettingVM:ModelPropertyBase
    {
        private string _host = "127.0.0.1";
        private int _port = 9000;
        public TcpSocketClient tcpSocket;
        public readonly HightSettingModel _model;


        public HightSettingVM()
        {
            _model = new HightSettingModel();
            tcpSocket = new TcpSocketClient(1024);
            tcpSocket.OnConnected +=async () => { await Task.Delay(1);Message = "高度通讯连接成功"; };
            tcpSocket.OnDisconnected += async() => { await Task.Delay(1); Message = "高度通讯断开连接"; };           
            tcpSocket.OnReceived += async (s) => {
                try
                {
                    await DataAnnalysis(s);
                    Message = "高度数据解析完成";
                }
                catch (Exception ex)
                {
                   Message= ex.Message;
                }
            };
           //_= tcpSocket.ConnectAsync(_host,_port);
        }

        private async Task DataAnnalysis(byte[] s)
        {
            string data = Encoding.ASCII.GetString(s);
            string[] strings = data.Split(',');

            // 在 UI 线程更新属性
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                PIN1X = strings[0];
                PIN2X = strings[1];
                PIN3X = strings[2];
                PIN1Y = strings[3];
                PIN2Y = strings[4];
                PIN3Y = strings[5];
            });
        }
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

        public string PIN1X
        {
            get { return _model.PIN1X; }
            set { _model.PIN1X = value; OnPropertyChanged(); }
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
        public string Message
        {
            get { return _model.Message; }
            set { 
                _model.Message = value; OnPropertyChanged();
                //将数据保存到D:Log//文件名为当天日期.txt，内容为具体时间和Message内容
                _ = SaveLogAsync(value);
            }
        }
        private async Task SaveLogAsync(string message)
        {
            try
            {
                // 1. 确保目录存在
                string dirPath = @"D:\Log";
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                // 2. 拼接文件名（当天日期）和日志格式
                string fileName = $"{DateTime.Now:yyyy-MM-dd}.txt";
                string filePath = Path.Combine(dirPath, fileName);
                string logContent = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";

                // 3. 异步追加写入文件
                await File.AppendAllTextAsync(filePath, logContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // 可在此处记录异常日志，避免日志写入失败导致程序崩溃
                System.Diagnostics.Debug.WriteLine($"写入日志失败: {ex.Message}");
            }
        }
    }
}
