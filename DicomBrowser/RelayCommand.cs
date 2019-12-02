using System;
using System.Windows.Input;

namespace DicomBrowser
{
    public class RelayCommand : ICommand
    {
        private Action<object> _action;
        private ICommand show;

        public RelayCommand(Action<object> action)
        {
            _action = action;
        }

        public RelayCommand(ICommand show)
        {
            this.show = show;
        }

        #region ICommand Members

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public event EventHandler CanExecuteChanged;

        public void Execute(object parameter)
        {
            _action(parameter);
        }

        #endregion
    }
}
