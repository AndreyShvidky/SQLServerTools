using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.Arm;

namespace ObjectDependencyExplorer
{
    public class SQLStatement
    {
        public enum SQLStatementType
        {
            Unknown = 0,
            Select = 1,
            Insert = 2,
            Update = 3,
            Delete = 4,
            Merge = 5,
            Execute = 10
        }

        public TSqlFragment SQLFragment { get; }
        public SQLStatementType Type { get; }
        public HashSet<SQLObjectReference> References { get; private set; }
		public SQLObjectReference TargetObject { get; }

		// Just statement
		public SQLStatement(SQLStatementType statementType, TSqlFragment statement)
        {
            Type = statementType;
            SQLFragment = statement;
            References = new();
        }

		// Statement with Target object
		public SQLStatement(TSqlFragment targetReference, SQLStatementType statementType, TSqlFragment statement)
		{
			Type = statementType;
			SQLFragment = statement;
			References = new();
			SQLObjectReference targetDependency = AddTargetReference(targetReference as dynamic, Type);
			TargetObject = targetDependency;
		}

		// Target reference added only within local constructors
		private SQLObjectReference AddTargetReference(TSqlFragment reference, SQLStatementType dependencyType)
		{
			SQLObjectReference target = new(reference as dynamic, dependencyType) { IsTarget = true };
			References.Add(target);
			return target;
		}

		// All other references with known Dependency type
		public SQLObjectReference AddParticipant(TSqlFragment reference, SQLStatementType dependencyType)
		{
			// Check if it is already in collection
			SQLObjectReference sameOffsetReference = FindExistingReference(reference);

			// Create new Dependency
			SQLObjectReference dep = new(reference as dynamic, dependencyType);

			// Let more long reference prevail
			if (sameOffsetReference != null)
			{
				if (sameOffsetReference.SQLFragment.FragmentLength < dep.SQLFragment.FragmentLength)
					sameOffsetReference = dep;

				return sameOffsetReference;
			}

			References.Add(dep);
			return dep;
		}

		// All other participants with unknown Dependency type
		public SQLObjectReference AddParticipant(TSqlFragment reference)
		{
			// Check if it is already in collection
			SQLObjectReference sameOffsetReference = FindExistingReference(reference);

			SQLObjectReference newRef;

			// If it is Target object
			newRef = References.Where(it => it.SQLFragment == reference).FirstOrDefault();
			if (newRef != null)
				return newRef;

			// Trying to detect Dependency Type, looking up in whole Statement. This must help for such statments:
			// insert (into) dbo.SomeTable
			// update (top) dbo.SomeTable
			// delete (top) dbo.SomeTable
			// merge (top) dbo.SomeTable
			newRef = new(reference as dynamic, SQLStatementType.Unknown);
			bool isTarget = false;

			for (int i = reference.FirstTokenIndex - 1; i >= 0 && reference.ScriptTokenStream[i].Offset >= SQLFragment.StartOffset; i--)
			{
				switch (reference.ScriptTokenStream[i].TokenType)
				{
					case TSqlTokenType.From:
						newRef.ReferenceType = SQLStatementType.Select;

						// If we have object fully equal to TargetObject in "from" clause, so it is Target object in statements like "delete from dbo.SomeTable". Statements like "delete A from dbo.SomeTable as A" are precessed with following code
						if (!isTarget && TargetObject != null && newRef.Server == TargetObject.Server && newRef.DataBase == TargetObject.DataBase && newRef.Schema == TargetObject.Schema && newRef.Name == TargetObject.Name && newRef.Alias == TargetObject.Alias && reference.StartOffset >= SQLFragment.StartOffset && (reference.StartOffset + reference.FragmentLength) <= (SQLFragment.StartOffset + SQLFragment.FragmentLength))
						{
							newRef.ReferenceType = Type;
							isTarget = true;
						}

						break;
					case TSqlTokenType.Insert:
						newRef.ReferenceType = SQLStatementType.Insert;
						isTarget = true;
						break;
					case TSqlTokenType.Update:
						newRef.ReferenceType = SQLStatementType.Update;
						isTarget = true;
						break;
					case TSqlTokenType.Delete:
						newRef.ReferenceType = SQLStatementType.Delete;
						isTarget = true;
						break;
					case TSqlTokenType.Merge:
						newRef.ReferenceType = SQLStatementType.Merge;
						isTarget = true;
						break;
					default:
						break;
				}

				if (newRef.ReferenceType != SQLStatementType.Unknown)
					break;
			}

			if (newRef.ReferenceType == SQLStatementType.Unknown)
				throw new Exception("Failed to detect scope of TSqlFragment");

			// Target object can be mentioned somewhere in "from" clause
			// Target object is more reliable source of information about changed object, but can duplicate with object identifier detected by Identifier visitors
			// But for assurance let's keep Target detecting for now
			if (TargetObject != null)
			{
				switch (Type)
				{
					// If this Dependency has same alias as Target object name, than it could be syntax like "update A from dbo.Table as A". So mark it as Target
					// Such things happens only in "update", "delete" and ... "merge"?
					case SQLStatementType.Update:
					case SQLStatementType.Delete:
					case SQLStatementType.Merge:
						// Searching for object with Alias as Target name
						if (newRef.Alias == TargetObject.Name && newRef.ReferenceType == SQLStatementType.Select)
						{
							// Change Operation type as Target
							newRef.ReferenceType = Type;
							isTarget = true;
							TargetObject.IsAlias = true;
						}
						break;
					default:
						break;
				}
			}

			// Let more long reference prevail
			if (sameOffsetReference != null)
			{
				if (sameOffsetReference.SQLFragment.FragmentLength < newRef.SQLFragment.FragmentLength)
					sameOffsetReference = newRef;
				
					return sameOffsetReference;
			}

			References.Add(newRef);
			return newRef;
		}

