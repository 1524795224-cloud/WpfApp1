using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WpfApp1.Models;

namespace WpfApp1.ViewModels
{
    public class LoginViewModel:ModelPropertyBase
    {

        public Action? CloseAction { get; set; }
        private readonly LoginModel _modelLogin = new LoginModel();      
        public string Corporation
        {
            get
            {
                return _modelLogin.CorporationName;
            }
            set
            {
                _modelLogin.CorporationName=value;
                OnPropertyChanged();
            }
        }
        public string Password
        {
            get
            {
                return _modelLogin.Password;
            }
            set 
            {
                _modelLogin.Password=value;
                OnPropertyChanged();
            }
        }
        public string User
        {
            get
            {
                return _modelLogin.User;
            }
            set 
            { 
                _modelLogin.User=value;
                OnPropertyChanged();
            }
        }
        public bool RememberMe { get; }
        public ICommand LoginCommand
        {
            get
            {
                return new RelayCommand(LoginAction, CanLoginExecute);
            }
        }
        public ICommand ExitComand 
        {
            get 
            {
                return new RelayCommand(ExitAction, CanLoginExecute);
            }
        }

        private void ExitAction()
        {
            User = "";
            Password = "";
        }

        private bool CanLoginExecute()
        {
            if (Password == null || User == null)
            {
                return false;
            }
            return true;
            
        }

        private void LoginAction()
        {
            if (User == "管理员" && Password == "123456")
            {
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
                SystemStaton.IsAdmin = true;
                CloseAction?.Invoke();
            }
            else if (User == "员工")
            {
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
                CloseAction?.Invoke();
            }
            else
             MessageBox.Show("请输入正确用户名和密码");
        }
    }
}
