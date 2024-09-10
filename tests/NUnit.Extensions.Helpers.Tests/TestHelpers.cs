using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Extensions.Helpers.Generators;
using NUnit.Extensions.Helpers.Generators.Internal;

namespace NUnit.Extensions.Helpers.Tests;

internal static class TestHelpers
{
	public static CSharpCompilation CreateCSharpCompilation(string source, string additionalSource = "")
	{
		var netCoreReferencePath = Path.GetDirectoryName(typeof(object).Assembly.Location) + Path.DirectorySeparatorChar;

		return CSharpCompilation.Create("compilation",
	   [CSharpSyntaxTree.ParseText(source, cancellationToken: CancellationToken.None), CSharpSyntaxTree.ParseText(additionalSource, cancellationToken: CancellationToken.None)],
	   [
		   MetadataReference.CreateFromFile(netCoreReferencePath + "System.Private.CoreLib.dll"),
		   MetadataReference.CreateFromFile(netCoreReferencePath + "System.Runtime.dll"),
	   ],
	   new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
	}

	public static GeneratorRunResult GenerateAndValidateCSharpOutput<TGenerator>(Compilation inputCompilation, int expectedSourcesCount, bool assertCaching = false) where TGenerator : ICustomGenerator, IIncrementalGenerator, new()
	{
		// ⚠ Tell the driver to track all the incremental generator outputs
		// without this, you'll have no tracked outputs!
		var opts = new GeneratorDriverOptions(
			disabledOutputs: IncrementalGeneratorOutputKind.None,
			trackIncrementalGeneratorSteps: true);

		return GenerateAndValidateOutput<TGenerator>(inputCompilation, (g) => CSharpGeneratorDriver.Create([g.AsSourceGenerator()], driverOptions: opts), expectedSourcesCount, assertCaching);
	}
	private static GeneratorRunResult GenerateAndValidateOutput<TGenerator>(Compilation inputCompilation, Func<IIncrementalGenerator, GeneratorDriver> generateDriver, int expectedSourcesCount, bool assertCaching) where TGenerator : ICustomGenerator, IIncrementalGenerator, new()
	{
		// get all the const string fields on the TrackingName type
		string[] trackingNames = [TrackingNames.InitialExtraction];

		return GenerateAndValidateOutput<TGenerator>(inputCompilation, generateDriver, expectedSourcesCount, trackingNames, assertCaching);
	}

	private static GeneratorRunResult GenerateAndValidateOutput<TGenerator>(Compilation inputCompilation, Func<IIncrementalGenerator, GeneratorDriver> generateDriver, int expectedSourcesCount, string[] trackingNames, bool assertCaching) where TGenerator : ICustomGenerator, IIncrementalGenerator, new()
	{
		// Ensure compilation has no errors
		var compilationDiagnostics = inputCompilation.GetDiagnostics(CancellationToken.None).Where(d => d.Severity == DiagnosticSeverity.Error);
		compilationDiagnostics.Should().HaveCount(0, string.Join(Environment.NewLine, compilationDiagnostics.Select(d => d.GetMessage())));

		// directly create an instance of the generator
		// (Note: in the compiler this is loaded from an assembly, and created via reflection at runtime)
		var generator = new TGenerator();
		generator.AddGenerationInfoHeader = false;
		generator.AddMarkerAttributes = false;

		// Create the driver that will control the generation, passing in our generator
		GeneratorDriver driver = generateDriver(generator);

		// Create a clone of the compilation that we will use later
		var clone = inputCompilation.Clone();

		// Run the generation pass
		// (Note: the generator driver itself is immutable, and all calls return an updated version of the driver that you should use for subsequent calls)
		driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics, CancellationToken.None);

		// We can now assert things about the resulting compilation:
		diagnostics.IsEmpty.Should().BeTrue(string.Join(Environment.NewLine, diagnostics.Select(d => d.GetMessage()))); // there were no diagnostics created by the generators

		// Or we can look at the results directly:
		var runResult = driver.GetRunResult();

		// The runResult contains the combined results of all generators passed to the driver
		runResult.GeneratedTrees.Should().HaveCount(expectedSourcesCount);
		runResult.Diagnostics.IsEmpty.Should().BeTrue();

		// Or you can access the individual results on a by-generator basis
		var generatorResult = runResult.Results[0];
		generatorResult.Diagnostics.IsEmpty.Should().BeTrue();
		generatorResult.GeneratedSources.Should().HaveCount(expectedSourcesCount);
		generatorResult.Exception.Should().BeNull();

		// assert caching
		if (assertCaching)
		{
			// Run again, using the same driver, with a clone of the compilation
			var runResult2 = driver.RunGenerators(clone, CancellationToken.None).GetRunResult();

			// Compare all the tracked outputs, throw if there's a failure
			AssertRunsEqual(runResult, runResult2, trackingNames);

			// verify the second run only generated cached source outputs
			runResult2.Results[0]
						.TrackedOutputSteps
						.SelectMany(x => x.Value) // step executions
						.SelectMany(x => x.Outputs) // execution results
						.Should()
						.OnlyContain(x => x.Reason == IncrementalStepRunReason.Cached);
		}

		return generatorResult;
	}

