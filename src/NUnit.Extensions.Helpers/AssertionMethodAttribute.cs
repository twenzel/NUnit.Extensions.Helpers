namespace NUnit.Extensions.Helpers;

/// <summary>
/// Marker attribute to fullfill S2699 (https://rules.sonarsource.com/csharp/RSPEC-2699/)
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class AssertionMethodAttribute : Attribute
{
}
