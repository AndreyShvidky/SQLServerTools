using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ObjectDependencyExplorer.ErrorHandlers
{
	public class SimpleLogItem : ILogItem
	{
		private MessageType _type;
		private string _message;

		public SimpleLogItem(MessageType type, string message)
		{
			_type = type;
			_message = message;
		}

		public MessageType Type { get => _type; set => _type = value; }
		public string Message { get => _message; set => _message = value; }
	}

	// ObservableCollection logger for colored FlowDocument
	public class SimpleLogger : ILogger
	{
		private ObservableCollection<SimpleLogItem> logItems;

		public SimpleLogger()
		{
			logItems = new();
		}

		public ObservableCollection<SimpleLogItem> Items
		{
			get => logItems;
		}

		public void Clear()
		{
			try
			{
				logItems.Clear();
			}
			catch { }
		}

		private void AppendText(string message)
		{
			AppendText(MessageType.Message, message);
		}

		private void AppendText(MessageType type, string message)
		{
			logItems.Add(new SimpleLogItem(type, message));
		}

		// Добавляет строку лога
		public void LogInformation(string message)
		{
			AppendText(message);
		}

		public void LogInformationWithTimestamp(string text)
		{
			AppendText(DateTime.Now.ToString("G") + " " + text);
		}

		public void LogWarning(WarningException exception)
		{
			AppendText(MessageType.Warning, exception.Message);
		}

		public void LogWarning(string message)
		{
			AppendText(MessageType.Warning, message);
		}

		public void LogWarning(string message, WarningException exception)
		{
			AppendText(MessageType.Warning, message + "\n" + exception.Message);
		}

		public void LogException(Exception exception)
		{
			AppendText(MessageType.Error, exception.Message);
		}

		public void LogException(string message)
		{
			AppendText(MessageType.Error, message);
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
			AppendText(MessageType.Error, message + "\n" + exMessage);
		}

		public void LogDebug(string message)
		{
			LogInformationWithTimestamp(message + "\n" + message);
		}
	}
}
