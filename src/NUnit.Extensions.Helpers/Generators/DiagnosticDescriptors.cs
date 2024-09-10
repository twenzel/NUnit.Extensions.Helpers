using Microsoft.CodeAnalysis;

namespace NUnit.Extensions.Helpers.Generators;

internal static class DiagnosticDescriptors
{
	private const string CATEGORY = "NUnit.Extensions.CodeGenerators";

	public static readonly DiagnosticDescriptor Error = new(
	   "NEG100",
	   "Error on generating code",
	   "Error on generating code for type: {0}",
	   CATEGORY,
	   DiagnosticSeverity.Error,
	   true
   );
}