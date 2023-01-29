using Microsoft.Data.Tools.Schema.Tasks.Sql;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.ComponentModel;
using System.Windows;

namespace ObjectDependencyExplorer
{
    public class SQLObjectReference
    {
		// SQL fragment, was parded as this object
		public TSqlFragment SQLFragment { get; private set; }
		public string Server { get; private set; }
        public string DataBase { get; private set; }
        public string Schema { get; private set; }
        public string Name { get; private set; }
        public string Alias { get; private set; }

		// Reference type
		public SQLStatement.SQLStatementType ReferenceType { get; set; }
		// Is Target object of SQL statement
		public bool IsTarget { get; set; } = false;
		// True if we detect, taht this Object is pure alias in statment like "delete A from dbo.SomeTable as A" or "update A set ... from dbo.SomeTable as A"
		public bool IsAlias { get; set; } = false;

		#region Constructors

		// Common object
		public SQLObjectReference(TSqlFragment fragment, string server, string dataBase, string schema, string name, string alias, SQLStatement.SQLStatementType referenceType)
        {
            SQLFragment = fragment;
			ReferenceType = referenceType;

			Server = server;
			DataBase = dataBase;
			Schema = schema;
			Name = name;
			Alias = alias;
		}

		// Aliasable table variable
		public SQLObjectReference(VariableReference variable, Identifier alias, SQLStatement.SQLStatementType referenceType)
		{
			ReferenceType = referenceType;

			Name = variable.Name;
			Alias = alias?.Value;
		}

		// Aliasable table variable
		public SQLObjectReference(VariableReference variable, Identifier sqlObject, Identifier alias, SQLStatement.SQLStatementType referenceType)
		{
			ReferenceType = referenceType;

			Name = variable.Name + "." + sqlObject.Value;
			Alias = alias?.Value;
		}

		// Aliasable object
		public SQLObjectReference(Identifier sqlObject, Identifier alias, SQLStatement.SQLStatementType referenceType)
		{
			SQLFragment = sqlObject;
			ReferenceType = referenceType;

			Name = sqlObject.Value;
			Alias = alias?.Value;
		}

		// TSqlFragment -> MultiPartIdentifier -> SchemaObjectName
		public SQLObjectReference(SchemaObjectName sqlObject, SQLStatement.SQLStatementType referenceType)
		{
			SQLFragment = sqlObject;
			ReferenceType = referenceType;

			ProcessSchemaObjectName(sqlObject);
		}

		// Aliasable object within schema
		public SQLObjectReference(SchemaObjectName sqlObject, Identifier alias, SQLStatement.SQLStatementType referenceType) : this(sqlObject, referenceType)
		{
			Alias = alias?.Value;
		}

		// TSqlFragment -> TableReference -> TableReferenceWithAlias
		public SQLObjectReference(TableReferenceWithAlias reference, SQLStatement.SQLStatementType referenceType)
        {
            throw new Exception($"Got into trap for abstract class {reference.GetType()} on statement");
        }

		// TSqlFragment -> TableReference -> TableReferenceWithAlias -> NamedTableReference
		public SQLObjectReference(NamedTableReference reference, SQLStatement.SQLStatementType referenceType) : this(reference.SchemaObject, reference.Alias, referenceType)
        {
			SQLFragment = reference;
		}

		// TSqlFragment -> ExecutableEntity -> ExecutableProcedureReference
		public SQLObjectReference(ExecutableProcedureReference reference, SQLStatement.SQLStatementType referenceType)
        {
			SQLFragment = reference;
			ReferenceType = referenceType;

			if (reference.ProcedureReference?.ProcedureReference != null)
				ProcessSchemaObjectName(reference.ProcedureReference?.ProcedureReference?.Name);
			else if (reference.ProcedureReference?.ProcedureVariable != null)
				Name = reference.ProcedureReference?.ProcedureVariable.Name;
		}

		// TSqlFragment -> ExecutableEntity -> ExecutableStringList
		public SQLObjectReference(ExecutableStringList reference, SQLStatement.SQLStatementType referenceType) : this(reference, string.Empty, string.Empty, string.Empty, "{execute}", string.Empty, referenceType)
		{
		}

		// TSqlFragment -> TableReference -> TableReferenceWithAlias -> GlobalFunctionTableReference
		public SQLObjectReference(GlobalFunctionTableReference reference, SQLStatement.SQLStatementType referenceType) : this(reference.Name, reference.Alias, referenceType)
		{
			SQLFragment = reference;
		}

		// TSqlFragment -> TableReference -> TableReferenceWithAlias -> OpenXmlTableReference
		public SQLObjectReference(OpenXmlTableReference reference, SQLStatement.SQLStatementType referenceType) : this(reference.Variable, reference.Alias, referenceType)
		{
			SQLFragment = reference;
		}

		// TSqlFragment -> TableReference -> TableReferenceWithAlias -> TableReferenceWithAliasAndColumns -> SchemaObjectFunctionTableReference
		public SQLObjectReference(SchemaObjectFunctionTableReference reference, SQLStatement.SQLStatementType referenceType) : this(reference.SchemaObject, reference.Alias, referenceType)
		{
			SQLFragment = reference;
		}

