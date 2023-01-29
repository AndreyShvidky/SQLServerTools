using DBContextBase.Interfaces;
using System.Data.SqlClient;
using System.Security;

namespace DBConnectTest.Helpers
{
	internal class DBConnectionSecurityContext : IDBConnectionSecurityContext
	{
		public ServerTypes ServerType { get; set; }
		public string Server { get; set; }
		public AuthenticateTypes AuthenticateType { get; set; }
		public string Login { get; set; }
		public SecureString Password { get; set; }
		public string Database { get; set; }

		public void TestConnection()
		{
			switch (ServerType)
			{
				case ServerTypes.MSSQL:
					{
						SqlConnectionStringBuilder connectionBuilder = new();
						connectionBuilder.DataSource = Server;
						if (AuthenticateType == AuthenticateTypes.Windows)
							connectionBuilder.IntegratedSecurity = true;
						else
							connectionBuilder.IntegratedSecurity = false;

						if (!string.IsNullOrEmpty(Database))
							connectionBuilder.InitialCatalog = Database;

						using SqlConnection con = new(connectionBuilder.ConnectionString);
						if (AuthenticateType != AuthenticateTypes.Windows)
							con.Credential = new(Login, Password);

						connectionBuilder.ApplicationName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;

						con.Open();
					}
					break;
				default:
					throw new System.NotImplementedException();
			}
		}
	}
}
