using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace ObjectDependencyExplorer
{
    public class ParseMessage
    {
        public enum MessageType
        {
            Message = 0,
            Warning = 1,
            Error = 2
        }

        public MessageType Type;
        public string Message;
		public int Line;
		public int Column;
		public int FragmentOffset;
		public int FragmentLength;

		public ParseMessage(MessageType type, string message, TSqlFragment statement)
		{
			Type = type;
			Message = SQLDependenciesVisitor.ComposeMessage(message, statement);
			Line = statement.StartLine;
			Column = statement.StartColumn;
			FragmentOffset = statement.StartOffset;
			FragmentLength = statement.FragmentLength;
		}

		public ParseMessage(MessageType type, string message, int line, int column, int offset, int len)
        {
            Type = type;
            Message = message;
            Line = line;
            Column = column;
            FragmentOffset = offset;
            FragmentLength = len;
        }
    }
}
