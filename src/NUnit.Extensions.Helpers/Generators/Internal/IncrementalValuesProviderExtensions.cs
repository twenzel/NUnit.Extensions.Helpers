using Microsoft.CodeAnalysis;

namespace NUnit.Extensions.Helpers.Generators.Internal;

internal static class IncrementalValuesProviderExtensions
{
	public static IncrementalValuesProvider<TSource> WhereNotNull<TSource>(this IncrementalValuesProvider<TSource?> source) where TSource : class
	{
#nullable disable
		return source.Where(x => x != null);
#nullable enable
	}

	/// <summary>
	/// Registers an output node into an <see cref="IncrementalGeneratorInitializationContext"/> to output diagnostics.
	/// </summary>
	/// <param name="context">The input <see cref="IncrementalGeneratorInitializationContext"/> instance.</param>
	/// <param name="diagnostics">The input <see cref="IncrementalValuesProvider{TValues}"/> sequence of diagnostics.</param>
	public static void ReportDiagnostics(this IncrementalGeneratorInitializationContext context, IncrementalValueProvider<IEnumerable<Diagnostic>> diagnostics
	)
	{
		context.RegisterSourceOutput(
			diagnostics,
			static (context, diagnostics) =>
			{
				foreach (var diagnostic in diagnostics)
				{
					context.ReportDiagnostic(diagnostic);
				}
			}
		);
	}
}