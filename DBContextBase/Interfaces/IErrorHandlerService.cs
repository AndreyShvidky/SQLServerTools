using System;

namespace DBConnectDialog.Interfaces
{
	public interface IErrorHandlerService
	{
		void Handle(Exception exception, string message = "");
	}
}