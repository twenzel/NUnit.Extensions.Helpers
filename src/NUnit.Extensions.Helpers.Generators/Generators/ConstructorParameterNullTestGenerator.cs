using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using NUnit.Extensions.Helpers.Generators.Internal;
using NUnit.Extensions.Helpers.Generators.Models;

namespace NUnit.Extensions.Helpers.Generators;

[Generator]
public partial class ConstructorParameterNullTestGenerator : BaseGenerator, IIncrementalGenerator
{
	/// <summary>
	/// Called to initialize the generator and register generation steps via callbacks
	/// on the <paramref name="context" />
	/// </summary>
	/// <param name="context">The <see cref="T:Microsoft.CodeAnalysis.IncrementalGeneratorInitializationContext" /> to register callbacks on</param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		AddAttributes(context);

		var classesToGenerate =
				context.SyntaxProvider
					.ForAttributeWithMetadataName<ConstructorParameterTestGeneratorModel?>($"{Sources.NAMESPACE}.{Sources.GENERATE_CONSTRUCTOR_PARAMETER_NULL_TESTS_ATTRIBUTE}",
						predicate: static (node, _) => node is ClassDeclarationSyntax,
						transform: GetTypeToGenerate)
					.WithTrackingName(TrackingNames.InitialExtraction);

