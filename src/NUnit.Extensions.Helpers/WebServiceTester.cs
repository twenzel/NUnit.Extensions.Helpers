using System.Net;
using System.Text;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace NUnit.Extensions.Helpers;

/// <summary>
/// Helper class to execute tests for webservices
/// </summary>
public class WebServiceTester
{
	private const string CONTENT_TYPE_MULTIPART_FORM = "multipart/form-data";
	private const string CONTENT_TYPE_FORM = "application/x-www-form-urlencoded";
	private readonly string? _openApiDocumentPath;
	private Stream? _openApiDocumentStream;
	private OpenApiDocument? _openApiDocument;

	/// <summary>
	/// Gets or sets a delegate to customize the endpoint parameter value
	/// </summary>
	public Func<EndpointParameterInformation, string?>? CustomParameterValue { get; set; }

	/// <summary>
	/// Gets or sets a delegate to customize the request content
	/// </summary>
	public Func<RequestContentInformation, HttpContent?>? CustomRequestContent { get; set; }

	/// <summary>
	/// Creates a new instance of <see cref=" WebServiceTester"/>
	/// </summary>
	/// <param name="openApiDocumentPath">File path of the OpenApi document</param>
	public WebServiceTester(string openApiDocumentPath)
	{
		if (string.IsNullOrEmpty(openApiDocumentPath))
			throw new ArgumentException($"'{nameof(openApiDocumentPath)}' cannot be null or empty.", nameof(openApiDocumentPath));

		_openApiDocumentPath = openApiDocumentPath;
	}

	/// <summary>
	/// Creates a new instance of <see cref=" WebServiceTester"/>
	/// </summary>
	/// <param name="openApiDocument">Stream of the OpenApi document</param>
	public WebServiceTester(Stream openApiDocument)
	{
		_openApiDocumentStream = openApiDocument ?? throw new ArgumentNullException(nameof(openApiDocument));
	}

	private async Task ReadOpenApiDocument()
	{
		var canCloseStream = false;
		if (!string.IsNullOrEmpty(_openApiDocumentPath))
		{
			if (!File.Exists(_openApiDocumentPath))
				throw new FileNotFoundException($"OpenApi document '{_openApiDocumentPath}' does not exist!");

			_openApiDocumentStream = File.OpenRead(_openApiDocumentPath);
			canCloseStream = true;
		}

		if (_openApiDocumentStream == null)
			throw new InvalidOperationException("No OpenApi document source specified");

		(_openApiDocument, var diagnostic) = await OpenApiDocument.LoadAsync(_openApiDocumentStream);

		if (canCloseStream && _openApiDocumentStream != null)
			_openApiDocumentStream.Close();

		HandleLoadingErrors(diagnostic);
	}

	/// <summary>
	/// Verifies that all endpoints with security definition returns HTTP 401 if not authentication was given
	/// </summary>
	/// <param name="httpClient"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[AssertionMethod]
	public async Task VerifySecuredEndpointsRequiresAuthentication(HttpClient httpClient, CancellationToken cancellationToken)
	{
		await EnsureDocumentExists();

		foreach (var path in _openApiDocument!.Paths)
		{
			foreach (var operation in path.Value.Operations.Where(o => IsSecured(o.Value)))
			{
				var response = await CallOperation(httpClient, path.Key, operation.Key, operation.Value, cancellationToken);

				if (response.StatusCode != HttpStatusCode.Unauthorized)
					throw new TestFailedException($"Endpoint {response.RequestMessage.Method} {response.RequestMessage.RequestUri} ({operation.Value.Description ?? operation.Value.OperationId}) didn't return HTTP 401");
			}
		}
	}

	/// <summary>
	/// Just for monkey test. Calls every endpoint
	/// </summary>
	/// <param name="httpClient"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[AssertionMethod]
	public async Task CallEveryEndpoint(HttpClient httpClient, CancellationToken cancellationToken, Action<EndpointInformation, HttpResponseMessage>? handleResponseDelegate = null)
	{
		await EnsureDocumentExists();

		foreach (var path in _openApiDocument!.Paths)
		{
			if (path.Value.Operations != null)
			{
				foreach (var operation in path.Value.Operations)
				{
					var response = await CallOperation(httpClient, path.Key, operation.Key, operation.Value, cancellationToken);

					handleResponseDelegate?.Invoke(new EndpointInformation(path.Key, operation.Key, operation.Value), response);
				}
			}
		}
	}

	private async Task<HttpResponseMessage> CallOperation(HttpClient httpClient, string path, HttpMethod httpMethod, OpenApiOperation operation, CancellationToken cancellationToken)
	{
		var request = new HttpRequestMessage(httpMethod, BuildRequestUri(path, httpMethod, operation));

		if (operation.RequestBody?.Content?.Count > 0)
		{
			var firstEntry = operation.RequestBody.Content.First();
			request.Content = CreateRequestContent(operation, firstEntry.Key, firstEntry.Value, path, httpMethod);
		}

		return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
	}

