using Microsoft.OpenApi;

namespace NUnit.Extensions.Helpers;

public record RequestContentInformation(OpenApiOperation Operation, string ContentType, OpenApiMediaType Content, string Path, HttpMethod HttpMethod);
