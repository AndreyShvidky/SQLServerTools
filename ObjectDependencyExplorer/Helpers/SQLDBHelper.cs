using DBContextBase.Interfaces;
using System.Data;
using System.Data.SqlClient;
using System.Security;

namespace ObjectDependencyExplorer
{
	public partial class DBHelper : IDBConnectionSecurityContext
    {
		//private readonly string connectionString;
		private readonly SqlConnectionStringBuilder connectionStringBuilder;
		private string login;
		private SecureString password;

		public ServerTypes ServerType { get; set; }
		public bool WasSuccessfullyTested { get; set; }

		public DBHelper()
        {
			connectionStringBuilder = new();
			connectionStringBuilder.IntegratedSecurity = true;  // Let it be default
			WasSuccessfullyTested = false;
		}

		public string Server
        {
            get => connectionStringBuilder.DataSource;
            set
            {
                connectionStringBuilder.DataSource = value;
				DataBase = "master";	// На новом сервере может не быть текущей БД
				WasSuccessfullyTested = false;
			}
		}

		public AuthenticateTypes AuthenticateType
		{
			get => connectionStringBuilder.IntegratedSecurity ? AuthenticateTypes.Windows : AuthenticateTypes.SQL;
			set
			{
				if (value == AuthenticateTypes.Windows)
					connectionStringBuilder.IntegratedSecurity = true;
				else
					connectionStringBuilder.IntegratedSecurity = false;

				WasSuccessfullyTested = false;
			}
		}

		public string Login
        {
            get => login;
            set {
				login = value;
				WasSuccessfullyTested = false;
			}
        }

		public SecureString Password
        {
            get => password;
            set {
                password = value;
				password.MakeReadOnly();
				WasSuccessfullyTested = false;
			}
		}

		public string DataBase
        {
            get => connectionStringBuilder.InitialCatalog;
            set
            {
                connectionStringBuilder.InitialCatalog = value;
				WasSuccessfullyTested = false;
			}
		}

        private SqlConnection GetConnection()
        {
			SqlConnection con = new(connectionStringBuilder.ConnectionString);
			if (AuthenticateType != AuthenticateTypes.Windows)
				con.Credential = new(Login, Password);

            return con;
		}

		public void TestConnection()
		{
			using SqlConnection con = GetConnection();
			con.Open();
			WasSuccessfullyTested = true;
		}

		public int ExecuteQuery(string query, List<SqlParameter> paramList)
        {
			using SqlConnection con = GetConnection();
			using SqlCommand cmd = new(query, con);

			if (paramList != null)
				cmd.Parameters.AddRange(paramList.ToArray());

            cmd.Connection.Open();
            return cmd.ExecuteNonQuery();
		}

		public int ExecuteQuery(string query)
		{
			return ExecuteQuery(query, null);
		}

		public int ExecuteProcedure(string query, List<SqlParameter> paramList)
        {
			using SqlConnection con = GetConnection();
			using SqlCommand cmd = new(query, con) { CommandType = CommandType.StoredProcedure };

            if (paramList != null)
			    cmd.Parameters.AddRange(paramList.ToArray());

            cmd.Connection.Open();
            return cmd.ExecuteNonQuery();
		}

		public int ExecuteProcedure(string query)
		{
			return ExecuteProcedure(query, null);
		}

		public DataTable GetDataTable(string query, string tableName, List<SqlParameter> paramList)
        {
            DataTable result = new(tableName);

            using SqlConnection con = GetConnection();
			using SqlCommand cmd = new(query, con);

            if (paramList != null)
				cmd.Parameters.AddRange(paramList.ToArray());

			using SqlDataAdapter sda = new SqlDataAdapter(cmd);

            cmd.Connection.Open();
            sda.Fill(result);

            return result;
        }

		public DataTable GetDataTable(string query, string tableName)
		{
			return GetDataTable(query, tableName, null);
		}

		public DataTable GetDataTable(string query)
        {
            return GetDataTable(query, "Table");
        }

		public object GetScalar(string query, List<SqlParameter> paramList)
		{
			using SqlConnection con = GetConnection();
			using SqlCommand cmd = new(query, con);

			if (paramList != null)
				cmd.Parameters.AddRange(paramList.ToArray());

			cmd.Connection.Open();

			return cmd.ExecuteScalar();
		}

		public object GetScalar(string query)
		{
			return GetScalar(query, null);
		}

		public void UploadDataTable(DataTable data)
        {
            using SqlConnection con = GetConnection();
            SqlBulkCopy bulkcopy = new SqlBulkCopy(con);
            bulkcopy.DestinationTableName = data.TableName;
            con.Open();
            bulkcopy.WriteToServer(data);
        }

		/*
public async Task<SqlConnection> CreateConnectionAsync(bool Open = true)
{
   var connection = conFact.Create();
   if (Open)
	   await connection.OpenAsync().ConfigureAwait(false);
   return connection;
}

public async Task<DataTable> GetDataTableAsync(string query, string tableName, List<SqlParameter> paramList)
{
   return await Task.Run(() => { return GetDataTable(query, tableName, paramList); });
}
*/
	}
}