	/// <summary>
	/// From https://andrewlock.net/creating-a-source-generator-part-10-testing-your-incremental-generator-pipeline-outputs-are-cacheable/
	/// </summary>
	private static void AssertRunsEqual(GeneratorDriverRunResult runResult1, GeneratorDriverRunResult runResult2, string[] trackingNames)
	{
		// We're given all the tracking names, but not all the
		// stages will necessarily execute, so extract all the 
		// output steps, and filter to ones we know about
		var trackedSteps1 = GetTrackedSteps(runResult1, trackingNames);
		var trackedSteps2 = GetTrackedSteps(runResult2, trackingNames);

		// Both runs should have the same tracked steps
		trackedSteps1.Should()
					 .NotBeEmpty()
					 .And.HaveSameCount(trackedSteps2)
					 .And.ContainKeys(trackedSteps2.Keys);

		// Get the IncrementalGeneratorRunStep collection for each run
		foreach (var (trackingName, runSteps1) in trackedSteps1)
		{
			// Assert that both runs produced the same outputs
			var runSteps2 = trackedSteps2[trackingName];
			AssertEqual(runSteps1, runSteps2, trackingName);
		}


		// Local function that extracts the tracked steps
		static Dictionary<string, ImmutableArray<IncrementalGeneratorRunStep>> GetTrackedSteps(
			GeneratorDriverRunResult runResult, string[] trackingNames)
			=> runResult
					.Results[0] // We're only running a single generator, so this is safe
					.TrackedSteps // Get the pipeline outputs
					.Where(step => trackingNames.Contains(step.Key)) // filter to known steps
					.ToDictionary(x => x.Key, x => x.Value); // Convert to a dictionary
	}

	private static void AssertEqual(ImmutableArray<IncrementalGeneratorRunStep> runSteps1, ImmutableArray<IncrementalGeneratorRunStep> runSteps2, string stepName)
	{
		runSteps1.Should().HaveSameCount(runSteps2);

		for (var i = 0; i < runSteps1.Length; i++)
		{
			var runStep1 = runSteps1[i];
			var runStep2 = runSteps2[i];

			// The outputs should be equal between different runs
			var outputs1 = runStep1.Outputs.Select(x => x.Value);
			var outputs2 = runStep2.Outputs.Select(x => x.Value);

			outputs1.Should()
					.Equal(outputs2, $"because {stepName} should produce cacheable outputs");

			// Therefore, on the second run the results should always be cached or unchanged!
			// - Unchanged is when the _input_ has changed, but the output hasn't
			// - Cached is when the the input has not changed, so the cached output is used 
			runStep2.Outputs.Should()
				.OnlyContain(
					x => x.Reason == IncrementalStepRunReason.Cached || x.Reason == IncrementalStepRunReason.Unchanged,
					$"{stepName} expected to have reason {IncrementalStepRunReason.Cached} or {IncrementalStepRunReason.Unchanged}");

			// Make sure we're not using anything we shouldn't
			AssertObjectGraph(runStep1, stepName);
		}
	}

	static private void AssertObjectGraph(IncrementalGeneratorRunStep runStep, string stepName)
	{
		// Including the stepName in error messages to make it easy to isolate issues
		var because = $"{stepName} shouldn't contain banned symbols";

		// Check all of the outputs - probably overkill, but why not
		foreach (var (obj, _) in runStep.Outputs)
		{
			Visit(obj);
		}

		void Visit(object? node)
		{
			if (node is null)
				return;

			// Make sure it's not a banned type
			node.Should()
				.NotBeOfType<Compilation>(because)
				.And.NotBeOfType<ISymbol>(because)
				.And.NotBeOfType<SyntaxNode>(because);

			// Examine the object
			Type type = node.GetType();
			if (type.IsPrimitive || type.IsEnum || type == typeof(string))
				return;

			// If the object is a collection, check each of the values
			if (node is System.Collections.IEnumerable collection and not string)
			{
				foreach (var element in collection)
				{
					// recursively check each element in the collection
					Visit(element);
				}

				return;
			}

			// Recursively check each field in the object
			foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
			{
				var fieldValue = field.GetValue(node);
				Visit(fieldValue);
			}
		}
	}
}
