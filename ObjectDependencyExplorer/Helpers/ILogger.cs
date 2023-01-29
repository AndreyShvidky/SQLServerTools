using System.ComponentModel;

namespace ObjectDependencyExplorer.ErrorHandlers
{
    public interface ILogger
    {
        void LogDebug(string message);

        void LogInformation(string message);

		void LogWarning(WarningException exception);

		void LogWarning(string message);

        void LogWarning(string message, WarningException exception);

        void LogException(Exception exception);

        void LogException(string message);

        void LogException(string message, Exception exception);
    }
}
