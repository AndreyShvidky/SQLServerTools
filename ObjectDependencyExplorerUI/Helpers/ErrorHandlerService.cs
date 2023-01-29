using DBConnectDialog.Interfaces;
using System;
using System.Text;
using System.Windows;

namespace ObjectDependencyExplorerUI
{
	public class ErrorHandlerService : IErrorHandlerService
	{
		public void Handle(Exception exception, string caption)
		{
			var mainWindow = Application.Current.MainWindow;
			var message = new StringBuilder();
			if (exception != null)
			{
				message.AppendLine(exception.Message)
					.AppendLine(exception.GetType().FullName)
					.AppendLine(exception.StackTrace);
			}
			if (mainWindow == null)
				MessageBox.Show(message.ToString(), caption, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
			else
			{
				MessageBox.Show(mainWindow, message.ToString(), caption, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
			}
		}
	}
}
