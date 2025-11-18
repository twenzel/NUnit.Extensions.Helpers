using Microsoft.OpenApi;

namespace NUnit.Extensions.Helpers;

public record RequestContentInformation(OpenApiOperation Operation, string ContentType, IOpenApiMediaType Content, string Path, HttpMethod OperationType);
