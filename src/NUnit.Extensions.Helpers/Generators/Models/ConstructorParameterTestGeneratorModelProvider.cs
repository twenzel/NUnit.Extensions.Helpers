using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Extensions.Helpers.Generators.Internal;

namespace NUnit.Extensions.Helpers.Generators.Models;

internal static class ConstructorParameterTestGeneratorModelProvider
{
	public static ConstructorParameterTestGeneratorModel GetDescriptor(INamedTypeSymbol typeSymbol, List<INamedTypeSymbol> testClasses, bool hasNunitGlobalImport, ClassDeclarationSyntax? targetNode, bool asNestedClass)
	{
		var name = typeSymbol.Name;
		var namespaceName = typeSymbol.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
		var baseType = string.Empty;

		if (typeSymbol.BaseType != null && typeSymbol.BaseType.SpecialType != SpecialType.System_Object)
			baseType = typeSymbol.BaseType.ToDisplayString();

		EquatableList<ClassModel> classes = new();

		foreach (var testClass in testClasses)
		{
			EquatableList<ConstructorModel> constructors = new();
			foreach (var ctor in testClass.InstanceConstructors)
				ResolveConstructors(constructors, ctor);

			if (constructors.Count > 0)
				classes.Add(new ClassModel(testClass.Name, testClass.ContainingNamespace.ToDisplayString(), constructors));
		}

		var isPartialClass = targetNode.IsPartialClass();
		var parentClass = targetNode.GetParentClasses();

		return new ConstructorParameterTestGeneratorModel(name, namespaceName, baseType, classes, hasNunitGlobalImport, isPartialClass, parentClass, asNestedClass);
	}

	private static void ResolveConstructors(EquatableList<ConstructorModel> constructors, IMethodSymbol ctor)
	{
		EquatableList<ParameterModel> parameters = new();

		foreach (var parameter in ctor.Parameters)
		{
			if (!parameter.Type.IsReferenceType) // only constructors with ALL-Reference types are supported
			{
				parameters.Clear();
				break;
			}

			parameters.Add(new ParameterModel(parameter.Name, parameter.Type.ToDisplayString()));
		}

		if (parameters.Count > 0)
			constructors.Add(new ConstructorModel(parameters));
	}
}
