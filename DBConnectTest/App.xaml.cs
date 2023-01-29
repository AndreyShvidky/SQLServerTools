using DBConnectDialog;
using DBConnectTest.Helpers;
using System;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace DBConnectTest
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			DispatcherUnhandledException += OnDispatcherUnhandledException;

			// Creating DataBase context - what we need
			DBConnectionSecurityContext currentDBContext = new() { Server = "DEV-SR-SQL2016\\SQL2016SRV" };

			// Creating Model
			// Model has empty constructor because we want to have Design Time Model. Eternal war View or ViewMode first. All magic in ViewModel.Initalize
			DBConnectViewModel viewModel = new();
			viewModel.Initalize(currentDBContext, new ErrorHandlerService());

			// Creating View
			DBConnectWindow dbConnectWindow = new(viewModel);
			dbConnectWindow.Owner = Application.Current.MainWindow;
			MainWindow = dbConnectWindow;

			// Showtime
			dbConnectWindow.ShowDialog();
		}


		private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs dispatcherUnhandledExceptionEventArgs)
		{
			Exception exception = dispatcherUnhandledExceptionEventArgs.Exception;
			if (exception is TargetInvocationException invocationException)
			{
				exception = invocationException.InnerException;
			}
			var stackTrace = exception.StackTrace;
			var image = MessageBoxImage.Error;
			if (exception is ApplicationException)
			{
				stackTrace = "";
				image = MessageBoxImage.Warning;
				dispatcherUnhandledExceptionEventArgs.Handled = true;
			}

			var mainWindow = MainWindow;
			if (mainWindow != null)
				MessageBox.Show(MainWindow, $"{exception.Message}{Environment.NewLine}{stackTrace}", "", MessageBoxButton.OK, image);
			else
			{
				MessageBox.Show($"{exception.Message}{Environment.NewLine}{stackTrace}", "", MessageBoxButton.OK, image);
			}

			dispatcherUnhandledExceptionEventArgs.Handled = true;
		}
	}
}
