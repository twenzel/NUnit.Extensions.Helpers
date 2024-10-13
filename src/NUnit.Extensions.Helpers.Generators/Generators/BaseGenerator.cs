using System.Text;

namespace NUnit.Extensions.Helpers.Generators;

public abstract class BaseGenerator : ICustomGenerator
{
	public const string GENERATED_FILE_SUFFIX = ".g.cs";

	public bool AddGenerationInfoHeader { get; set; } = true;
	public bool AddMarkerAttributes { get; set; } = true;

	internal void AddHeader(StringBuilder builder)
	{
		if (AddGenerationInfoHeader)
			builder.AppendLine($"""
// Generated on {DateTimeOffset.UtcNow:yyyy-MM-dd}
#pragma warning disable CS0618 // Type or member is obsolete
""");
	}
}
