namespace NUnit.Extensions.Helpers;

public class TestFailedException : Exception
{
	public TestFailedException() { }
	public TestFailedException(string message) : base(message) { }
	public TestFailedException(string message, Exception inner) : base(message, inner) { }

}
