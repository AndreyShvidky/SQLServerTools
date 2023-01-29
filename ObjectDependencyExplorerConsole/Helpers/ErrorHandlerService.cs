using DBConnectDialog.Interfaces;
using System.Text;

namespace ObjectDependencyExplorer.ErrorHandlers
{
	public class ErrorHandlerService : IErrorHandlerService
	{
		public void Handle(Exception exception, string caption)
		{
			var message = new StringBuilder();
			if (exception != null)
			{
				message.AppendLine(exception.Message)
					.AppendLine(exception.GetType().FullName)
					.AppendLine(exception.StackTrace);

				if (exception.InnerException != null)
					message.AppendLine(exception.InnerException.Message)
					.AppendLine(exception.InnerException.GetType().FullName)
					.AppendLine(exception.InnerException.StackTrace);
			}

			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(message.ToString());
			Console.ResetColor();
		}
	}
}