	private HttpContent CreateRequestContent(OpenApiOperation operation, string contentType, OpenApiMediaType content, string path, HttpMethod httpMethod)
	{
		var result = CustomRequestContent?.Invoke(new RequestContentInformation(operation, contentType, content, path, httpMethod));

		if (result != null)
			return result;

		if (contentType == CONTENT_TYPE_MULTIPART_FORM)
			return CreateMultiPartFormContent(content);

		if (contentType == CONTENT_TYPE_FORM)
			return CreateFormContent(content, path, httpMethod, operation);

		// default = json
		return CreateJsonContent(content, path, httpMethod, operation);
	}

	private HttpContent CreateFormContent(OpenApiMediaType content, string path, HttpMethod httpMethod, OpenApiOperation operation)
	{
		var values = new Dictionary<string, string>();

		if (content.Schema?.Properties != null)
		{
			foreach (var prop in content.Schema.Properties)
				values.Add(prop.Key, GenerateParameterValue(prop.Value, prop.Key, path, httpMethod, operation, false));
		}

		return new FormUrlEncodedContent(values);
	}

	private HttpContent CreateJsonContent(OpenApiMediaType content, string path, HttpMethod httpMethod, OpenApiOperation operation)
	{
		var builder = new StringBuilder();
		builder.AppendLine("{");

		var isFirst = true;
		if (content.Schema?.Required != null)
		{
			foreach (var propName in content.Schema.Required)
			{
				if (!isFirst)
					builder.AppendLine(",");

				var prop = content.Schema?.Properties?[propName];
				var value = GenerateParameterValue(prop, propName, path, httpMethod, operation, true);

				if (prop?.Type == JsonSchemaType.Array)
					value = $"[{value}]";

				builder.Append($"\"{propName}\": {value}");

				isFirst = false;
			}
		}

		builder.AppendLine().Append("}");
		return new StringContent(builder.ToString(), Encoding.UTF8, "application/json");
	}

	private static HttpContent CreateMultiPartFormContent(OpenApiMediaType content)
	{
		var form = new MultipartFormDataContent();

		if (content.Schema?.Properties != null)
		{
			foreach (var prop in content.Schema.Properties)
				form.Add(CreateFormData(prop.Value, prop.Key), prop.Key);
		}

		return form;
	}

	private static HttpContent CreateFormData(IOpenApiSchema schema, string name)
	{
		if (schema.Format == "binary" || name == "file")
			return new StreamContent(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Test content")));

		return new StringContent(string.Empty);
	}

	private string BuildRequestUri(string path, HttpMethod httpMethod, OpenApiOperation operation)
	{
		var result = path;

		if (operation.Parameters != null)
		{
			foreach (var param in operation.Parameters.Where(p => p.In == ParameterLocation.Path))
				result = result.Replace($"{{{param.Name}}}", GenerateParameterValue(param, path, httpMethod, operation));
		}

		return result;
	}

	private string GenerateParameterValue(IOpenApiParameter param, string path, HttpMethod httpMethod, OpenApiOperation operation)
		=> GenerateParameterValue(param.Schema, param.Name, path, httpMethod, operation, false);

	private string GenerateParameterValue(IOpenApiSchema? schema, string? parameterName, string path, HttpMethod httpMethod, OpenApiOperation operation, bool encloseStringValues)
	{
		if (schema?.Type == JsonSchemaType.Array)
			return GenerateParameterValue(schema.Items, parameterName, path, httpMethod, operation, encloseStringValues);

		var result = "test";

		if (schema?.Type == JsonSchemaType.Integer)
			result = "1";

		result = CustomParameterValue?.Invoke(new EndpointParameterInformation(path, httpMethod, operation, schema, parameterName)) ?? result;

		if (schema?.Type == JsonSchemaType.String && encloseStringValues)
			result = $"\"{result}\"";

		return result;
	}

	private static bool IsSecured(OpenApiOperation operation)
	{
		return operation.Security?.Count > 0;
	}

	private async Task EnsureDocumentExists()
	{
		if (_openApiDocument == null)
			await ReadOpenApiDocument();

		if (_openApiDocument == null)
			throw new InvalidOperationException("Could not read OpenApi document");
	}

	private static void HandleLoadingErrors(OpenApiDiagnostic? diagnostic)
	{
		if (diagnostic != null && diagnostic.Errors.Count > 0)
		{
			if (diagnostic.Errors.Count == 1 && diagnostic.Errors[0].Message == "Version node not found.")
				throw new OpenApiUnsupportedSpecVersionException("Version node not found.");

			var errors = string.Join(Environment.NewLine, diagnostic.Errors);
			throw new InvalidOperationException($"Could not read OpenApi document.\r\n{errors}");
		}
	}
}
