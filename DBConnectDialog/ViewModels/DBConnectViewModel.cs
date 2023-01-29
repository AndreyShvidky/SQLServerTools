using DBConnectDialog.Interfaces;
using DBContextBase.Interfaces;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DBConnectDialog
{
	public class DBConnectViewModel : INotifyPropertyChanged
	{
		private IDBConnectionSecurityContext dbConnectionSecurityContext;
		private IErrorHandlerService errorHandlerService;

		private bool isBusy;

		public event PropertyChangedEventHandler? PropertyChanged;

		public SimpleCommand TryConnectCommand { get; }

		public DBConnectViewModel()
		{
			TryConnectCommand = new SimpleCommand(obj =>
			{
				try
				{
					if (IsBusy) return;
					IsBusy = true;

					Task connectTask = Task.Run(() => Connect())
						.ContinueWith(task =>
						{
							IsBusy = false;

							if (task.Exception != null)
							{
								var exception = task.Exception.InnerException;
								errorHandlerService.Handle(exception, "Failed connect to Database");
								return;
							}

							//SaveParameters();
							Window view = obj as Window;
							if (view != null)
							{
								view.DialogResult = true;
								view.Close();
							}
						}, CancellationToken.None, TaskContinuationOptions.AttachedToParent, TaskScheduler.FromCurrentSynchronizationContext());
				}
				catch (Exception)
				{
					IsBusy = false;
					throw;
				}
			}, obj =>
			{
				if (IsBusy)
					return false;

				if (!string.IsNullOrEmpty(ServerName) && (AuthenticateType == AuthenticateTypes.Windows || (!string.IsNullOrEmpty(Login) && Password?.Length > 0)))
					return true;

				return false;
			});
		}

		public void Initalize(IDBConnectionSecurityContext dbContext, IErrorHandlerService errHandler)
		{
			dbConnectionSecurityContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
			errorHandlerService = errHandler ?? throw new ArgumentNullException(nameof(errHandler));

			try
			{
				//_loginSettings = loginSettings ?? new LoginSettingsService();
				//GetDefaultFromSettings();
			}
			catch (Exception e)
			{
				MessageBox.Show($"Failed to read settings{Environment.NewLine}{e.StackTrace}", "", MessageBoxButton.OK);
			}
		}

		private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private void Connect()
		{
			dbConnectionSecurityContext?.TestConnection();
		}

		public ServerTypes ServerType
		{
			get
			{
				if (dbConnectionSecurityContext == null) return ServerTypes.MSSQL;
				return dbConnectionSecurityContext.ServerType;
			}
			set {
				if (dbConnectionSecurityContext == null) return;
				dbConnectionSecurityContext.ServerType = value;
				NotifyPropertyChanged();
				TryConnectCommand.RaiseCanExecuteChanged();
			}
		}

		public string? ServerName
		{
			get => dbConnectionSecurityContext?.Server;
			set
			{
				if (dbConnectionSecurityContext == null) return;
				dbConnectionSecurityContext.Server = (string)value;
				NotifyPropertyChanged();
				TryConnectCommand.RaiseCanExecuteChanged();
			}
		}

		public AuthenticateTypes AuthenticateType
		{
			get
			{
				if (dbConnectionSecurityContext == null) return AuthenticateTypes.Windows;
				return dbConnectionSecurityContext.AuthenticateType;
			}
			set
			{
				if (dbConnectionSecurityContext == null) return;
				dbConnectionSecurityContext.AuthenticateType = value;
				NotifyPropertyChanged();
				NotifyPropertyChanged(nameof(IsLoginEnabled));
				NotifyPropertyChanged(nameof(IsPasswordEnabled));
				TryConnectCommand.RaiseCanExecuteChanged();
			}
		}

		public string? Login
		{
			get => dbConnectionSecurityContext?.Login;
			set
			{
				if (dbConnectionSecurityContext == null) return;
				dbConnectionSecurityContext.Login = (string)value;
				NotifyPropertyChanged();
				TryConnectCommand.RaiseCanExecuteChanged();
			}
		}

		public bool IsLoginEnabled
		{
			get {
				if (dbConnectionSecurityContext?.AuthenticateType == AuthenticateTypes.SQL)
					return true;

				return false;
			}
		}

		public SecureString? Password
		{
			private get => dbConnectionSecurityContext?.Password;
			set {
				if (dbConnectionSecurityContext == null) return;
				dbConnectionSecurityContext.Password = (SecureString)value;
				TryConnectCommand.RaiseCanExecuteChanged();
			}
		}

		public bool IsPasswordEnabled
		{
			get
			{
				if (dbConnectionSecurityContext?.AuthenticateType == AuthenticateTypes.SQL)
					return true;

				return false;
			}
		}

		public bool IsBusy
		{
			get => isBusy;
			set {
				isBusy = value;
				NotifyPropertyChanged();
				TryConnectCommand.RaiseCanExecuteChanged();
			}
		}
	}
}
