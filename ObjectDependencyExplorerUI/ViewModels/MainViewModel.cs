using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Threading.Tasks;
using DBConnectDialog;
using ObjectDependencyExplorer.ErrorHandlers;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows.Documents;
using System.Globalization;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using ObjectDependencyExplorer;
using ObjectDependencyExplorer.Model;

namespace ObjectDependencyExplorerUI
{
	internal class MainViewModel : INotifyPropertyChanged
	{
		private DBHelper currentConnection;
		private SQLDependencyExplorer sqlDependencyExplorer;
		private bool _isBusy;

		private const string _objectTableName = "Object";
		private const string _referencedObjectTableName = "ReferencedObject";
		private const string _objectDependency = "Dependency";
		private const string _referencedObjectDependency = "ReferencedDependency";

		//private bool breakOnError = true;
		private bool traceOn = false;
		//private bool onlyFirst = false;
		private int selectedTabIndex;
		private readonly SimpleLogger logger;
		private object loggerLock;

		private DataView _resultDataView;
		private DataView _resultReferencedDataView;
		private DataRowView _selectedDataRowView;
		private DataRowView _selectedReferencedDataRowView;
		private SQLObject _selectedObject;
		private SQLObject _selectedObjectRevert;
		private SQLSyntheticDependency _selectedDependency;
		private SQLSyntheticDependency _selectedDependencyRevert;
		private FlowDocument _currentObjectDefinition;
		private FlowDocument _currentObjectDefinitionRevert; 
		private TextRange highLightedTextRange;
		private TextRange referencedHighLightedTextRange;

		public SimpleCommand TryConnectServer { get; }
		public SimpleCommand RunCommand { get; }

		public MainViewModel()
		{
			//GetConnectionStrings();

			logger = new();
			//To let the collection interact with UI
			loggerLock = new();
			BindingOperations.EnableCollectionSynchronization(logger.Items, loggerLock);

			TryConnectServer = new SimpleCommand(obj =>
			{
				try
				{
					// DB connection
					if (currentConnection == null)
						currentConnection = new();

					// Singleton to have ability to gather multiple results in a single output table
					if (sqlDependencyExplorer == null)
					{
						sqlDependencyExplorer = new(currentConnection, logger);
						sqlDependencyExplorer.PropertyChanged += SqlDependencyExplorer_PropertyChanged;
					}

					// Creating Model
					// Model has empty constructor because we want to have Design Time Model. Eternal war View or ViewMode first. All magic in ViewModel.Initalize
					DBConnectViewModel viewModel = new();
					viewModel.Initalize(currentConnection, new ErrorHandlerService());

					// Creating View
					DBConnectWindow dbConnectWindow = new(viewModel);
					dbConnectWindow.Owner = Application.Current.MainWindow;

					// Showtime
					if (dbConnectWindow.ShowDialog() == false) return;
					RunCommand.RaiseCanExecuteChanged();
					NotifyPropertyChanged(nameof(CurrentConnection));
					NotifyPropertyChanged(nameof(DataBaseList));
					NotifyPropertyChanged(nameof(IsDatabaseSelectionAvailable)); 
				}
				catch (Exception ex)
				{
					string msg = $"Failed to connect. {ex.Message}";
					if (ex.InnerException != null) msg += $"\n{ex.InnerException.Message}";
					MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
					return;
				}
			}, obj => true);

			RunCommand = new SimpleCommand(obj =>
			{
				// Clearing all previouse results
				SelectedObject = null;
				SelectedDependency = null;
				CurrentObjectDefinition = null;
				highLightedTextRange = null;

				try
				{
					SelectedTabIndex = 0;
					IsBusy = true;
					logger?.Clear();

					Task task = Task.Run(() => sqlDependencyExplorer.ExploreDependencies(false, TraceOn));
					//task.ContinueWith(t => Application.Current.Dispatcher.Invoke(() => SelectedTabIndex = 1));
					task.ContinueWith(t => GetResults(), TaskScheduler.FromCurrentSynchronizationContext());
				}
				catch (Exception ex)
				{
					IsBusy = false;
					string msg = $"Error while running exploring task. {ex.Message}";
					if (ex.InnerException != null) msg += $"\n{ex.InnerException.Message}";
					MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}
			}, obj =>
			{
				if (currentConnection != null && currentConnection.WasSuccessfullyTested)
					return true;

				return false;
			});
		}

