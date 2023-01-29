using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ObjectDependencyExplorer.Comands
{
	public class SimpleCommand : ICommand, INotifyPropertyChanged
	{
		private readonly Action<object> _execute;
		private readonly Func<object, bool> _canExecute;
		public event EventHandler CanExecuteChanged;
		public event PropertyChangedEventHandler PropertyChanged;

		private string _cantExecuteReason;

		public SimpleCommand(Action<object> execute, Func<object, bool> canExecute)
		{
			_execute = execute;
			_canExecute = canExecute;
		}

		public SimpleCommand(Action<object> execute) : this(execute, null) { }

		public bool CanExecute(object parameter)
		{
			bool result = true;

			if (_canExecute != null)
				result = _canExecute(parameter);

			if (!string.IsNullOrEmpty(_cantExecuteReason))
				result = false;

			return result;
		}

		public void Execute(object parameter)
		{
			_execute(parameter);
		}

		public void RaiseCanExecuteChanged()
		{
			CanExecuteChanged?.Invoke(this, new EventArgs());
		}

		protected void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public string CantExecuteReason
		{
			get
			{
				return _cantExecuteReason;
			}

			set
			{
				_cantExecuteReason = value;
				NotifyPropertyChanged();
			}
		}
	}
}