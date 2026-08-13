using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfApp1.Models;

namespace WpfApp1.ViewModels
{
   public class Class1VM:ModelPropertyBase
    {
        private readonly Class1 _model = new Class1();
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
        public string PIN13
        {
            get { return _model.PIN3Y; }
            set { _model.PIN3Y = value; OnPropertyChanged(); }
        }
    }
}
