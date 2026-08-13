using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WpfApp1.Models
{
    public class RelayCommand : ICommand
    {
       // public event EventHandler? CanExecuteChanged;
        private readonly Action excute;
        private readonly Func<bool> canExecute;
        public RelayCommand(Action _execute, Func<bool> _canExecute) 
        { 
           excute = _execute;
           canExecute = _canExecute;
        }
        public bool CanExecute(object? parameter)
        {
            if(canExecute==null)
            {
                return true;
            }
            else
            {
                return canExecute();
            }
        }

        public void Execute(object? parameter)
        {
            excute();
        }
        public event EventHandler CanExecuteChanged
        {
            add
            {
                if (canExecute != null)
                {
                    CommandManager.RequerySuggested += value;
                }
            }
            remove { 
              CommandManager.RequerySuggested -= value;
            }
        }
    }
}