		context.RegisterSourceOutput(classesToGenerate, Generate);
	}

	private void AddAttributes(IncrementalGeneratorInitializationContext context)
	{
		if (AddMarkerAttributes)
		{
			context.RegisterPostInitializationOutput(context =>
			{
				context.AddSource($"{Sources.GENERATE_CONSTRUCTOR_PARAMETER_NULL_TESTS_ATTRIBUTE}{GENERATED_FILE_SUFFIX}", SourceText.From(Sources.GENERATE_CONSTRUCTOR_PARAMETER_NULL_TESTS_ATTRIBUTE_SOURCE, Encoding.UTF8));
			});
		}
	}

	static ConstructorParameterTestGeneratorModel? GetTypeToGenerate(GeneratorAttributeSyntaxContext context, CancellationToken ct)
	{
		var list = new List<INamedTypeSymbol>();
		var asNestedClass = false;

		if (context.TargetSymbol is not INamedTypeSymbol targetTypeSymbol)
			return null;

		foreach (var attribute in context.Attributes)
		{
			if (attribute.ConstructorArguments.Length == 0)
				continue;

			if (attribute.ConstructorArguments[0].Value is INamedTypeSymbol typeSymbol)
				list.Add(typeSymbol);

			foreach (var namedArgument in attribute.NamedArguments)
			{
				if (namedArgument.Key == Sources.ARGUMENT_NAME_ASNESTEDCLASS && namedArgument.Value.ToCSharpString() == "true")
					asNestedClass = true;
			}
		}

		ct.ThrowIfCancellationRequested();

		var hasNunitGlobalImport = DetermineNUnitGlobalImport(context.SemanticModel.Compilation);

		return ConstructorParameterTestGeneratorModelProvider.GetDescriptor(targetTypeSymbol, list, hasNunitGlobalImport, context.TargetNode as ClassDeclarationSyntax, asNestedClass);
	}

	private static bool DetermineNUnitGlobalImport(Compilation compilation)
	{
		if (compilation is CSharpCompilation sharpCompilation && sharpCompilation.Options.Usings.Contains("NUnit.Framework"))
			return true;

		return false;
	}

	void Generate(SourceProductionContext spc, ConstructorParameterTestGeneratorModel? testDescriptor)
	{
		if (testDescriptor is { } test)
		{
			try
			{
				OnGenerate(spc, test);
			}
			catch (Exception ex)
			{
				spc.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.Error, null, $"({test.ClassName}) {ex.Message}"));
			}
		}
	}

	private void OnGenerate(SourceProductionContext context, ConstructorParameterTestGeneratorModel testToGenerate)
	{
		var cancellationToken = context.CancellationToken;
		if (cancellationToken.IsCancellationRequested)
			return;

		ReportErrors(context, testToGenerate);

		var stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("// <auto-generated/>");
		AddHeader(stringBuilder);
		stringBuilder.AppendLine("using System;");
		stringBuilder.AppendLine("using FluentAssertions;");
		stringBuilder.AppendLine("using Moq;");

		if (!testToGenerate.HasNunitGlobalImport)
			stringBuilder.AppendLine("using NUnit.Framework;");

		stringBuilder.AppendLine();
		stringBuilder.AppendLine($"namespace {testToGenerate.NameSpace};");

		var parentsCount = AddParentClasses(stringBuilder, testToGenerate.ParentClass);

		AddIntent(stringBuilder, parentsCount);
		stringBuilder.Append($"partial class {testToGenerate.ClassName}");

		if (!string.IsNullOrEmpty(testToGenerate.BaseType))
			stringBuilder.AppendLine($" : {testToGenerate.BaseType}");
		else
			stringBuilder.AppendLine();

		AddIntent(stringBuilder, parentsCount);
		stringBuilder.AppendLine("{");

		if (testToGenerate.AsNestedClass)
		{
			parentsCount++;
			AddIntent(stringBuilder, parentsCount);
			stringBuilder.AppendLine("class ConstructorParameterNullTests");
			AddIntent(stringBuilder, parentsCount);
			stringBuilder.AppendLine("{");
		}

		AddTests(testToGenerate, stringBuilder, parentsCount);

		AddIntent(stringBuilder, parentsCount);
		stringBuilder.AppendLine("}");
		CloseParentClasses(stringBuilder, parentsCount);

		string fileName;

		if (!string.IsNullOrEmpty(testToGenerate.BaseType))
			fileName = $"{testToGenerate.BaseType}.{testToGenerate.ClassName}";
		else
			fileName = $"{testToGenerate.NameSpace}.{testToGenerate.ClassName}";

		context.AddSource($"{fileName}_CPT{GENERATED_FILE_SUFFIX}", SourceText.From(stringBuilder.ToString(), Encoding.UTF8));

	}

	private static void CloseParentClasses(StringBuilder stringBuilder, int parentsCount)
	{
		// We need to "close" each of the parent types, so write
		// the required number of '}'
		for (var i = 0; i < parentsCount; i++)
		{
			AddIntent(stringBuilder, i);
			stringBuilder.AppendLine("}");
		}
	}

	private static int AddParentClasses(StringBuilder stringBuilder, ParentClass? parentClass)
	{
		var parentsCount = 0;

		// Loop through the full parent type hiearchy, starting with the outermost
		while (parentClass is not null)
		{
			AddIntent(stringBuilder, parentsCount);
			stringBuilder
				.Append("partial ")
				.Append(parentClass.Keyword) // e.g. class/struct/record
				.Append(' ')
				.Append(parentClass.Name); // e.g. Outer/Generic<T>

			if (!string.IsNullOrEmpty(parentClass.Constraints))
				stringBuilder.Append(' ')
				.Append(parentClass.Constraints); // e.g. where T: new()

			stringBuilder.AppendLine();

			AddIntent(stringBuilder, parentsCount);
			stringBuilder.AppendLine("{");
			parentsCount++; // keep track of how many layers deep we are
			parentClass = parentClass.Child; // repeat with the next child
		}

		return parentsCount;
	}

	private static void AddIntent(StringBuilder stringBuilder, int level)
	{
		for (var i = 0; i < level; i++)
			stringBuilder.Append('\t');
	}

	private static string GetIndent(int level)
	{
		var builder = new StringBuilder();
		AddIntent(builder, level);
		return builder.ToString();
	}

	private static void AddTests(ConstructorParameterTestGeneratorModel testToGenerate, StringBuilder stringBuilder, int indentLevel)
	{
		var indent = GetIndent(indentLevel + 1);
		foreach (var classToGenerate in testToGenerate.TestClasses)
		{
			foreach (var constructors in classToGenerate.Constructors)
			{
				foreach (var parameter in constructors.Parameters)
				{
					var parameterName = FirstCharUpper(parameter.ParameterName);
					var parameterValues = GetParameterValues(constructors.Parameters, parameter);

					stringBuilder.AppendLine($$"""
{{indent}}[Test]
{{indent}}public void Throws_Exception_When_{{parameterName}}_Is_Null()
{{indent}}{
{{indent}}	Action action = () => new {{classToGenerate.Name}}({{parameterValues}});
{{indent}}	action.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("{{parameter.ParameterName}}");
{{indent}}}

""");
				}
			}
		}
	}

	private static string GetParameterValues(EquatableList<ParameterModel> parameters, ParameterModel currentParameter)
	{
		var builder = new StringBuilder();
		var useNull = false;

		foreach (var parameter in parameters)
		{
			if (builder.Length > 0)
				builder.Append(", ");

			if (parameter == currentParameter)
				useNull = true;

			if (useNull)
				builder.Append("null");
			else
				builder.Append($"Mock.Of<{parameter.TypeName}>()");
		}

		return builder.ToString();
	}

	private static string FirstCharUpper(string value)
	{
		if (value == null)
			throw new ArgumentNullException(nameof(value), "The given string should not be empty to convert to pascal case.");

		if (value.Length == 1)
			return value.ToUpper();

		return char.ToUpper(value[0]) + value.Substring(1);
	}

	internal static void ReportErrors(SourceProductionContext context, ConstructorParameterTestGeneratorModel testToGenerate)
	{
		if (!testToGenerate.IsPartialClass)
		{
			var diagnostic = Diagnostic.Create(DiagnosticDescriptors.NoPartialClass, null, GetFullQualifiedClassName(testToGenerate));

			context.ReportDiagnostic(diagnostic);
		}
	}

	private static string GetFullQualifiedClassName(ConstructorParameterTestGeneratorModel testToGenerate)
	{
		var builder = new StringBuilder();
		builder.Append($"{testToGenerate.NameSpace}.");

		var parentClass = testToGenerate.ParentClass;

		while (parentClass is not null)
		{
			builder.Append($"{parentClass.Name}.");
			parentClass = parentClass.Child;
		}

		builder.Append(testToGenerate.ClassName);
		return builder.ToString();
	}
}
