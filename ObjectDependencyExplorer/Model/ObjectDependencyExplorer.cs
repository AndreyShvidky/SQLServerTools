using ObjectDependencyExplorer.ErrorHandlers;
using System.ComponentModel;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text;

namespace ObjectDependencyExplorer.Model
{
	public class SQLDependencyExplorer : INotifyPropertyChanged
	{
		private DBHelper _currentConnection;
		private readonly ILogger _logger;

		private readonly string _workingTableName = "SQL_DB_Module_Dependencies_" + Guid.NewGuid().ToString().Replace("-", "_");
		private List<string> _debugObjects;
		private const string _debugObjectsSeparator = ",";

		private double _runProgress;

		public event PropertyChangedEventHandler? PropertyChanged;

		private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public SQLDependencyExplorer(DBHelper currentConnection, ILogger logger)
		{
			_currentConnection = currentConnection;
			_logger = logger;
		}

		public string WorkingTableName { get => _workingTableName; }

		public string DebugObjects
		{
			get => _debugObjects != null ? string.Join(_debugObjectsSeparator, _debugObjects) : null;

			set
			{
				if (!string.IsNullOrEmpty(value))
					_debugObjects = value != null ? value.Split(_debugObjectsSeparator).ToList() : null;
				else
					_debugObjects = null;
			}
		}

		public double RunProgress
		{
			get => _runProgress;

			set
			{
				//На мелкие изменения можно не реагировать
				//if ((value - runProgress) < 1) return;
				_runProgress = value;
				NotifyPropertyChanged();
			}
		}

