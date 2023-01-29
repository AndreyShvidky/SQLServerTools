using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace ObjectDependencyExplorer
{
    public class SQLObject
    {
        public string Server { get; private set; }
        public string DataBase { get; private set; }
        public string Schema { get; private set; }
        public string Name { get; private set; }
        public string Alias { get; private set; }
        // True if we detect, taht this Object is pure alias in statment like "delete A from dbo.SomeTable as A" or "update A set ... from dbo.SomeTable as A"
        public bool IsAlias { get; set; } = false;
		public string Definition { get; set; }

		#region Constructors

		// Common object
		public SQLObject(string server, string dataBase, string schema, string name, string alias, string definition)
        {
			Server = server;
			DataBase = dataBase;
			Schema = schema;
			Name = name;
			Alias = alias;
			Definition = definition;
		}

		public SQLObject(SchemaObjectName sqlObject)
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

			Definition = SQLDependenciesVisitor.RestoreStatement(sqlObject);
		}

		// Aliasable object within schema
		public SQLObject(SchemaObjectName sqlObject, Identifier alias) : this(sqlObject)
        {
            //Process(sqlObject as dynamic);
            Alias = alias?.Value;
            Definition = SQLDependenciesVisitor.RestoreStatement(sqlObject);
			string _alias = SQLDependenciesVisitor.RestoreStatement(alias);
			if (!string.IsNullOrEmpty(Definition) && !string.IsNullOrEmpty(_alias))
                Definition += " as ";
			Definition += _alias;
		}

		// Aliasable table variable
		public SQLObject(VariableReference variable, Identifier alias)
        {
            Name = variable.Name ;
            Alias = alias?.Value;
            Definition = SQLDependenciesVisitor.RestoreStatement(variable);
        }

		// Aliasable table variable
		public SQLObject(VariableReference variable, Identifier sqlObject, Identifier alias)
        {
            Name = variable.Name + "." + sqlObject.Value;
            Alias = alias?.Value;
            Definition = SQLDependenciesVisitor.RestoreStatement(variable) + SQLDependenciesVisitor.RestoreStatement(sqlObject);
        }

		// Aliasable object
		public SQLObject(Identifier sqlObject, Identifier alias)
        {
            Name = sqlObject.Value;
            Alias = alias?.Value;
            Definition = SQLDependenciesVisitor.RestoreStatement(sqlObject);
        }

#endregion Constructors

		public void CopyAllProperties(SQLObject from)
        {
            Server = from.Server;
            DataBase = from.DataBase;
            Schema = from.Schema;
            Name = from.Name;
            Alias = from.Alias;
            Definition = from.Definition;
        }

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

        public override string ToString()
        {
            return $"{Server ?? string.Empty}.{DataBase ?? string.Empty}.{Schema ?? string.Empty}.{Name ?? string.Empty}";
        }

        // При сравнении внутри коллекции SQLStatement
        public string ToStringWithAlias()
        {
            string result = $"{Server ?? string.Empty}.{DataBase ?? string.Empty}.{Schema ?? string.Empty}.{Name ?? string.Empty}";
            if (Alias?.Length > 0)
                result += " as " + Alias;
            return result;
        }
    }

/*
	public class SQLObjectComparer : IEqualityComparer<SQLObject>
	{
		public bool Equals(SQLObject x, SQLObject y)
		{
			return x.ToString().Equals(y.ToString(), StringComparison.InvariantCultureIgnoreCase);
		}

		public int GetHashCode(SQLObject obj)
		{
			return obj.ToString().GetHashCode();
		}
	}
*/
}
