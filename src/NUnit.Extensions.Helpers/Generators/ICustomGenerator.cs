namespace NUnit.Extensions.Helpers.Generators;

public interface ICustomGenerator
{
	bool AddGenerationInfoHeader { get; set; }
	bool AddMarkerAttributes { get; set; }
}
