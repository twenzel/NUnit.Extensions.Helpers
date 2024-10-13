using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Extensions.Helpers.Generators.Models;

namespace NUnit.Extensions.Helpers.Generators.Internal;

internal static class RoslynExtensions
{
	public static bool TryGetValue<T>(this INamedTypeSymbol symbol, string attributeName, string name, out T? value)
	{
		value = default;
		var attributes = symbol.GetAttributes().Where(a => a.AttributeClass?.Name == attributeName);
		foreach (var attribute in attributes)
		{
			if (attribute.TryGetValue(name, out value))
				return true;
		}
		return false;
	}

	public static bool TryGetValue<T>(this AttributeData? attributeData, string name, out T? value)
	{
		value = default;
		if (attributeData == null)
			return false;

		var names = attributeData
						.AttributeConstructor
						?.Parameters
						.Select(p => p.Name)
						.ToArray() ?? [];
		var i = 0;
		foreach (var parameter in attributeData.ConstructorArguments)
		{
			if (string.Compare(names[i], name, true) != 0)
				continue;

			value = (T?)parameter.Value;
			return true;
		}

		var prop = attributeData.NamedArguments.FirstOrDefault(m => m.Key == name);
		var val = prop.Value;
		if (val.IsNull)
			return false;

		value = (T?)val.Value;
		return true;
	}

	public static bool IsPartialClass(this SyntaxNode? node)
	{
		if (node is ClassDeclarationSyntax classDeclaration)
			return classDeclaration.Modifiers.IndexOf(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword) > -1;

		return false;
	}

	public static ParentClass? GetParentClasses(this BaseTypeDeclarationSyntax? typeSyntax)
	{
		if (typeSyntax == null)
			return null;

		// Try and get the parent syntax. If it isn't a type like class/struct, this will be null
		var parentSyntax = typeSyntax.Parent as TypeDeclarationSyntax;
		ParentClass? parentClassInfo = null;

		// Keep looping while we're in a supported nested type
		while (parentSyntax != null && IsAllowedKind(parentSyntax.Kind()))
		{
			// Record the parent type keyword (class/struct etc), name, and constraints
			parentClassInfo = new ParentClass(
				Keyword: parentSyntax.Keyword.ValueText,
				Name: parentSyntax.Identifier.ToString() + parentSyntax.TypeParameterList,
				Constraints: parentSyntax.ConstraintClauses.ToString(),
				Child: parentClassInfo); // set the child link (null initially)

			// Move to the next outer type
			parentSyntax = (parentSyntax.Parent as TypeDeclarationSyntax);
		}

		// return a link to the outermost parent type
		return parentClassInfo;

	}

	// We can only be nested in class/struct/record
	static bool IsAllowedKind(SyntaxKind kind) =>
		kind == SyntaxKind.ClassDeclaration ||
		kind == SyntaxKind.StructDeclaration ||
		kind == SyntaxKind.RecordDeclaration;
}
