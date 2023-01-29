namespace ObjectDependencyExplorer.ErrorHandlers
{
	public enum MessageType
	{
		Message = 0,
		Warning = 1,
		Error = 2
	}

	public interface ILogItem
    {
		MessageType Type
		{
			get;
			set;
		}

		string Message
		{
			get;
			set;
		}
	}
}
