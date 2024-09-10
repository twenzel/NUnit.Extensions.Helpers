using Microsoft.CodeAnalysis;

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
}
