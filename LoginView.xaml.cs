using System.Windows;
using System.Windows.Input;
using WpfApp1.ViewModels;

namespace WpfApp1
{

    /// <summary>
    /// Window1.xaml 的交互逻辑
    /// </summary>
    public partial class LoginView : Window
    {
        private LoginViewModel? _viewModel;
        public LoginView()
        {
            InitializeComponent();
            _viewModel = new LoginViewModel();
            _viewModel.CloseAction += this.Close;
            this.DataContext = _viewModel;
        }
        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void ExitApp_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // 关闭整个应用程序

        }
    }
}
