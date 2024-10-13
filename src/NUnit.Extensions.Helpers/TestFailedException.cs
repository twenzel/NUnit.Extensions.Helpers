namespace NUnit.Extensions.Helpers;

[Serializable]
public class TestFailedException : Exception
{
	public TestFailedException() { }
	public TestFailedException(string message) : base(message) { }
	public TestFailedException(string message, Exception inner) : base(message, inner) { }
	protected TestFailedException(
	  System.Runtime.Serialization.SerializationInfo info,
	  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
}
