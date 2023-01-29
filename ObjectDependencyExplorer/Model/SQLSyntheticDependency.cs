namespace ObjectDependencyExplorer
{
	// Class for loading and presentation information about SQL objects dependencies
	public class SQLSyntheticDependency
	{
		public SQLObject DBObject { get; }
		public SQLStatement.SQLStatementType Type { get; set; }
		// Line in SQL Module
		public int StartLine { get; }
		// Column (of first symbol) in SQL Module
		public int StartColumn { get; }
		// Position in SQL Module
		public int ModuleOffset { get; }
		// Length in SQL Module
		public int FragmentLength { get; }

		// Constructor for viewing (loaded from result)
		public SQLSyntheticDependency(SQLObject sqlObject, SQLStatement.SQLStatementType dependencyType, int startLine, int startColumn, int startOffset, int fragmentLength)
		{
			DBObject = sqlObject;
			Type = dependencyType;
			StartLine = startLine;
			StartColumn = startColumn;
			ModuleOffset = startOffset;
			FragmentLength = fragmentLength;
		}

		public override string ToString()
		{
			return $"{DBObject}({Type})";
		}

		public string ToStringWithAlias()
		{
			return $"{DBObject.ToStringWithAlias()}({Type})";
		}
	}
}
