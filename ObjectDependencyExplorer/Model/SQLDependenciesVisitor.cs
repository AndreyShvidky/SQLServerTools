using Microsoft.SqlServer.TransactSql.ScriptDom;
using ObjectDependencyExplorer.ErrorHandlers;
using System;

namespace ObjectDependencyExplorer
{
	// Our main Visitor
	// Our intent - is to get information about where and what tables (or may be views) are modified or accessed for read
	// So methods for visiting are choosed not all, but only those which serve that intent
	public class SQLDependenciesVisitor : TSqlFragmentVisitor
	//public class SQLDependenciesVisitor : TSqlConcreteFragmentVisitor
	{
		//private IEnumerable<SPParam> parameters;
		private SQLModule currentModule;
		private SQLStatement currentStatement;
		//private List<TSqlFragment> prevFragments = new List<TSqlFragment>();
		private readonly ILogger logger; // Leave logger just like a mark to collect RestoreStatement
		private delegate string RestoreStatementMethod(TSqlFragment element);
		public static Func<TSqlFragment, string> RestoreStatement = (element) => { return string.Empty; };
		//private bool onlyFirst;

		public SQLDependenciesVisitor(SQLModule pSQLModule, ILogger pLogger)    //, bool pOnlyFirst)
		{
			currentModule = pSQLModule;
			logger = pLogger;
			//onlyFirst = pOnlyFirst;

			if (logger != null)
				RestoreStatement = GenerateStatement;
		}

		// Debug. For a totally bad case. Visits absoluteley all fragments
		//public override void Visit(TSqlFragment element)
		//{
		//    prevFragments.Add(element);
		//}

		#region Collecting top level expressions might be interesting: changing data, module execution, etc

		// TSqlFragment -> TSqlStatement
		// Methods in this region create currentStatement = Current top level statement
		// Had to use Visit, because it only who catches calls from particular object up to TSqlStatement through all inheritors. So only here there is an oportunity to catch any expression
		public override void Visit(TSqlStatement element)
		{
			// But those we cought in ExplicitVisit might be excluded, because we already processed em, to avoid duplicate dependencies
			if (
				element is InsertStatement
				|| element is UpdateStatement
				|| element is MergeStatement
				|| element is DeleteStatement
				|| element is ExecuteStatement
				)
				return;

			currentStatement = new SQLStatement(SQLStatement.SQLStatementType.Unknown, element);
		}

		// TSqlFragment -> TSqlStatement -> StatementWithCtesAndXmlNamespaces -> SelectStatement
		public override void ExplicitVisit(InsertStatement element)
		{
			currentStatement = currentModule.AddStatementWithTarget(element.InsertSpecification.Target as dynamic, element, SQLStatement.SQLStatementType.Insert);
			base.ExplicitVisit(element);
			currentStatement = null;    // Moved through all children and exited from current statement
		}

		public override void ExplicitVisit(UpdateStatement element)
		{
			currentStatement = currentModule.AddStatementWithTarget(element.UpdateSpecification.Target as dynamic, element, SQLStatement.SQLStatementType.Update);
			base.ExplicitVisit(element);
			currentStatement = null;    // Moved through all children and exited from current statement
		}

		public override void ExplicitVisit(MergeStatement element)
		{
			currentStatement = currentModule.AddStatementWithTarget(element.MergeSpecification.Target as dynamic, element, SQLStatement.SQLStatementType.Merge);
			base.ExplicitVisit(element);
			currentStatement = null;    // Moved through all children and exited from current statement
		}

		public override void ExplicitVisit(DeleteStatement element)
		{
			currentStatement = currentModule.AddStatementWithTarget(element.DeleteSpecification.Target as dynamic, element, SQLStatement.SQLStatementType.Delete);
			base.ExplicitVisit(element);
			currentStatement = null;    // Moved through all children and exited from current statement
		}

		public override void ExplicitVisit(TruncateTableStatement element)
		{
			currentStatement = currentModule.AddStatementWithTarget(element.TableName as dynamic, element, SQLStatement.SQLStatementType.Delete);
			base.ExplicitVisit(element);
			currentStatement = null;    // Moved through all children and exited from current statement
		}

		//public override void ExplicitVisit(ExecuteStatement element)
		//{
		//    TSqlFragment executedItem = element.ExecuteSpecification.ExecutableEntity;
		//    if (executedItem == null)
		//        executedItem = element.ExecuteSpecification.Variable;

		//    currentStatement = currentModule.AddStatementWithTarget(executedItem as dynamic, element, SQLStatement.SQLStatementType.Execute, RestoreStatement(element));
		//    base.ExplicitVisit(element);
		//    currentStatement = null;    // Moved through all children and exited from current statement
		//}

		// Instead of ExecuteStatement lets take ExecuteSpecification, because syntax "insert into exec" doesn't caught by ExecuteStatement. This is InsertStatement. because of that Execute itself goes out of scope
		// There's no need to change other *Statement to *Specification, because there might be subqueries and all processing of currentStatement will shatter. If so we couldn't find changed tables on expressions like "update D from dbo.Data as D"
		public override void ExplicitVisit(ExecuteSpecification element)
		{
			TSqlFragment executedItem = element.ExecutableEntity;
			if (executedItem == null)
				executedItem = element.Variable;

			currentStatement = currentModule.AddStatementWithTarget(executedItem as dynamic, element, SQLStatement.SQLStatementType.Execute);
			base.ExplicitVisit(element);
			currentStatement = null;    // Moved through all children and exited from current statement
		}

		#endregion Collecting top level expressions might be interesting: changing data, module execution, etc

		#region Searching and processing dependencies

		#region Inheritors of TSqlFragment

		// TSqlFragment -> TableReference
		public override void Visit(TableReference element)
		{
			// But all caught by ExplicitVisit must be excluded. We will process em directly, to avoid douplicates
			if (
				element is ChangeTableChangesTableReference // Not interesting
				|| element is DataModificationTableReference
				|| element is JoinParenthesisTableReference // Not interesting, because any way we will dive deeper
				|| element is InlineDerivedTable // Not interesting, there are no references
				|| element is GlobalFunctionTableReference
				|| element is NamedTableReference
				|| element is OpenXmlTableReference
				|| element is PivotedTableReference // Not interesting, this is just a transformation
				|| element is QueryDerivedTable // Not interesting, because any way we will dive deeper
				|| element is QualifiedJoin // Not interesting, because any way we will dive deeper
				|| element is SchemaObjectFunctionTableReference
				|| element is UnpivotedTableReference // Not interesting, this is just a transformation
				|| element is UnqualifiedJoin // Not interesting, because any way we will dive deeper
				|| element is VariableMethodCallTableReference
				|| element is VariableTableReference
				|| element is OpenJsonTableReference // Not interesting, because any way we will dive deeper
				)
				return;
			try
			{
				throw new NotImplementedException("Processing not implemented for this element");
			}
			catch (Exception ex)
			{
				HandleError(ex, element);
			}
		}

		#region Inheritors of TSqlFragment -> TableReference -> TableReferenceWithAlias

		// TSqlFragment -> TableReference -> TableReferenceWithAlias -> GlobalFunctionTableReference
		public override void ExplicitVisit(GlobalFunctionTableReference element)
		{
			ProcessVisit(element as dynamic, SQLStatement.SQLStatementType.Select);
			base.ExplicitVisit(element);
		}

		// TSqlFragment -> TableReference -> TableReferenceWithAlias -> NamedTableReference
		public override void ExplicitVisit(NamedTableReference element)
		{
			ProcessVisit(element as dynamic);

			base.ExplicitVisit(element);
		}

		#endregion Inheritors of TSqlFragment -> TableReference -> TableReferenceWithAlias

		#region Inheritors TSqlFragment -> TableReference -> TableReferenceWithAlias -> TableReferenceWithAliasAndColumns

		// TSqlFragment -> TableReference -> TableReferenceWithAlias -> TableReferenceWithAliasAndColumns -> DataModificationTableReference
		public override void ExplicitVisit(DataModificationTableReference element)
		{
			// Mighty syntax. This can be fired by something like "insert into ... select ... from (delete output)"
			// This is data changing(!!!) subquery inside main expression. We have to process it like new expression, because we need to create new Target, at least.
			// Unfortunatly that won't help in case of CTE, but it will be a key to understand it - Alias not being processed.
			// For examle in case of "with T (select * from A) delete T", we will see deleting from Aliase T itself.
			// TODO. Learn to detect real changing object, when using CTE

			// Current statement we will remember (it could be stack)
			SQLStatement mainStatement = currentStatement;

			// Switch context to data modification substatement
			currentStatement = ProcessDataModificationSpecification(element.DataModificationSpecification as dynamic);

			base.ExplicitVisit(element);

			// Return context to main statement
			currentStatement = mainStatement;
		}

		// TSqlFragment -> TableReference -> TableReferenceWithAlias -> TableReferenceWithAliasAndColumns -> OpenXmlTableReference
		public override void ExplicitVisit(OpenXmlTableReference element)
		{
			ProcessVisit(element as dynamic, SQLStatement.SQLStatementType.Execute);

			base.ExplicitVisit(element);
		}

		// TSqlFragment -> TableReference -> TableReferenceWithAlias -> TableReferenceWithAliasAndColumns -> SchemaObjectFunctionTableReference
		public override void ExplicitVisit(SchemaObjectFunctionTableReference element)
		{
			ProcessVisit(element as dynamic, SQLStatement.SQLStatementType.Select);

			base.ExplicitVisit(element);
		}

		// TSqlFragment -> TableReference -> TableReferenceWithAlias -> TableReferenceWithAliasAndColumns -> VariableMethodCallTableReference
		public override void ExplicitVisit(VariableMethodCallTableReference element)
		{
			ProcessVisit(element as dynamic, SQLStatement.SQLStatementType.Execute);

			base.ExplicitVisit(element);
		}

		// TSqlFragment -> TableReference -> TableReferenceWithAlias -> VariableTableReference
		public override void ExplicitVisit(VariableTableReference element)
		{
			ProcessVisit(element as dynamic);

			base.ExplicitVisit(element);
		}

		#endregion Inheritors TSqlFragment -> TableReference -> TableReferenceWithAlias -> TableReferenceWithAliasAndColumns

		#region Inheritors TSqlFragment -> ScalarExpression -> PrimaryExpression -> FunctionCall

		// TSqlFragment -> ScalarExpression -> PrimaryExpression -> FunctionCall
		public override void ExplicitVisit(FunctionCall element)
		{
			ProcessVisit(element as dynamic, SQLStatement.SQLStatementType.Select);

			base.ExplicitVisit(element);
		}

		#endregion Inheritors TSqlFragment -> ScalarExpression -> PrimaryExpression -> FunctionCall

		#endregion Inheritors of TSqlFragment

		#endregion Searching and processing dependencies

		#region Methods for supporting Visiting

		// Processing visiting with known Dependency type
		public void ProcessVisit(TSqlFragment element, SQLStatement.SQLStatementType dependencyType)
		{
			try
			{
				// This expression we are interested in. Lets take it
				currentModule.AddStatement(currentStatement);
				// TODO: Call above can do not add currentStatement, so all our new Patricipants will fly away
				currentStatement.AddParticipant(element as dynamic, dependencyType);
			}
			catch (Exception ex)
			{
				HandleError(ex, element);
			}
		}

		// Processing visiting with known Dependency type
		public void ProcessVisit(TSqlFragment element)
		{
			try
			{
				// This expression we are interested in. Lets take it
				currentModule.AddStatement(currentStatement);
				// TODO: Call above can do not add currentStatement, so all our new Patricipants will fly away
				currentStatement.AddParticipant(element as dynamic);
			}
			catch (Exception ex)
			{
				HandleError(ex, element);
			}
		}

		private SQLStatement ProcessDataModificationSpecification(InsertSpecification element)
		{
			return ProcessDataModificationSpecification(element.Target as dynamic, element as dynamic, SQLStatement.SQLStatementType.Insert);
		}

		private SQLStatement ProcessDataModificationSpecification(UpdateSpecification element)
		{
			return ProcessDataModificationSpecification(element.Target as dynamic, element as dynamic, SQLStatement.SQLStatementType.Update);
		}

		private SQLStatement ProcessDataModificationSpecification(DeleteSpecification element)
		{
			return ProcessDataModificationSpecification(element.Target as dynamic, element as dynamic, SQLStatement.SQLStatementType.Delete);
		}

		private SQLStatement ProcessDataModificationSpecification(MergeSpecification element)
		{
			return ProcessDataModificationSpecification(element.Target as dynamic, element as dynamic, SQLStatement.SQLStatementType.Merge);
		}

		private SQLStatement ProcessDataModificationSpecification(TableReference target, DataModificationSpecification element, SQLStatement.SQLStatementType dependencyType)
		{
			// However we must treat subquery like meanfull expression. Items being read - will be processed any way by reference detector. Our aim - make separate expression to change target. It will not help us with CTE, but we did try at least something.
			return currentModule.AddStatementWithTarget(target as dynamic, element, dependencyType);
		}

		#endregion Methods for supporting Visiting

		public static string GenerateStatement(TSqlFragment element)
		{
			Sql150ScriptGenerator generator = new Sql150ScriptGenerator(new SqlScriptGeneratorOptions()
			{
				AlignClauseBodies = true,
				IncludeSemicolons = true,
				NewLineBeforeJoinClause = true,
				NewLineBeforeFromClause = true,
				NewLineBeforeCloseParenthesisInMultilineList = true,
				NewLineBeforeWhereClause = true
			});
			string script;
			generator.GenerateScript(element, out script);
			return script;
		}

		public void HandleError(Exception ex, TSqlFragment element)
		{
			string message = ComposeMessage(ex.Message, element);
			currentModule.AddParseError(message, element);
		}

		// Message for Error handling
		public static string ComposeMessage(string message, TSqlFragment element)
		{
			string result = message;
			result += "\n";
			result += $"Type: {element.GetType()}";
			result += "\n";
			result += "Expression:";
			result += "\n";
			result += GenerateStatement(element);

			return result;
		}
	}
}