		// Main procedure for exploring Dependencies. Returns false in case of any errors
		public void ExploreDependencies(bool recreateOutputTable, bool traceOn)
		{
			SQLModule module;
			//FileStream fileOutput = null;
			DataTable dependencies;
			string whereExpression = string.Empty;
			int messageCount = 0;
			int warningCount = 0;
			int errorCount = 0;

			if (_debugObjects != null && _debugObjects.Count > 0)
				whereExpression = $@"
	and O.name in ({string.Join(_debugObjectsSeparator, _debugObjects.Select(it => $"'{it}'"))})";

			try
			{
				_logger?.LogInformation("Loading modules");

				DataTable procList = _currentConnection.GetDataTable($@"select
	SchemaName			= S.name
	,ObjectName			= O.name
	,ObjectDefinition	= T.definition
from
	sys.objects as O
	inner join sys.schemas as S on
		S.[schema_id] = O.[schema_id]
		and S.name not in ('cdc', 'test')
	left join sys.sql_modules as T on
		T.[object_id] = O.[object_id]
where
	O.is_ms_shipped = 0
	and O.type in ('P', 'FN', 'IF', 'TF', 'V'){whereExpression}
order by
	SchemaName
	,ObjectName");

				double progress = .0;
				//fileOutput = new FileStream(TextBoxFileName.Text, FileMode.Create);
				//string fileLine;
				DataRow fields;
				UnicodeEncoding uniencoding = new UnicodeEncoding();

				_currentConnection.ExecuteQuery(@$"declare @RecreateOutputTable bit = {(recreateOutputTable ? "1" : "0")}
if object_id('tempdb.dbo.{_workingTableName}') is not null
begin
	if @RecreateOutputTable = 1
		drop table tempdb.dbo.{_workingTableName}
	else
		delete from tempdb.dbo.{_workingTableName}
		where [DataBase] = '{_currentConnection.DataBase}'
end
else
	create table tempdb.dbo.{_workingTableName}
	(

		[DataBase]				sysname collate database_default		not null
		,SchemaName				sysname collate database_default		not null
		,ObjectName				sysname collate database_default		not null
		,DependencyType			tinyint									not null	-- 1 = Select, 2 = Insert, 3 = Update, 4 = Delete, 5 = Merge, 10 = Execute
		,ReferencedServer		sysname collate database_default		not null
		,ReferencedDataBase		sysname collate database_default		not null
		,ReferencedSchemaName	sysname collate database_default		not null
		,ReferencedObjectName	sysname collate database_default		not null
		,StartLine				int										not null
		,StartColumn			int										not null
		,ModuleOffset			int										not null
		,FragmentLength			int										not null
		,MessageType			tinyint										null
		,Message				varchar(max) collate database_default		null
		,primary key ([DataBase], SchemaName, [ObjectName], ModuleOffset)			-- Can't do for now, because of doubling from ""xml.query"" and next ""xml.query.value""
		--,primary key ([DataBase], SchemaName, [ObjectName], ModuleOffset, FragmentLength)
		--,primary key ([DataBase], SchemaName, [ObjectName], DependencyType, ReferencedServer, ReferencedDataBase, ReferencedSchemaName, ReferencedObjectName, ModuleOffset)	-- PK in case, when everithing is bad
		,check (DependencyType = (1) OR DependencyType = (2) OR DependencyType = (3) OR DependencyType = (4) OR DependencyType = (5) OR DependencyType = (10))
	)");

				dependencies = _currentConnection.GetDataTable(@$"select
	T.[DataBase]
	,T.SchemaName
	,T.ObjectName
	,T.DependencyType
	,T.ReferencedServer
	,T.ReferencedDataBase
	,T.ReferencedSchemaName
	,T.ReferencedObjectName
	,T.StartLine
	,T.StartColumn
	,T.ModuleOffset
	,T.FragmentLength
	,T.MessageType
	,T.Message
from
	tempdb.dbo.{_workingTableName} as T
where
	T.[DataBase] = '{_currentConnection.DataBase}'");
				dependencies.TableName = $"tempdb.dbo.{_workingTableName}";  // Для BulkLoad

				foreach (DataRow proc in procList.Rows)
				{
					progress += 1;

					_logger?.LogInformation($"Processing {proc["SchemaName"]}.{proc["ObjectName"]}");
					//logger?.ShowInformation($"Processing {proc["SchemaName"]}.{proc["ObjectName"]}");
					//System.Threading.Thread.Sleep(20);
					module = new SQLModule(proc["SchemaName"].ToString(), proc["ObjectName"].ToString(), proc["ObjectDefinition"].ToString());
					if (traceOn)
						_logger?.LogInformation(proc["ObjectDefinition"].ToString());
					module.Parse(traceOn ? _logger : null);  //, OnlyFirst);

					// Эмулятор ошибки
					//if (proc["ObjectName"].ToString() == "spk_SaveBorderCrossing")
					//	throw new Exception("Случилось страшное!!!!");

					// Errors. To output table and to log
					foreach (ParseMessage err in module.Messages)
					{
						switch (err.Type)
						{
							case ParseMessage.MessageType.Error:
								_logger?.LogException(err.Message);
								errorCount++;
								break;
							case ParseMessage.MessageType.Warning:
								_logger?.LogWarning(err.Message);
								warningCount++;
								break;
							default:
								_logger?.LogInformation(err.Message);
								messageCount++;
								break;
						}

						fields = dependencies.NewRow();
						fields["DataBase"] = _currentConnection.DataBase;
						fields["SchemaName"] = module.Schema;
						fields["ObjectName"] = module.Name;
						fields["DependencyType"] = SQLStatement.SQLStatementType.Unknown;
						fields["ReferencedServer"] = string.Empty;
						fields["ReferencedDataBase"] = string.Empty;
						fields["ReferencedSchemaName"] = string.Empty;
						fields["ReferencedObjectName"] = string.Empty;
						fields["StartLine"] = err.Line;
						fields["StartColumn"] = err.Column;
						fields["ModuleOffset"] = err.FragmentOffset;
						fields["FragmentLength"] = err.FragmentLength;
						fields["MessageType"] = (byte)err.Type;
						fields["Message"] = err.Message.IndexOf("\n") > 0 ? err.Message.Substring(0, err.Message.IndexOf("\n")) : err.Message;
						dependencies.Rows.Add(fields);
					}

					// Dependencies. To output table
					foreach (SQLObjectReference dependency in module.Dependencies.OrderBy(it => it.SQLFragment.StartOffset))
					{
						//fileLine = $"\t\t,('{db.DataBase}', '{module.Schema}', '{module.Name}', {(int)dependency.Type}, '{dependency.DBObject.Server ?? string.Empty}', '{dependency.DBObject.DataBase ?? string.Empty}', '{dependency.DBObject.Schema ?? string.Empty}', '{dependency.DBObject.Name ?? string.Empty}', {dependency.StartLine}, {dependency.StartColumn}, {dependency.ModuleOffset})\n";
						//result = uniencoding.GetBytes(fileLine);
						//fileOutput.Write(result, 0, result.Length);
						fields = dependencies.NewRow();
						fields["DataBase"] = _currentConnection.DataBase;
						fields["SchemaName"] = module.Schema;
						fields["ObjectName"] = module.Name;
						fields["DependencyType"] = dependency.ReferenceType;
						fields["ReferencedServer"] = dependency.Server ?? string.Empty;
						fields["ReferencedDataBase"] = dependency.DataBase ?? string.Empty;
						fields["ReferencedSchemaName"] = dependency.Schema ?? string.Empty;
						fields["ReferencedObjectName"] = dependency.Name ?? string.Empty;
						fields["StartLine"] = dependency.SQLFragment.StartLine;
						fields["StartColumn"] = dependency.SQLFragment.StartColumn;
						fields["ModuleOffset"] = dependency.SQLFragment.StartOffset;
						fields["FragmentLength"] = dependency.SQLFragment.FragmentLength;
						dependencies.Rows.Add(fields);
					}

					RunProgress = progress / procList.Rows.Count * 100;
					//Application.Current.Dispatcher.Invoke(() => RunProgress = progress / procList.Rows.Count * 100);

					//if (BreakOnError && module.Messages.Count > 0)
					//	break;
				}

				//fileLine = "\t) as V ([DataBase], SchemaName, ObjectName, DependencyType, ReferencedServer, ReferencedDataBase, ReferencedSchemaName, ReferencedObjectName, StartLine, StartColumn, ModuleOffset, FragmentLength)";
				//result = uniencoding.GetBytes(fileLine);
				//fileOutput.Write(result, 0, result.Length);

				_currentConnection.UploadDataTable(dependencies);
			}
			catch (WarningException ex)
			{
				_logger?.LogWarning(ex);
			}
			catch (Exception ex)
			{
				_logger?.LogException(ex);
			}
			finally
			{
				//if (fileOutput != null)
				//	fileOutput.Close();

				_logger?.LogInformation("Exploring finished.");
				if (errorCount > 0)
					_logger?.LogException("There were errors during processing, see above.");
				else if (warningCount > 0)
					_logger?.LogException("There were warnings during processing, see above.");
				else if (messageCount > 0)
					_logger?.LogInformation("There were messages during processing, see above.");

				_logger?.LogInformation($"Output data saved to SQL table tempdb.dbo.{_workingTableName}");
			}
		}
	}
}
