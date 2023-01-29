using ObjectDependencyExplorer;
using ObjectDependencyExplorer.ErrorHandlers;
using ObjectDependencyExplorer.Model;
using ObjectDependencyExplorerConsole.ErrorHandlers;
using System.Net;
using System.Security;

namespace ObjectDependencyExplorerConsole
{
	class Program
	{
		private const string PARAMETER_KEY_SERVER = "-S";
		private const string PARAMETER_KEY_TRUSTED = "-E";
		private const string PARAMETER_KEY_LOGIN = "-U";
		private const string PARAMETER_KEY_PASSWORD = "-P";
		private const string PARAMETER_KEY_DATABASE = "-D";

		private static DBHelper currentConnection;
		private static SimpleConsoleLogger logger;
		private static SQLDependencyExplorer sqlDependencyExplorer;

		static void Main(string[] args)
		{
			// Processing arguments
			if (args.Length == 0)
			{
				ShowHelpAndExit();
				return;
			}

			// DB context
			currentConnection = new();
			// Logger
			logger = new();
			// ErrorHandler
			ErrorHandlerService errHnd = new();

			string currentArgumentKey = null;
			bool trustedWasProvided = false;
			string login = null;
			SecureString password = null;

			// Working
			try
			{
				foreach (string arg in args)
				{
					switch (arg)
					{
						case PARAMETER_KEY_SERVER:
						case PARAMETER_KEY_TRUSTED:
						case PARAMETER_KEY_LOGIN:
						case PARAMETER_KEY_PASSWORD:
						case PARAMETER_KEY_DATABASE:
							currentArgumentKey = arg;
							break;
						default:
							break;
					}

					switch (currentArgumentKey)
					{
						case PARAMETER_KEY_SERVER:
							currentConnection.Server = arg;
							break;
						case PARAMETER_KEY_TRUSTED:
							trustedWasProvided = true;
							break;
						case PARAMETER_KEY_LOGIN:
							login = arg;
							break;
						case PARAMETER_KEY_PASSWORD:
							password = new NetworkCredential(string.Empty, arg).SecurePassword;
							break;
						case PARAMETER_KEY_DATABASE:
							currentConnection.DataBase = arg;
							break;
						default:
							break;
					}
				}

				if (trustedWasProvided)
				{
					currentConnection.AuthenticateType = DBContextBase.Interfaces.AuthenticateTypes.Windows;
					if (login != null)
						logger.LogWarning("Trusted connection argument was provided, login ignored!");
				}
				else
				{
					currentConnection.AuthenticateType = DBContextBase.Interfaces.AuthenticateTypes.SQL;
					currentConnection.Login = login;
					currentConnection.Password = password;
				}

				if (currentConnection.Server == null)
					throw new ArgumentException("Server not specified");

				if (currentConnection.AuthenticateType == DBContextBase.Interfaces.AuthenticateTypes.SQL && (currentConnection.Login == null || currentConnection.Password == null))
					throw new ArgumentException("Login or password not specified");

				if (currentConnection.DataBase == null)
					throw new ArgumentException("Database not specified");

				if (currentConnection.DataBase == "master")
					logger.LogWarning("Database not specified, master used");

				sqlDependencyExplorer = new(currentConnection, logger);
				sqlDependencyExplorer.ExploreDependencies(false, false);

				Console.WriteLine("Press any key to exit");
				Console.ReadKey(true);
			}
			catch (Exception ex)
			{
				errHnd.Handle(ex, "Failed to explore dependencies");
			}
		}

		private static void ShowHelpAndExit()
		{
			Console.WriteLine("Usage:");
			Console.WriteLine("Connect with trusted connection:");
			Console.WriteLine("ObjectDependencyExplorerConsole -S {Server} -E -D {DataBase}");
			Console.WriteLine("Connect with SQL authentication:");
			Console.WriteLine("ObjectDependencyExplorerConsole -S {Server} -U {Login} -P {Password} -D {DataBase}");
			Console.WriteLine("Press any key to exit");
			Console.ReadKey(true);
		}
	}
}