		private void SqlDependencyExplorer_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(sqlDependencyExplorer.RunProgress):
					NotifyPropertyChanged(nameof(RunProgress));
					break;
				default:
					break;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public bool IsBusy
		{
			get => _isBusy;
			set
			{
				_isBusy = value;
				NotifyPropertyChanged();
			}
		}

		//private void GetConnectionStrings()
		//{
		//	try
		//	{
		//		// Преднастроенные строки подключения из конфигурационного файла
		//		SqlConnectionStringBuilder cs;
		//		foreach (ConnectionStringSettings css in ConfigurationManager.ConnectionStrings)
		//		{
		//			cs = new SqlConnectionStringBuilder(css.ConnectionString);
		//			cs.ApplicationName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
		//			connectionStrings.Add(css.Name, cs);
		//		}

		//		// Пользовательская строка подключения
		//		connectionStrings.Add(UserDefinedConnectionString, null);
		//	}
		//	catch (Exception ex)
		//	{
		//		string msg = $"Failed to get connection strings (servers) from configuration file. {ex.Message}";
		//		if (ex.InnerException != null) msg += $"\n{ex.InnerException.Message}";
		//		MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		//		return;
		//	}
		//}

		private void ChangeDatabase(string db)
		{
			if (currentConnection == null)
				return;
			else
			{
				if (db != null)
					currentConnection.DataBase = db;
				else
					currentConnection.DataBase = "master";
			}
			currentConnection.TestConnection();
			RunCommand.RaiseCanExecuteChanged();
			NotifyPropertyChanged(nameof(CurrentConnection));
		}

