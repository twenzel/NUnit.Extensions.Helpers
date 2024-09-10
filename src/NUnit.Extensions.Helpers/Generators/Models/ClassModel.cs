namespace NUnit.Extensions.Helpers.Generators.Models;

internal record ClassModel(string Name, string NameSpace, EquatableList<ConstructorModel> Constructors);