using System.Net;
using System.Text;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace NUnit.Extensions.Helpers;

/// <summary>
/// Helper class to execute tests for webservices
/// </summary>
public class WebServiceTester
{
	private const string CONTENT_TYPE_MULTIPART_FORM = "multipart/form-data";
	private const string CONTENT_TYPE_FORM = "application/x-www-form-urlencoded";
	private const string SCHEMA_TYPE_FILE = "file";
	private const string SCHEMA_TYPE_INTEGER = "integer";
	private const string SCHEMA_TYPE_STRING = "string";
	private const string SCHEMA_TYPE_ARRAY = "array";
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

	private void ReadOpenApiDocument()
	{
		var canCloseStream = false;
		if (!string.IsNullOrEmpty(_openApiDocumentPath))
		{
			if (!File.Exists(_openApiDocumentPath))
				throw new FileNotFoundException($"OpenApi document '{_openApiDocumentPath}' does not exist!");

			_openApiDocumentStream = File.OpenRead(_openApiDocumentPath);
			canCloseStream = true;
		}

		_openApiDocument = new OpenApiStreamReader().Read(_openApiDocumentStream, out var diagnostic);

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
		EnsureDocumentExists();

		foreach (var path in _openApiDocument!.Paths)
		{
			foreach (var operation in path.Value.Operations.Where(o => IsSecured(o.Value)))
			{
				var response = await CallOperation(httpClient, path.Key, operation.Key, operation.Value, cancellationToken);

				if (response.StatusCode != HttpStatusCode.Unauthorized)
					throw new TestFailedException($"Endpoint {operation.Key} {operation.Value} did'nt return HTTP 401");
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
		EnsureDocumentExists();

		foreach (var path in _openApiDocument!.Paths)
		{
			foreach (var operation in path.Value.Operations)
			{
				var response = await CallOperation(httpClient, path.Key, operation.Key, operation.Value, cancellationToken);

				handleResponseDelegate?.Invoke(new EndpointInformation(path.Key, operation.Key, operation.Value), response);
			}
		}
	}

	private async Task<HttpResponseMessage> CallOperation(HttpClient httpClient, string path, OperationType operationType, OpenApiOperation operation, CancellationToken cancellationToken)
	{
		var request = new HttpRequestMessage(GetMethod(operationType), BuildRequestUri(path, operationType, operation));

		if (operation.RequestBody != null && operation.RequestBody.Content.Count > 0)
		{
			var firstEntry = operation.RequestBody.Content.First();
			request.Content = CreateRequestContent(operation, firstEntry.Key, firstEntry.Value, path, operationType);
		}

		return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
	}

	private HttpContent CreateRequestContent(OpenApiOperation operation, string contentType, OpenApiMediaType content, string path, OperationType operationType)
	{
		var result = CustomRequestContent?.Invoke(new RequestContentInformation(operation, contentType, content, path, operationType));

		if (result != null)
			return result;

		if (contentType == CONTENT_TYPE_MULTIPART_FORM)
			return CreateMultiPartFormContent(content);

		if (contentType == CONTENT_TYPE_FORM)
			return CreateFormContent(content, path, operationType, operation);

		// default = json
		return CreateJsonContent(content, path, operationType, operation);
	}

	private HttpContent CreateFormContent(OpenApiMediaType content, string path, OperationType operationType, OpenApiOperation operation)
	{
		var values = new Dictionary<string, string>();

		foreach (var prop in content.Schema.Properties)
			values.Add(prop.Key, GenerateParameterValue(prop.Value, prop.Key, path, operationType, operation, false));

		return new FormUrlEncodedContent(values);
	}

	private HttpContent CreateJsonContent(OpenApiMediaType content, string path, OperationType operationType, OpenApiOperation operation)
	{
		var builder = new StringBuilder();
		builder.AppendLine("{");

		var isFirst = true;
		foreach (var propName in content.Schema.Required)
		{
			if (!isFirst)
				builder.AppendLine(",");

			var prop = content.Schema.Properties[propName];
			var value = GenerateParameterValue(prop, propName, path, operationType, operation, true);

			if (prop.Type == SCHEMA_TYPE_ARRAY)
				value = $"[{value}]";

			builder.Append($"\"{propName}\": {value}");

			isFirst = false;
		}

		builder.AppendLine().Append("}");
		return new StringContent(builder.ToString(), Encoding.UTF8, "application/json");
	}

	private static HttpContent CreateMultiPartFormContent(OpenApiMediaType content)
	{
		var form = new MultipartFormDataContent();

		foreach (var prop in content.Schema.Properties)
			form.Add(CreateFormData(prop.Key, prop.Value), prop.Key);

		return form;
	}

	private static HttpContent CreateFormData(string partName, OpenApiSchema schema)
	{
		if (schema.Type == SCHEMA_TYPE_FILE)
			return new StreamContent(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Test content")));

		return new StringContent(string.Empty);
	}

	private string BuildRequestUri(string path, OperationType operationType, OpenApiOperation operation)
	{
		var result = path;

		foreach (var param in operation.Parameters.Where(p => p.In == ParameterLocation.Path))
			result = result.Replace($"{{{param.Name}}}", GenerateParameterValue(param, path, operationType, operation));

		return result;
	}

	private string GenerateParameterValue(OpenApiParameter param, string path, OperationType operationType, OpenApiOperation operation)
		=> GenerateParameterValue(param.Schema, param.Name, path, operationType, operation, false);

	private string GenerateParameterValue(OpenApiSchema schema, string parameterName, string path, OperationType operationType, OpenApiOperation operation, bool encloseStringValues)
	{
		if (schema.Type == SCHEMA_TYPE_ARRAY)
			return GenerateParameterValue(schema.Items, parameterName, path, operationType, operation, encloseStringValues);

		var result = "test";

		if (schema.Type == SCHEMA_TYPE_INTEGER)
			result = "1";

		result = CustomParameterValue?.Invoke(new EndpointParameterInformation(path, operationType, operation, schema, parameterName)) ?? result;

		if (schema.Type == SCHEMA_TYPE_STRING && encloseStringValues)
			result = $"\"{result}\"";

		return result;
	}

	private static HttpMethod GetMethod(OperationType operationType)
	{
		return operationType switch
		{
			OperationType.Get => HttpMethod.Get,
			OperationType.Put => HttpMethod.Put,
			OperationType.Post => HttpMethod.Post,
			OperationType.Delete => HttpMethod.Delete,
			OperationType.Options => HttpMethod.Options,
			OperationType.Head => HttpMethod.Head,
			OperationType.Patch => new HttpMethod("PATCH"),
			OperationType.Trace => HttpMethod.Trace,
			_ => throw new InvalidOperationException($"Operation type {operationType} is not supported"),
		};
	}

	private static bool IsSecured(OpenApiOperation operation)
	{
		return operation.Security.Count > 0;
	}

	private void EnsureDocumentExists()
	{
		if (_openApiDocument == null)
			ReadOpenApiDocument();

		if (_openApiDocument == null)
			throw new InvalidOperationException("Could not read OpenApi document");
	}

	private static void HandleLoadingErrors(OpenApiDiagnostic? diagnostic)
	{
		if (diagnostic != null && diagnostic.Errors.Count > 0)
		{
			var errors = string.Join(Environment.NewLine, diagnostic.Errors);
			throw new InvalidOperationException($"Could not read OpenApi document.\r\n{errors}");
		}
	}
}
