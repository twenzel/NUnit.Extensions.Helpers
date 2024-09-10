using Microsoft.CodeAnalysis;

namespace NUnit.Extensions.Helpers.Generators.Models;

internal static class ConstructorParameterTestGeneratorModelProvider
{
	public static ConstructorParameterTestGeneratorModel GetDescriptor(INamedTypeSymbol typeSymbol, List<INamedTypeSymbol> testClasses)
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

			if (constructors.Count > 0)
				classes.Add(new ClassModel(testClass.Name, testClass.ContainingNamespace.ToDisplayString(), constructors));
		}

		return new ConstructorParameterTestGeneratorModel(name, namespaceName, baseType, classes);
	}
}
