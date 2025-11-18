using Microsoft.OpenApi;

namespace NUnit.Extensions.Helpers;

/// <summary>
/// Provides information about a parameter (in Uri or body)
/// </summary>
public record EndpointParameterInformation : EndpointInformation
{
	/// <summary>
	/// Gets the schema information
	/// </summary>
	public IOpenApiSchema? Schema { get; init; }

	/// <summary>
	/// Gets the parameter name
	/// </summary>
	public string? ParameterName { get; init; }

	public EndpointParameterInformation(string path, HttpMethod operationType, OpenApiOperation operation, IOpenApiSchema? schema, string? name)
		: base(path, operationType, operation)
	{
		Schema = schema;
		ParameterName = name;
	}
}
