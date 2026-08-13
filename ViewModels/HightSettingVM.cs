using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using WpfApp1.Models;
using WpfApp1.Service.Communication;
using WpfApp1.StaticClasses.Log;

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
            tcpSocket.OnConnected +=async () => { await Task.Delay(10); Logger.Info("多线激光连接成功"); };
            tcpSocket.OnDisconnected += async() => { await Task.Delay(10); Logger.Info("多线激光断开连接"); };           
            tcpSocket.OnReceived += async (s) => {
                try
                {
                    await DataAnnalysis(s);
                    Logger.Info($"{Encoding.ASCII.GetString(s)}");
                }
                catch (Exception ex)
                {
                    Logger.Error("数据处理异常");
                }
            };
           _= tcpSocket.ConnectAsync(_host,_port);
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
    }
}