		private SQLObjectReference FindExistingReference(TSqlFragment reference)
		{
			// We have a Key in output table with Offset included. But because some double function calls (like nodes.values) we can get two references from same Offset
			return References.Where(it => it.SQLFragment.StartOffset == reference.StartOffset).FirstOrDefault();
		}

		//// TSqlFragment -> MultiPartIdentifier -> SchemaObjectName
		//public SQLDependency AddTargetReference(SchemaObjectName targetReference, SQLStatementType dependencyType)
		//      {
		//	SQLDependency target = new(new SQLParsedObject(targetReference, null), dependencyType) { IsTarget = true };
		//	References.Add(target);
		//	return target;
		//}

		//// TSqlFragment -> TableReference -> TableReferenceWithAlias
		//public SQLDependency AddTargetReference(TableReferenceWithAlias targetReference, SQLStatementType dependencyType)
		//      {
		//          throw new Exception($"Got into trap for abstract class {targetReference.GetType()} on statement");
		//      }

		//// TSqlFragment -> TableReference -> TableReferenceWithAlias -> NamedTableReference
		//public SQLDependency AddTargetReference(NamedTableReference targetReference, SQLStatementType dependencyType)
		//      {
		//	SQLDependency target = new(new SQLParsedObject(targetReference.SchemaObject, targetReference.Alias), dependencyType) { IsTarget = true };
		//	References.Add(target);
		//	return target;
		//}

		//public SQLDependency AddTargetReference(VariableTableReference targetReference, SQLStatementType dependencyType)
		//      {
		//	SQLDependency target = new(new SQLParsedObject(targetReference, string.Empty, string.Empty, string.Empty, targetReference.Variable.Name, targetReference.Alias?.Value), dependencyType) { IsTarget = true };
		//	References.Add(target);
		//	return target;
		//}

		//// TSqlFragment -> ExecutableEntity -> ExecutableProcedureReference
		//public SQLDependency AddTargetReference(ExecutableProcedureReference targetReference, SQLStatementType dependencyType)
		//      {
		//	SQLParsedObject newParsedObject = null;

		//	if (targetReference.ProcedureReference?.ProcedureReference != null)
		//		newParsedObject = new SQLParsedObject(targetReference.ProcedureReference?.ProcedureReference?.Name);
		//          else if (targetReference.ProcedureReference?.ProcedureVariable != null)
		//		newParsedObject = new SQLParsedObject(targetReference, string.Empty, string.Empty, null, targetReference.ProcedureReference?.ProcedureVariable.Name, string.Empty);

		//          if (newParsedObject == null)
		//	    throw new Exception(SQLDependenciesVisitor.ComposeMessage("Missing processing unit for this kind of object", targetReference));

		//	SQLDependency target = new(newParsedObject, dependencyType) { IsTarget = true };
		//	References.Add(target);
		//	return target;
		//}

		//// TSqlFragment -> ExecutableEntity -> ExecutableStringList
		//public SQLDependency AddTargetReference(ExecutableStringList targetReference, SQLStatementType dependencyType)
		//      {
		//	SQLDependency target = new(new SQLParsedObject(targetReference, string.Empty, string.Empty, string.Empty, "{execute}", string.Empty), dependencyType) { IsTarget = true };
		//	References.Add(target);
		//	return target;
		//}
	}
}
