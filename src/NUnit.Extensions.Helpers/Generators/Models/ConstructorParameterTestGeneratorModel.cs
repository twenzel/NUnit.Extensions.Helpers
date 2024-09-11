namespace NUnit.Extensions.Helpers.Generators.Models;

internal record ConstructorParameterTestGeneratorModel(string ClassName, string NameSpace, string BaseType, EquatableList<ClassModel> TestClasses, bool HasNunitGlobalImport);
