using System.Text;
using FluentAssertions;

namespace NUnit.Extensions.Helpers.Tests;

public class WebServiceTesterTests
{
	public class ConstructorTests : WebServiceTesterTests
	{
		[TestCase("")]
		[TestCase(null)]
		public void Throws_Exception_If_Path_Is_Null(string? path)
		{
			var action = () => new WebServiceTester(path!);
			action.Should().Throw<ArgumentException>();
		}

		[Test]
		public void Throws_Exception_If_Stream_Is_Null()
		{
			var action = () => new WebServiceTester((Stream)null!);
			action.Should().Throw<ArgumentNullException>().Which.Message.Should().Be("Value cannot be null. (Parameter 'openApiDocument')");
		}
	}

	[Test]
	public async Task Throws_Exception_If_File_Does_Not_Exist()
	{
		var helper = new WebServiceTester("somenotexistingfile.json");
		var action = async () => await helper.VerifySecuredEndpointsRequiresAuthentication(null!, CancellationToken.None);
		await action.Should().ThrowExactlyAsync<FileNotFoundException>();
	}

	[Test]
	public async Task Throws_Exception_If_Document_Is_Invalid()
	{
		var stream = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

		var helper = new WebServiceTester(stream);
		var action = async () => await helper.VerifySecuredEndpointsRequiresAuthentication(null!, CancellationToken.None);
		await action.Should().ThrowAsync<Microsoft.OpenApi.Readers.Exceptions.OpenApiUnsupportedSpecVersionException>();
	}

	public class VerifySecuredEndpointsRequiresAuthenticationMethod : WebServiceTesterTests
	{
		[Test]
		public async Task Requests_Every_Secured_Endpoint()
		{
			var stream = ReadFromResource("petstore_swagger.json");

			var handler = new DelegateHttpMessageHandler();
			var callCount = 0;

			handler.RequestDelegate = (request) =>
			{
				ArgumentNullException.ThrowIfNull(request.RequestUri);

				if (request.RequestUri.AbsolutePath != "/pet/1/uploadImage"
				&& request.RequestUri.AbsolutePath != "/pet"
				&& request.RequestUri.AbsolutePath != "/pet/findByStatus"
				&& request.RequestUri.AbsolutePath != "/pet/findByTags"
				&& request.RequestUri.AbsolutePath != "/pet/1"
				&& request.RequestUri.AbsolutePath != "/store/inventory"
				)
					Assert.Fail($"{request.RequestUri} is not a secured endpoint");

				callCount++;

				return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
			};

			var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test.com") };

			var helper = new WebServiceTester(stream);
			await helper.VerifySecuredEndpointsRequiresAuthentication(httpClient, CancellationToken.None);
			callCount.Should().Be(9);
		}

		[Test]
		public async Task Throws_Exception_If_Secured_Endpoint_Does_Not_Return_401()
		{
			var stream = ReadFromResource("petstore_swagger.json");

			var handler = new DelegateHttpMessageHandler
			{
				RequestDelegate = (request) =>
				{
					ArgumentNullException.ThrowIfNull(request.RequestUri);

					if (request.RequestUri.AbsolutePath != "/pet/1/uploadImage")
						return new HttpResponseMessage(System.Net.HttpStatusCode.OK);

					return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
				}
			};

			var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test.com") };

			var helper = new WebServiceTester(stream);
			var action = async () => await helper.VerifySecuredEndpointsRequiresAuthentication(httpClient, CancellationToken.None);
			await action.Should().ThrowAsync<TestFailedException>();
		}

		[Test]
		public async Task Requests_Post_Endpoint_With_Body()
		{
			var stream = ReadFromResource("petstore_swagger.json");

			var handler = new DelegateHttpMessageHandler
			{
				RequestDelegate = (request) =>
				{
					if (request.RequestUri?.AbsolutePath == "/pet" && request.Method == HttpMethod.Post)
					{
						request.Content.Should().NotBeNull();
					}

					return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
				}
			};

			var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test.com") };

			var helper = new WebServiceTester(stream);
			await helper.VerifySecuredEndpointsRequiresAuthentication(httpClient, CancellationToken.None);
		}
	}

	private Stream ReadFromResource(string resourceName)
	{
		var stream = GetType().Assembly.GetManifestResourceStream($"NUnit.Extensions.Helpers.Tests.TestData.{resourceName}");

		return stream ?? throw new System.ArgumentException($"Resource '{resourceName}' not found!", nameof(resourceName));
	}
}
