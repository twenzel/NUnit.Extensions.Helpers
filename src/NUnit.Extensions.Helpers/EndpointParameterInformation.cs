using Microsoft.OpenApi.Models;

namespace NUnit.Extensions.Helpers;

public record EndpointParameterInformation
{
	/// <summary>
	/// Gets the path of the endpoint
	/// </summary>
	public string Path { get; init; } = string.Empty;

	/// <summary>
	/// Gets the endpoint operation type (POST, GET,...)
	/// </summary>
	public OperationType OperationType { get; }

	/// <summary>
	/// Gets the operation information
	/// </summary>
	public OpenApiOperation Operation { get; init; }

	/// <summary>
	/// Gets the parameter information
	/// </summary>
	public OpenApiParameter Parameter { get; init; }

	public EndpointParameterInformation(string path, OperationType operationType, OpenApiOperation operation, OpenApiParameter parameter)
	{
		Path = path;
		OperationType = operationType;
		Operation = operation;
		Parameter = parameter;
	}
}
