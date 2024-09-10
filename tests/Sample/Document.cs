namespace Sample;
public class Document
{
	private Stream _stream;
	private IFileTester _fileTester;
	private IOtherFilter _filter;

	public Document(Stream myStream, IFileTester fileTester, IOtherFilter filter)
	{
		_stream = myStream ?? throw new ArgumentNullException(nameof(myStream));
		_fileTester = fileTester ?? throw new ArgumentNullException(nameof(fileTester));
		_filter = filter ?? throw new ArgumentNullException(nameof(filter));
	}
}

public interface IFileTester { }
public interface IOtherFilter { }