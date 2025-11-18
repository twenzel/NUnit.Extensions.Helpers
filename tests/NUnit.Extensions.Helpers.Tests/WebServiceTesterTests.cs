using System.Text;
using Shouldly;

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
			action.ShouldThrow<ArgumentException>();
		}

		[Test]
		public void Throws_Exception_If_Stream_Is_Null()
		{
			var action = () => new WebServiceTester((Stream)null!);
			action.ShouldThrow<ArgumentNullException>().Message.ShouldBe("Value cannot be null. (Parameter 'openApiDocument')");
		}
	}

	[Test]
	public async Task Throws_Exception_If_File_Does_Not_Exist()
	{
		var helper = new WebServiceTester("somenotexistingfile.json");
		var action = async () => await helper.VerifySecuredEndpointsRequiresAuthentication(null!, CancellationToken.None);
		await action.ShouldThrowAsync<FileNotFoundException>();
	}

	[Test]
	public async Task Throws_Exception_If_Document_Is_Invalid()
	{
		var stream = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

		var helper = new WebServiceTester(stream);
		var action = async () => await helper.VerifySecuredEndpointsRequiresAuthentication(null!, CancellationToken.None);
		await action.ShouldThrowAsync<Microsoft.OpenApi.OpenApiUnsupportedSpecVersionException>();
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
			callCount.ShouldBe(9);
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
			await action.ShouldThrowAsync<TestFailedException>();
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
						request.Content.ShouldNotBeNull();
					}

					return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
				}
			};

			var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test.com") };

			var helper = new WebServiceTester(stream);
			await helper.VerifySecuredEndpointsRequiresAuthentication(httpClient, CancellationToken.None);
		}
	}

	public class RequestCreationTests : WebServiceTesterTests
	{
		private WebServiceTester _tester;
		private DelegateHttpMessageHandler _handler;
		private HttpClient _httpClient;

		[SetUp]
		public void Setup()
		{
			var stream = ReadFromResource("petstore_swagger.json");

			_handler = new DelegateHttpMessageHandler
			{
				RequestDelegate = (request) =>
				{
					return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { RequestMessage = request };
				}
			};

			_httpClient = new HttpClient(_handler) { BaseAddress = new Uri("http://test.com") };

			_tester = new WebServiceTester(stream);
		}

		[TearDown]
		public void TearDown()
		{
			_handler?.Dispose();
			_httpClient?.Dispose();
		}

		[Test]
		public async Task Builds_Request_Uri_With_Parameters()
		{
			await _tester.CallEveryEndpoint(_httpClient, CancellationToken.None, (info, response) =>
			{
				if (info.Path == "/pet/{petId}/uploadImage")
				{
					response.RequestMessage!.RequestUri!.AbsolutePath.ShouldBe("/pet/1/uploadImage");
				}
			});
		}

		[Test]
		public async Task Builds_Request_Uri_With_Custom_Parameters()
		{
			_tester.CustomParameterValue = (info) =>
			{
				if (info.Operation.OperationId == "uploadFile")
					return "22";

				return null;
			};

			await _tester.CallEveryEndpoint(_httpClient, CancellationToken.None, (info, response) =>
			{
				if (info.Path == "/pet/{petId}/uploadImage")
				{
					response.RequestMessage!.RequestUri!.AbsolutePath.ShouldBe("/pet/22/uploadImage");
				}
			});
		}

		[Test]
		public async Task Builds_Request_With_MultiFormData_Content()
		{
			await _tester.CallEveryEndpoint(_httpClient, CancellationToken.None, (info, response) =>
			{
				if (info.Path == "/pet/{petId}/uploadImage" && info.HttpMethod == HttpMethod.Post)
				{
					response.RequestMessage!.Content.ShouldNotBeNull();
					response.RequestMessage!.Content.ShouldBeOfType<MultipartFormDataContent>();
					var content = (MultipartFormDataContent)response.RequestMessage!.Content;
					content.ShouldContain(c => c is StringContent);
					content.ShouldContain(c => c is StreamContent);
				}
			});
		}

		[Test]
		public async Task Builds_Request_With_Form_Content()
		{
			await _tester.CallEveryEndpoint(_httpClient, CancellationToken.None, (info, response) =>
			{
				if (info.Path == "/pet/{petId}" && info.HttpMethod == HttpMethod.Post)
				{
					response.RequestMessage!.Content.ShouldNotBeNull();
					response.RequestMessage!.Content.ShouldBeOfType<FormUrlEncodedContent>();
					var content = (FormUrlEncodedContent)response.RequestMessage!.Content;
					var s = content.ReadAsStringAsync().Result;

					s.ShouldBe("name=test&status=test");
				}
			});
		}

		[Test]
		public async Task Builds_Request_With_Form_Content_With_Custom_Values()
		{
			_tester.CustomParameterValue = (info) =>
			{
				if (info.Operation.OperationId == "updatePetWithForm" && info.ParameterName == "status")
					return "active";

				return null;
			};

			await _tester.CallEveryEndpoint(_httpClient, CancellationToken.None, (info, response) =>
			{
				if (info.Path == "/pet/{petId}" && info.HttpMethod == HttpMethod.Post)
				{
					response.RequestMessage!.Content.ShouldNotBeNull();
					response.RequestMessage!.Content.ShouldBeOfType<FormUrlEncodedContent>();
					var content = (FormUrlEncodedContent)response.RequestMessage!.Content;
					var s = content.ReadAsStringAsync().Result;

					s.ShouldBe("name=test&status=active");
				}
			});
		}

		[Test]
		public async Task Builds_Request_With_Json_Content()
		{
			await _tester.CallEveryEndpoint(_httpClient, CancellationToken.None, (info, response) =>
			{
				if (info.Path == "/pet" && info.HttpMethod == HttpMethod.Post)
				{
					response.RequestMessage!.Content.ShouldNotBeNull();
					response.RequestMessage!.Content.ShouldBeOfType<StringContent>();
					var content = (StringContent)response.RequestMessage!.Content;
					var s = content.ReadAsStringAsync().Result;

					s.ShouldBe("""
{
"name": "test",
"photoUrls": ["test"]
}
""");
				}
			});
		}

		[Test]
		public async Task Builds_Request_With_Json_Content_With_Custom_Value()
		{
			_tester.CustomParameterValue = (info) =>
			{
				if (info.Operation.OperationId == "addPet" && info.ParameterName == "name")
					return "Spike";

				return null;
			};

			await _tester.CallEveryEndpoint(_httpClient, CancellationToken.None, (info, response) =>
			{
				if (info.Path == "/pet" && info.HttpMethod == HttpMethod.Post)
				{
					response.RequestMessage!.Content.ShouldNotBeNull();
					response.RequestMessage!.Content.ShouldBeOfType<StringContent>();
					var content = (StringContent)response.RequestMessage!.Content;
					var s = content.ReadAsStringAsync().Result;

					s.ShouldBe("""
{
"name": "Spike",
"photoUrls": ["test"]
}
""");
				}
			});
		}
	}

	private Stream ReadFromResource(string resourceName)
	{
		var stream = GetType().Assembly.GetManifestResourceStream($"NUnit.Extensions.Helpers.Tests.TestData.{resourceName}");

		return stream ?? throw new System.ArgumentException($"Resource '{resourceName}' not found!", nameof(resourceName));
	}
}
