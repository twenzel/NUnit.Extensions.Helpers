using Microsoft.OpenApi.Models;

namespace NUnit.Extensions.Helpers;

public record RequestContentInformation(OpenApiOperation Operation, string ContentType, OpenApiMediaType Content, string Path, OperationType OperationType);
