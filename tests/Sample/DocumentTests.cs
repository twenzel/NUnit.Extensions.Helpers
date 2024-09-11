namespace Sample;

public partial class DocumentTests
{
	/// <summary>
	/// lala
	/// </summary>
	[Test]
	public void Test1()
	{
		Assert.Pass();
	}

	[GenerateConstructorParameterTests(typeof(Document))]
	public partial class CtorTests : DocumentTests
	{
	}
}