		// TSqlFragment -> TableReference -> TableReferenceWithAlias -> TableReferenceWithAliasAndColumns -> VariableMethodCallTableReference
		public SQLObjectReference(VariableMethodCallTableReference reference, SQLStatement.SQLStatementType referenceType) : this(reference.Variable, reference.MethodName, reference.Alias, referenceType)
		{
			SQLFragment = reference;
		}

		// TSqlFragment -> TableReference -> TableReferenceWithAlias -> VariableTableReference
		public SQLObjectReference(VariableTableReference reference, SQLStatement.SQLStatementType referenceType) : this(reference.Variable, reference.Alias, referenceType)
		{
			SQLFragment = reference;
		}

		// TSqlFragment -> ScalarExpression -> PrimaryExpression -> FunctionCall
		public SQLObjectReference(FunctionCall reference, SQLStatement.SQLStatementType referenceType)
		{
			SQLFragment = reference;
			ReferenceType = referenceType;

			MultiPartIdentifierCallTarget functionCallTarget = reference.CallTarget as MultiPartIdentifierCallTarget;
			string database = string.Empty;
			string schema = string.Empty;
			string name = reference.FunctionName.Value;

			if (functionCallTarget != null)
			{
				if (functionCallTarget.MultiPartIdentifier?.Identifiers?.Count == 1)
				{
					if (reference.FunctionName.Value == "value" || reference.FunctionName.Value == "query" || reference.FunctionName.Value == "nodes" || reference.FunctionName.Value == "exist")  // Это XML функции типа a.b.value()
					{
						name = functionCallTarget.MultiPartIdentifier.Identifiers[0].Value + "." + name;
					}
					else
					{
						schema = functionCallTarget.MultiPartIdentifier.Identifiers[0].Value;
					}
				}
				else
				{
					if (functionCallTarget.MultiPartIdentifier?.Identifiers?.Count == 2)
					{
						if (reference.FunctionName.Value == "value" || reference.FunctionName.Value == "query" || reference.FunctionName.Value == "nodes" || reference.FunctionName.Value == "exist")  // Это XML функции типа a.b.value()
						{
							name = functionCallTarget.MultiPartIdentifier.Identifiers[0].Value + "." + functionCallTarget.MultiPartIdentifier.Identifiers[1].Value + "." + name;
						}
						else
						{
							database = functionCallTarget.MultiPartIdentifier.Identifiers[0].Value;
							schema = functionCallTarget.MultiPartIdentifier.Identifiers[1].Value;
						}
					}
					else
						throw new Exception("Function call with two or more prefixes detected");
				}
			}

			Server = string.Empty;
			DataBase = database;
			Schema = schema;
			Name = name;
			Alias = string.Empty;
		}

		#endregion Constructors

		#region Multiusable auxilary Methods

		private void ProcessSchemaObjectName(SchemaObjectName sqlObject)
		{
			Server = sqlObject.ServerIdentifier?.Value;

			// XML TVF A.B.nodes goes like A = database, B = schema. Trying to deal with it
			if (sqlObject.BaseIdentifier?.Value == "nodes")
			{
				DataBase = string.Empty;
				Schema = string.Empty;
				Name = sqlObject.DatabaseIdentifier?.Value + "." + sqlObject.SchemaIdentifier?.Value + "." + sqlObject.BaseIdentifier?.Value;
			}
			else
			{
				DataBase = sqlObject.DatabaseIdentifier?.Value;
				Schema = sqlObject.SchemaIdentifier?.Value;
				Name = sqlObject.BaseIdentifier?.Value;
			}
		}

		#endregion Multiusable auxilary Methods

		//public void CopyAllProperties(SQLParsedObject from)
		//      {
		//          Server = from.Server;
		//          DataBase = from.DataBase;
		//          Schema = from.Schema;
		//          Name = from.Name;
		//          Alias = from.Alias;
		//      }

		public string FullName
        {
            get
            {
                string moduleFullName = Name;
                if (Schema?.Length > 0 || DataBase?.Length > 0 || Server?.Length > 0)
                    moduleFullName = ($"{Schema}.") + moduleFullName;
                if (DataBase?.Length > 0 || Server?.Length > 0)
                    moduleFullName = ($"{DataBase}.") + moduleFullName;
                if (Server?.Length > 0)
                    moduleFullName = ($"{Server}.") + moduleFullName;
                return moduleFullName;
            }
        }

        // Just for Debug
        public override string ToString()
        {
            string result = $"({ReferenceType}) {Server ?? string.Empty}.{DataBase ?? string.Empty}.{Schema ?? string.Empty}.{Name ?? string.Empty}";
			result += Alias?.Length > 0 ? $" as {Alias}" : string.Empty;
            return result;
        }
    }

/*
	public class SQLObjectComparer : IEqualityComparer<SQLParsedObject>
	{
		public bool Equals(SQLParsedObject x, SQLParsedObject y)
		{
			return x.ToString().Equals(y.ToString(), StringComparison.InvariantCultureIgnoreCase);
		}

		public int GetHashCode(SQLParsedObject obj)
		{
			return obj.ToString().GetHashCode();
		}
	}
*/
}
