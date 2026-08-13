using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfApp1.Models;

namespace WpfApp1.ViewModels
{
    public class MainWindowViewModel:ModelPropertyBase
    {

        public HightSettingModel hightSettingModel { get; set; }
        
        public MainWindowViewModel()
        {
            hightSettingModel = new HightSettingModel();          
        }

    }
}
