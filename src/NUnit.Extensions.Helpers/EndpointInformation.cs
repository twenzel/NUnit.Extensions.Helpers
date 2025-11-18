using Microsoft.OpenApi;

namespace NUnit.Extensions.Helpers;

/// <summary>
/// Provides information about an endpoint
/// </summary>
public record EndpointInformation
{
	/// <summary>
	/// Gets the path of the endpoint
	/// </summary>
	public string Path { get; init; } = string.Empty;

	/// <summary>
	/// Gets the endpoint operation type (POST, GET,...)
	/// </summary>
	public HttpMethod OperationType { get; }

	/// <summary>
	/// Gets the operation information
	/// </summary>
	public OpenApiOperation Operation { get; init; }

	/// <summary>
	/// Creates a new instance of <see cref="EndpointInformation"/>
	/// </summary>
	/// <param name="path">The defined path</param>
	/// <param name="operationType">The endpoint operation type (POST, GET,...)</param>
	/// <param name="operation">The operation information</param>
	public EndpointInformation(string path, HttpMethod operationType, OpenApiOperation operation)
	{
		Path = path;
		OperationType = operationType;
		Operation = operation;
	}
}
