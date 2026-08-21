using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfApp1.Service.Communication;
using WpfApp1.ViewModels;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainWindowViewModel viewModel;       
        public MainWindow()
        {
           
            InitializeComponent();
            viewModel = new MainWindowViewModel();
            this.DataContext = viewModel;
                                      
        }            
        #region 固定部分
        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        private void ScaleWindow(Object sender, RoutedEventArgs e)
        {
            if(this.WindowState==WindowState.Normal)
            this.WindowState = WindowState.Maximized;
            else
                this.WindowState = WindowState.Normal;
        }
        private void MiniBar(Object sender, RoutedEventArgs e)
        {
            this.WindowState= WindowState.Minimized;
        }
        #endregion

    }
}