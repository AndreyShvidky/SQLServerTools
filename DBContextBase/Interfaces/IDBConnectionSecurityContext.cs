using System.ComponentModel;
using System.Security;

namespace DBContextBase.Interfaces
{
	public enum ServerTypes
	{
		[Description("Microsoft SQL Server")]
		MSSQL = 0,
		[Description("PostgreSQL")]
		PGSQL = 1
	}

	public enum AuthenticateTypes
	{
		[Description("Windows")]
		Windows = 0,
		[Description("SQL")]
		SQL = 1
	}

	public interface IDBConnectionSecurityContext
	{
		public ServerTypes ServerType { get; set; }
		public string Server { get; set; }
		public AuthenticateTypes AuthenticateType { get; set; }
		public string Login { get; set; }
		public SecureString Password { get; set; }

		// Connect to Server (and Database if provided)
		void TestConnection();
	}
}