using ObjectDependencyExplorer.ErrorHandlers;
using System.ComponentModel;

namespace ObjectDependencyExplorerConsole.ErrorHandlers
{
	// ObservableCollection logger for colored FlowDocument
	public class SimpleConsoleLogger : ILogger
	{
		public SimpleConsoleLogger()
		{

		}

		// Добавляет строку лога
		public void LogInformation(string message)
		{
			Console.WriteLine(message);
		}

		public void LogInformationWithTimestamp(string text)
		{
			Console.WriteLine(DateTime.Now.ToString("G") + " " + text);
		}

		public void LogWarning(WarningException exception)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(exception.Message);
			Console.ResetColor();
		}

		public void LogWarning(string message)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(message);
			Console.ResetColor();
		}

		public void LogWarning(string message, WarningException exception)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(message + "\n" + exception.Message);
			Console.ResetColor();
		}

		public void LogException(Exception exception)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(exception.Message);
			Console.ResetColor();
		}

		public void LogException(string message)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(message);
			Console.ResetColor();
		}

		public void LogException(string message, Exception exception)
		{
			string exMessage = exception.Message;
			Exception exInner = exception.InnerException;
			while (exInner != null)
			{
				exMessage += "\n\n" + exInner.Message;
				exInner = exInner.InnerException;
			}
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(message + "\n" + exMessage);
			Console.ResetColor();
		}

		public void LogDebug(string message)
		{
			LogInformationWithTimestamp(message + "\n" + message);
		}
	}
}
