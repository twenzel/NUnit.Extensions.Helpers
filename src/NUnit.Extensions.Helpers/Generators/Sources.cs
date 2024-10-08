namespace NUnit.Extensions.Helpers.Generators;

internal static class Sources
{
	public const string GENERATE_CONSTRUCTOR_PARAMETER_NULL_TESTS_ATTRIBUTE = "GenerateConstructorParameterNullTestsAttribute";
	public const string NAMESPACE = "NUnit.Framework";
	public const string ARGUMENT_NAME_ASNESTEDCLASS = "AsNestedClass";

	public static readonly string GENERATE_CONSTRUCTOR_PARAMETER_NULL_TESTS_ATTRIBUTE_SOURCE = $$"""
// <auto-generated/>
using System;
namespace {{NAMESPACE}};

/// <summary>
/// Enables the generation of constructor parameter tests
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
internal sealed class {{GENERATE_CONSTRUCTOR_PARAMETER_NULL_TESTS_ATTRIBUTE}}: Attribute
{
    /// <summary>
    /// Gets the type which constructor tests should be generated for
    /// </summary>
    public Type TestClassType { get; }

	/// <summary>
    /// Gets or sets whether the test methods are generated within a nested class
    /// </summary>
	public bool {{ARGUMENT_NAME_ASNESTEDCLASS}} { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="{{GENERATE_CONSTRUCTOR_PARAMETER_NULL_TESTS_ATTRIBUTE}}"/> class.
    /// </summary>
    /// <param name="testClassType">Type of class to generate tests for</param>
    public {{GENERATE_CONSTRUCTOR_PARAMETER_NULL_TESTS_ATTRIBUTE}}(Type testClassType)
    {
	    TestClassType = testClassType ?? throw new ArgumentNullException(nameof(testClassType));
    }
}

""";
}