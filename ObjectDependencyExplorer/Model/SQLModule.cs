using Microsoft.SqlServer.TransactSql.ScriptDom;
using ObjectDependencyExplorer.ErrorHandlers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace ObjectDependencyExplorer
{
    public class SQLModule
    {
        public string Schema { get; }
        public string Name { get; }
        public string Definition { get; }
        public HashSet<SQLStatement> Statements { get; private set; }
		public HashSet<SQLObjectReference> Dependencies { get; private set; }
		public IList<ParseMessage> Messages { get; }

		public SQLModule(string schema, string name, string definition)
        {
            Schema = schema;
            Name = name;
            Definition = definition;
            Messages = new List<ParseMessage>();
        }

        public void Parse(ILogger logger)
        {
            TSql150Parser parser = new TSql150Parser(true);
            TextReader sqlModuleText = new StringReader(Definition);
            TSqlFragment parseResult;
            Statements = new();
            Dependencies = new();

            IList<ParseError> parseErrors;
            parseResult = parser.Parse(sqlModuleText, out parseErrors);
            foreach (ParseError err in parseErrors)
                Messages.Add(new ParseMessage(ParseMessage.MessageType.Error, err.Message, err.Line, err.Column, err.Offset, 3));

            // Main magic here: His magesty, Visitor!
            parseResult.Accept(new SQLDependenciesVisitor(this, logger));

            if (logger != null)
                logger.LogInformation("=== Dependencies from expressions ===");

            // Now we have all statements of our interest
            // Lets generate result - Dependencies
            foreach (SQLStatement statement in Statements)
            {
                if (logger != null)
                {
                    logger.LogInformation($"Выражение. Тип фрагмента: {statement.SQLFragment.GetType().Name}, Тип: {statement.Type}");
                    logger.LogInformation(SQLDependenciesVisitor.RestoreStatement(statement.SQLFragment));
                }

				// Adding target objects. i.e. those objects change operation performed on
				// But only if it doesn't provided in statement.References (what is an Error, friendly speaking, but if so - it should be investigated and described why such happens)
				if (statement.TargetObject != null && !statement.TargetObject.IsAlias)
                {
					// Target object is the most reliable source of information about changed object, but there will be dublicate mentioning, because Target object also mentioned (at least it should be) somewhere in References
					// Moreover, Target object fragment is whole changing data statement, but our aim is to higlight only Target object name
					// So we mentioning Target object only when it not mentioned in References (but if so - it should be investigated and described why such happens)
                    if (statement.References.Count > 0 && !statement.References.Where(it => it.IsTarget == true).Any())
                    {
						AddParseWarning("Target object not detected in Statement fragments", statement.SQLFragment);
						//AddTargetDependency(statement.TargetObject, statement.Type, statement.SQLFragment);
					}
				}

				// Adding rest of Objects. I.e. those who mentioned somewhere in "from" clause
				foreach (SQLObjectReference dep in statement.References)
                {
                    if (logger != null)
                    {
                        if (dep.ReferenceType > SQLStatement.SQLStatementType.Select) logger.LogWarning("Target object:");
                        logger.LogInformation($"Type: {dep.ReferenceType}, Server: {dep.Server}, Database: {dep.DataBase}, Schema: {dep.Schema}, Name: {dep.Name}, Alias: {dep.Alias}");
                        logger.LogInformation(SQLDependenciesVisitor.RestoreStatement(dep.SQLFragment));
                    }

                    AddDependency(dep);
                }
            }

            if (logger != null)
            {
                logger.LogInformation("====== Module dependencies ======");
                foreach (SQLObjectReference dep in Dependencies)
                {
                    logger.LogInformation($"Module. Type: {dep.ReferenceType}, Server: {dep.Server}, Database: {dep.DataBase}, Schema: {dep.Schema}, Name: {dep.Name}, Alias: {dep.Alias}");
                    logger.LogInformation(SQLDependenciesVisitor.RestoreStatement(dep.SQLFragment));
                }
            }
        }

		// Statement with target are for sure in our interest, so we adding em to collection immidiately
		public SQLStatement AddStatementWithTarget(TSqlFragment target, TSqlFragment fragment, SQLStatement.SQLStatementType type)
        {
			try
			{
				SQLStatement newStatement = new(target as dynamic, type, fragment);
				AddStatement(newStatement);
				return newStatement;
			}
			catch (Exception ex)
			{
				HandleError(ex, fragment);
			}

			return null;
		}

		// Statement without target are in our interest only after further investigation, so here is a method for adding em later
		public void AddStatement(SQLStatement statement)
        {
            bool wasAdded = Statements.Add(statement);  // Variable only for Debug purpose
        }

		// Adding Target Dependency
		public void AddTargetDependency(SQLObjectReference sqlObject, SQLStatement.SQLStatementType type, TSqlFragment fragment)
        {
			AddDependency(sqlObject);
        }

		// Adding existing Dependency (copy from Statement.Dependencies to Module.Dependencies
		public void AddDependency(SQLObjectReference reference)
        {
			bool wasAdded = Dependencies.Add(reference);   // Variable only for Debug purpose
		}

        public void AddParseWarning(string message, TSqlFragment statement)
        {
            ParseMessage err = new ParseMessage(ParseMessage.MessageType.Warning, message, statement);
            Messages.Add(err);
        }

        public void AddParseError(string message, TSqlFragment statement)
        {
            ParseMessage err = new ParseMessage(ParseMessage.MessageType.Error, message, statement);
            Messages.Add(err);
        }

		public void HandleError(Exception ex, TSqlFragment element)
		{
			string message = SQLDependenciesVisitor.ComposeMessage(ex.Message, element);
            if (ex is WarningException)
				AddParseWarning(message, element);
            else
			    AddParseError(message, element);
		}
	}

    //// На уровне модуля же Алиас не важен. И нужно сплющить одинаковые зависимости, отличающиеся только Алиасом
    //public class SQLModuleDependencyComparer : IEqualityComparer<SQLDependency>
    //{
    //    public bool Equals(SQLDependency x, SQLDependency y)
    //    {
    //        return x.DBObject.Equals(y.DBObject) || x.Type.Equals(y.Type);
    //    }

    //    public int GetHashCode(SQLDependency obj)
    //    {
    //        return obj.ToString().GetHashCode();
    //    }
    //}
}
