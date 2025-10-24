using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Wpf_MVVM_Read_Onnx_Using_Canvas.ViewModels
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
    //public class RelayCommand : ICommand
    //{
    //    private readonly Action<object> _execute;
    //    private readonly Predicate<object> _canExecute;

    //    public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
    //    {
    //        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    //        _canExecute = canExecute;
    //    }

    //    public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;

    //    public void Execute(object parameter) => _execute(parameter);

    //    public event EventHandler CanExecuteChanged;
    //    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    //}
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _can;
        public RelayCommand(Action execute, Func<bool> can = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _can = can;
        }
        public bool CanExecute(object p) => _can?.Invoke() ?? true;
        public void Execute(object p) => _execute();
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }
}