		private List<string> GetDataBaseList()
		{
			if (currentConnection == null)
				return null;

			List<string> result = new List<string>();

			try
			{
				DataTable dbList = currentConnection.GetDataTable($@"select
	[name]
from
	sys.databases
where
	is_read_only = 0
	and name not in ('master', 'tempdb', 'model', 'msdb', 'ReportServer', 'ReportServerTempDB')
order by
	[name]");

				foreach (DataRow db in dbList.Rows)
				{
					result.Add(db["name"].ToString());
				}

				return result;
			}
			catch (Exception ex)
			{
				string msg = $"Failed to get database list from the server. {ex.Message}";
				if (ex.InnerException != null) msg += $"\n{ex.InnerException.Message}";
				MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return null;
			}
		}

		public string CurrentConnection
		{
			get
			{
				if (currentConnection == null)
					return "Select Server";

				return currentConnection.Server;
			}
		}

		public bool IsDatabaseSelectionAvailable => currentConnection?.Server != null;

		public List<string> DataBaseList
		{
			get => GetDataBaseList();
		}

		public string SelectedDataBase
		{
			get => currentConnection?.DataBase;
			set => ChangeDatabase(value);
		}

		public string DebugObjects
		{
			get => sqlDependencyExplorer?.DebugObjects;

			set => sqlDependencyExplorer.DebugObjects = value;
		}

		public double RunProgress
		{
			get => sqlDependencyExplorer != null ? sqlDependencyExplorer.RunProgress : 0;
		}

		//public bool BreakOnError
		//{
		//	get => breakOnError;

		//	set
		//	{
		//		breakOnError = value;
		//		NotifyPropertyChanged();
		//	}
		//}

		public bool TraceOn
		{
			get => traceOn;

			set
			{
				traceOn = value;
				NotifyPropertyChanged();
			}
		}

		//public bool OnlyFirst
		//{
		//	get => onlyFirst;

		//	set
		//	{
		//		onlyFirst = value;
		//		NotifyPropertyChanged();
		//	}
		//}

		//public FlowDocument ExecutionOutput => logger?.ErrorPresenter;
		public ObservableCollection<SimpleLogItem> LogOutput => logger.Items;

		public int SelectedTabIndex
		{
			get => selectedTabIndex;
			set {
				selectedTabIndex = value;
				NotifyPropertyChanged();
			}
		}

		public DataView ResultDatabases
		{
			get => _resultDataView;
			private set
			{
				_resultDataView = value;
				NotifyPropertyChanged();
			}
		}

		public DataView ResultReferencedDatabases
		{
			get => _resultReferencedDataView;
			private set
			{
				_resultReferencedDataView = value;
				NotifyPropertyChanged();
			}
		}

		public DataRowView SelectedDataRowView
		{
			get => _selectedDataRowView;
			set
			{
				_selectedDataRowView = value;
				NotifyPropertyChanged();
				DetectSelectedContext(_selectedDataRowView);
			}
		}

		public DataRowView SelectedReferencedDataRowView
		{
			get => _selectedReferencedDataRowView;
			set
			{
				_selectedReferencedDataRowView = value;
				NotifyPropertyChanged();
				DetectReferencedSelectedContext(_selectedReferencedDataRowView);
			}
		}

		public SQLObject SelectedObject
		{
			get => _selectedObject;
			set
			{
				_selectedObject = value;
				NotifyPropertyChanged();
				if (_selectedObject != null)
				{
					FlowDocument doc = new() { FontFamily = SystemFonts.MessageFontFamily };
					Paragraph paragraph = new(new Run(SelectedObject.Definition));
					doc.Blocks.Add(paragraph);
					CurrentObjectDefinition = doc;
				}
			}
		}

		public SQLObject SelectedObjectRevert
		{
			get => _selectedObjectRevert;
			set
			{
				_selectedObjectRevert = value;
				NotifyPropertyChanged();
				if (_selectedObjectRevert != null)
				{
					FlowDocument doc = new() { FontFamily = SystemFonts.MessageFontFamily };
					Paragraph paragraph = new(new Run(SelectedObjectRevert.Definition));
					doc.Blocks.Add(paragraph);
					CurrentObjectDefinitionRevert = doc;
				}
			}
		}

		public SQLSyntheticDependency SelectedDependency
		{
			get => _selectedDependency;
			set
			{
				_selectedDependency = value;
				NotifyPropertyChanged();
			}
		}

		public SQLSyntheticDependency SelectedDependencyRevert
		{
			get => _selectedDependencyRevert;
			set
			{
				_selectedDependencyRevert = value;
				NotifyPropertyChanged();
			}
		}

		public FlowDocument CurrentObjectDefinition
		{
			get => _currentObjectDefinition;
			set
			{
				_currentObjectDefinition = value;
				NotifyPropertyChanged();
			}
		}

		public FlowDocument CurrentObjectDefinitionRevert
		{
			get => _currentObjectDefinitionRevert;
			set
			{
				_currentObjectDefinitionRevert = value;
				NotifyPropertyChanged();
			}
		}

		// Rusults from table
		private void GetResults()
		{
			DataSet dsResult = new DataSet("Database");

			// Detect Database collation
			bool dbCaseSensitive = false;

			int checkCollationResult = (int)currentConnection.GetScalar(@"select
	IsCaseSensitive = case when DB.collation_name like '%[_]CS[_]%' then 1 else 0 end
	--,DB.collation_name
from
	sys.databases as DB
where
	DB.database_id = db_id()");

			dbCaseSensitive = checkCollationResult > 0;

			dsResult.CaseSensitive = dbCaseSensitive;

			DataTable tblDatabases = currentConnection.GetDataTable(@$"select distinct
	T.[DataBase]
from
	tempdb.dbo.{sqlDependencyExplorer.WorkingTableName} as T
order by
	T.[DataBase]");
			tblDatabases.TableName = "DataBase";
			tblDatabases.PrimaryKey = new DataColumn[] { tblDatabases.Columns["DataBase"] };
			dsResult.Tables.Add(tblDatabases);

			DataTable tblReferencedDatabases = currentConnection.GetDataTable(@$"select distinct
	ReferencedDataBase = case T.ReferencedDataBase when '' then db_name() else T.ReferencedDataBase end
from
	tempdb.dbo.{sqlDependencyExplorer.WorkingTableName} as T
order by
	ReferencedDataBase");
			tblReferencedDatabases.TableName = "ReferencedDataBase";
			tblReferencedDatabases.PrimaryKey = new DataColumn[] { tblReferencedDatabases.Columns["ReferencedDataBase"] };
			dsResult.Tables.Add(tblReferencedDatabases);

			DataTable tblSchemas = currentConnection.GetDataTable(@$"select distinct
	T.[DataBase]
	,T.SchemaName
from
	tempdb.dbo.{sqlDependencyExplorer.WorkingTableName} as T
order by
	T.[DataBase]
	,T.SchemaName");
			tblSchemas.TableName = "Schema";
			tblSchemas.PrimaryKey = new DataColumn[] { tblSchemas.Columns["DataBase"], tblSchemas.Columns["SchemaName"] };
			dsResult.Tables.Add(tblSchemas);
			dsResult.Relations.Add("DatabaseSchemas", tblDatabases.Columns["DataBase"], tblSchemas.Columns["DataBase"], false);

			DataTable tblReferencedSchemas = currentConnection.GetDataTable(@$"select distinct
	ReferencedDataBase = case T.ReferencedDataBase when '' then db_name() else T.ReferencedDataBase end
	,ReferencedSchemaName = case T.ReferencedSchemaName when '' then '_NoSchema' else T.ReferencedSchemaName end
from
	tempdb.dbo.{sqlDependencyExplorer.WorkingTableName} as T
order by
	ReferencedDataBase
	,ReferencedSchemaName");
			tblReferencedSchemas.TableName = "ReferencedSchema";
			tblReferencedSchemas.PrimaryKey = new DataColumn[] { tblReferencedSchemas.Columns["ReferencedDataBase"], tblReferencedSchemas.Columns["ReferencedSchemaName"] };
			dsResult.Tables.Add(tblReferencedSchemas);
			dsResult.Relations.Add("ReferencedDatabaseSchemas", tblReferencedDatabases.Columns["ReferencedDataBase"], tblReferencedSchemas.Columns["ReferencedDataBase"], false);

			DataTable tblObjects = currentConnection.GetDataTable(@$"select distinct
	T.[DataBase]
	,T.SchemaName
	,T.ObjectName
from
	tempdb.dbo.{sqlDependencyExplorer.WorkingTableName} as T
order by
	T.[DataBase]
	,T.SchemaName
	,T.ObjectName");
			tblObjects.TableName = _objectTableName;
			tblObjects.CaseSensitive = dbCaseSensitive;
			tblObjects.PrimaryKey = new DataColumn[] { tblObjects.Columns["DataBase"], tblObjects.Columns["SchemaName"], tblObjects.Columns["ObjectName"] };
			dsResult.Tables.Add(tblObjects);
			dsResult.Relations.Add("SchemaObjects", new DataColumn[] { tblSchemas.Columns["DataBase"], tblSchemas.Columns["SchemaName"] }, new DataColumn[] { tblObjects.Columns["DataBase"], tblObjects.Columns["SchemaName"] }, false);

			DataTable tblReferencedObjects = currentConnection.GetDataTable(@$"select distinct
	ReferencedServer = case T.ReferencedServer when '' then db_name() else T.ReferencedServer end
	,ReferencedDataBase = case T.ReferencedDataBase when '' then db_name() else T.ReferencedDataBase end
	,ReferencedSchemaName = case T.ReferencedSchemaName when '' then '_NoSchema' else T.ReferencedSchemaName end
	,T.ReferencedObjectName
from
	tempdb.dbo.{sqlDependencyExplorer.WorkingTableName} as T
order by
	ReferencedServer
	,ReferencedDataBase
	,ReferencedSchemaName
	,T.ReferencedObjectName");
			tblReferencedObjects.TableName = _referencedObjectTableName;
			tblReferencedObjects.CaseSensitive = dbCaseSensitive;
			tblReferencedObjects.PrimaryKey = new DataColumn[] { tblReferencedObjects.Columns["DataBase"], tblReferencedObjects.Columns["SchemaName"], tblReferencedObjects.Columns["ObjectName"] };
			dsResult.Tables.Add(tblReferencedObjects);
			dsResult.Relations.Add("ReferencedSchemaObjects", new DataColumn[] { tblReferencedSchemas.Columns["ReferencedDataBase"], tblReferencedSchemas.Columns["ReferencedSchemaName"] }, new DataColumn[] { tblReferencedObjects.Columns["ReferencedDataBase"], tblReferencedObjects.Columns["ReferencedSchemaName"] }, false);

			DataTable tblDependencies = currentConnection.GetDataTable(@$"select
	T.[DataBase]
	,T.SchemaName
	,T.ObjectName
	,T.DependencyType
	,T.ReferencedServer
	,T.ReferencedDataBase
	,T.ReferencedSchemaName
	,T.ReferencedObjectName
	,T.ModuleOffset
	,T.FragmentLength
	,T.MessageType
	,NodeName = case
					when T.MessageType is not null then isnull(T.Message, 'null')
					else
						case T.DependencyType
							when 1	then 'select'	-- 1 = Select
							when 2	then 'insert'	-- 2 = Insert
							when 3	then 'update'	-- 3 = Update
							when 4	then 'delete'	-- 4 = Delete
							when 5	then 'merge'	-- 5 = Merge
							when 10	then 'execute '	-- 10 = Execute
						end
						+ ' '
						+ case
							when T.ReferencedServer != '' then T.ReferencedServer + '.'
							else ''
						end
						+ case
							when T.ReferencedDataBase != '' then T.ReferencedDataBase + '.'
							else ''
						end
						+ T.ReferencedSchemaName + '.'
						+ T.ReferencedObjectName
				end
from
	tempdb.dbo.{sqlDependencyExplorer.WorkingTableName} as T
order by
	T.[DataBase]
	,T.SchemaName
	,T.ObjectName
	,T.ModuleOffset");
			tblDependencies.TableName = _objectDependency;
			//tblDependencies.PrimaryKey = new DataColumn[] { tblDependencies.Columns["DataBase"], tblDependencies.Columns["SchemaName"], tblDependencies.Columns["ObjectName"] };
			dsResult.Tables.Add(tblDependencies);
			dsResult.Relations.Add("Dependencies", new DataColumn[] { tblObjects.Columns["DataBase"], tblObjects.Columns["SchemaName"], tblObjects.Columns["ObjectName"] }, new DataColumn[] { tblDependencies.Columns["DataBase"], tblDependencies.Columns["SchemaName"], tblDependencies.Columns["ObjectName"] }, false);

			DataTable tblReferencedDependencies = currentConnection.GetDataTable(@$"select
	T.[DataBase]
	,T.SchemaName
	,T.ObjectName
	,T.DependencyType
	,T.ReferencedServer
	,ReferencedDataBase = case T.ReferencedDataBase when '' then db_name() else T.ReferencedDataBase end
	,ReferencedSchemaName = case T.ReferencedSchemaName when '' then '_NoSchema' else T.ReferencedSchemaName end
	,T.ReferencedObjectName
	,T.ModuleOffset
	,T.FragmentLength
	,T.MessageType
	,NodeName = case
					when T.MessageType is not null then isnull(T.Message, 'null')
					else
						case T.DependencyType
							when 1	then 'select'	-- 1 = Select
							when 2	then 'insert'	-- 2 = Insert
							when 3	then 'update'	-- 3 = Update
							when 4	then 'delete'	-- 4 = Delete
							when 5	then 'merge'	-- 5 = Merge
							when 10	then 'execute '	-- 10 = Execute
						end
						+ ' from '
						+ T.[DataBase] + '.'
						+ T.SchemaName + '.'
						+ T.ObjectName
				end
from
	tempdb.dbo.{sqlDependencyExplorer.WorkingTableName} as T
order by
	T.[DataBase]
	,T.SchemaName
	,T.ObjectName
	,T.ModuleOffset");
			tblReferencedDependencies.TableName = _referencedObjectDependency;
			//tblReferencedDependencies.PrimaryKey = new DataColumn[] { tblReferencedDependencies.Columns["ReferencedDataBase"], tblReferencedDependencies.Columns["ReferencedSchemaName"], tblReferencedDependencies.Columns["ReferencedObjectName"] };
			dsResult.Tables.Add(tblReferencedDependencies);
			dsResult.Relations.Add("ReferencedDependencies", new DataColumn[] { tblReferencedObjects.Columns["ReferencedDataBase"], tblReferencedObjects.Columns["ReferencedSchemaName"], tblReferencedObjects.Columns["ReferencedObjectName"] }, new DataColumn[] { tblReferencedDependencies.Columns["ReferencedDataBase"], tblReferencedDependencies.Columns["ReferencedSchemaName"], tblReferencedDependencies.Columns["ReferencedObjectName"] }, false);

			ResultDatabases = new(tblDatabases);
			//ResultDatabases.RowFilter = "[Father]<0";
			//tblObjects.DefaultView.RowFilter = "[Father] < 0";
			ResultReferencedDatabases = new(tblReferencedDatabases);

			// If there are no Erros - switch to TreeView
			if (!tblDependencies.AsEnumerable().Any(row => row.Field<byte?>("MessageType") != null))
				SelectedTabIndex = 1;

			IsBusy = false;
		}

		private void DetectSelectedContext(DataRowView selectedDataRowView)
		{
			if (selectedDataRowView == null)
				return;

			SQLObject clickedObject;
			SQLObject clickedReferencedObject;
			SQLSyntheticDependency clickedDependency;

			try
			{
				switch (selectedDataRowView.Row.Table.TableName)
				{
					case _objectTableName:
						clickedObject = new(string.Empty, string.Empty, selectedDataRowView.Row["SchemaName"].ToString(), selectedDataRowView.Row["ObjectName"].ToString(), string.Empty, string.Empty);
						clickedReferencedObject = null;
						clickedDependency = null;
						break;
					case _objectDependency:
						clickedObject = new(string.Empty, string.Empty, selectedDataRowView.Row["SchemaName"].ToString(), selectedDataRowView.Row["ObjectName"].ToString(), string.Empty, string.Empty);
						clickedReferencedObject = new(selectedDataRowView.Row["ReferencedServer"].ToString(), selectedDataRowView.Row["ReferencedDataBase"].ToString(), selectedDataRowView.Row["ReferencedSchemaName"].ToString(), selectedDataRowView.Row["ReferencedObjectName"].ToString(), string.Empty, string.Empty);
						clickedDependency = new(clickedReferencedObject, (SQLStatement.SQLStatementType)(byte)selectedDataRowView.Row["DependencyType"], 0, 0, (int)selectedDataRowView.Row["ModuleOffset"], (int)selectedDataRowView.Row["FragmentLength"]);
						break;
					default:
						clickedObject = null;
						clickedDependency = null;
						highLightedTextRange = null;
						break;
				}

				if (SelectedObject?.FullName != clickedObject?.FullName)
				{
					if (clickedObject != null && string.IsNullOrEmpty(clickedObject.Definition))
					{
						clickedObject.Definition = currentConnection.GetScalar($"select ObjectDefinition = object_definition(object_id('{clickedObject.FullName}'))") as string;
						highLightedTextRange = null;
					}

					SelectedObject = clickedObject;
				}

				if (SelectedDependency?.DBObject.FullName != clickedDependency?.DBObject.FullName || SelectedDependency?.ModuleOffset != clickedDependency?.ModuleOffset)
				{
					SelectedDependency = clickedDependency;
					HighLightDependency(ref highLightedTextRange, CurrentObjectDefinition, SelectedDependency);
					NotifyPropertyChanged(nameof(CurrentObjectDefinition));
				}
			}
			catch (Exception ex)
			{
				string msg = $"Failed to detect current object and/or object it references. {ex.Message}";
				if (ex.InnerException != null) msg += $"\n{ex.InnerException.Message}";
				MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
		}

		private void DetectReferencedSelectedContext(DataRowView selectedDataRowView)
		{
			if (selectedDataRowView == null)
				return;

			SQLObject clickedObject;
			SQLObject clickedReferencedObject;
			SQLSyntheticDependency clickedDependency;

			try
			{
				switch (selectedDataRowView.Row.Table.TableName)
				{
					case _referencedObjectTableName:
						clickedObject = null;
						clickedReferencedObject = new(selectedDataRowView.Row["ReferencedServer"].ToString(), selectedDataRowView.Row["ReferencedDataBase"].ToString(), selectedDataRowView.Row["ReferencedSchemaName"].ToString(), selectedDataRowView.Row["ReferencedObjectName"].ToString(), string.Empty, string.Empty);
						clickedDependency = null;
						break;
					case _referencedObjectDependency:
						clickedObject = new(string.Empty, string.Empty, selectedDataRowView.Row["SchemaName"].ToString(), selectedDataRowView.Row["ObjectName"].ToString(), string.Empty, string.Empty);
						clickedReferencedObject = new(selectedDataRowView.Row["ReferencedServer"].ToString(), selectedDataRowView.Row["ReferencedDataBase"].ToString(), selectedDataRowView.Row["ReferencedSchemaName"].ToString(), selectedDataRowView.Row["ReferencedObjectName"].ToString(), string.Empty, string.Empty);
						clickedDependency = new(clickedReferencedObject, (SQLStatement.SQLStatementType)(byte)selectedDataRowView.Row["DependencyType"], 0, 0, (int)selectedDataRowView.Row["ModuleOffset"], (int)selectedDataRowView.Row["FragmentLength"]);
						break;
					default:
						clickedObject = null;
						clickedDependency = null;
						referencedHighLightedTextRange = null;
						break;
				}

				if (SelectedObjectRevert?.FullName != clickedObject?.FullName)
				{
					if (clickedObject != null && string.IsNullOrEmpty(clickedObject.Definition))
					{
						clickedObject.Definition = currentConnection.GetScalar($"select ObjectDefinition = object_definition(object_id('{clickedObject.FullName}'))") as string;
						referencedHighLightedTextRange = null;
					}

					SelectedObjectRevert = clickedObject;
				}

				if (SelectedDependencyRevert?.DBObject.FullName != clickedDependency?.DBObject.FullName || SelectedDependencyRevert?.ModuleOffset != clickedDependency?.ModuleOffset)
				{
					SelectedDependencyRevert = clickedDependency;
					HighLightDependency(ref referencedHighLightedTextRange, CurrentObjectDefinitionRevert, SelectedDependencyRevert);
					NotifyPropertyChanged(nameof(CurrentObjectDefinitionRevert));
				}
			}
			catch (Exception ex)
			{
				string msg = $"Failed to detect current object and/or object it references. {ex.Message}";
				if (ex.InnerException != null) msg += $"\n{ex.InnerException.Message}";
				MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
		}

		private void HighLightDependency(ref TextRange currentHighLight, FlowDocument currentObjectDefinition, SQLSyntheticDependency currentDependency)
		{
			try
			{
				// First - return previously highlighted text to normal
				if (currentHighLight != null)
				{
					currentHighLight.ApplyPropertyValue(TextElement.BackgroundProperty, null);
				}

				// Return if nothing to highlighted
				if (currentDependency == null)
					return;

				// Highlighted new fragment
				TextPointer start = currentObjectDefinition.ContentStart;
				start = start.GetPositionAtOffset(currentDependency.ModuleOffset + 2); // Experimetally. Don't know why + 2
				TextPointer end = start.GetPositionAtOffset(currentDependency.FragmentLength);
				currentHighLight = new TextRange(start, end);
				currentHighLight.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Yellow);
				
				// BringIntoView doesn't work without DoEvents()
				Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(delegate { }));
				(currentHighLight.Start.Parent as FrameworkContentElement)?.BringIntoView();
			}
			catch (Exception ex)
			{
				string msg = $"Failed to highlight dependency. {ex.Message}";
				if (ex.InnerException != null) msg += $"\n{ex.InnerException.Message}";
				MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
		}

		public void Dispose()
		{
			// Подчищаем за собой
			try
			{
				if (currentConnection != null && currentConnection.WasSuccessfullyTested)
				{
					currentConnection.ExecuteQuery(@$"if object_id('tempdb.dbo.{sqlDependencyExplorer.WorkingTableName}') is not null
	drop table tempdb.dbo.{sqlDependencyExplorer.WorkingTableName}");
				}
			}
			catch { }
		}
	}

	public class LogItemToBrushConverter : IValueConverter
	{
		readonly SolidColorBrush warningBrush = Brushes.Orange;
		readonly SolidColorBrush errorBrush = Brushes.Red;

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null || value == DBNull.Value)
				return null;

			switch ((MessageType)value)
			{
				case MessageType.Warning:
					return warningBrush;
				case MessageType.Error:
					return errorBrush;
				default:
					return System.Drawing.SystemBrushes.WindowText;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return new NotImplementedException();
		}
	}
	
	public class MessageTypeToIconConverter : IValueConverter
	{
		readonly BitmapImage warningImage = new(new Uri("/Images/warning.png", UriKind.Relative));
		readonly BitmapImage errorImage = new(new Uri("/Images/error.png", UriKind.Relative));

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null || value == DBNull.Value)
				return null;

			switch ((ParseMessage.MessageType)(byte)value)
			{
				case ParseMessage.MessageType.Warning:
					return warningImage;
				case ParseMessage.MessageType.Error:
					return errorImage;
				default:
					return null;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return new NotImplementedException();
		}
	}
}
