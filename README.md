# NUnit.Extensions.Helpers

[![NuGet](https://img.shields.io/nuget/v/NUnit.Extensions.Helpers.svg)](https://nuget.org/packages/NUnit.Extensions.Helpers/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Build Status](https://github.com/twenzel/NUnit.Extensions.Helpers/workflows/CI/badge.svg?branch=main)](https://github.com/twenzel/NUnit.Extensions.Helpers/actions)

[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=twenzel_NUnit.Extensions.Helpers&metric=sqale_rating)](https://sonarcloud.io/dashboard?id=twenzel_NUnit.Extensions.Helpers)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=twenzel_NUnit.Extensions.Helpers&metric=reliability_rating)](https://sonarcloud.io/dashboard?id=twenzel_NUnit.Extensions.Helpers)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=twenzel_NUnit.Extensions.Helpers&metric=security_rating)](https://sonarcloud.io/dashboard?id=twenzel_NUnit.Extensions.Helpers)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=twenzel_NUnit.Extensions.Helpers&metric=bugs)](https://sonarcloud.io/dashboard?id=twenzel_NUnit.Extensions.Helpers)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=twenzel_NUnit.Extensions.Helpers&metric=vulnerabilities)](https://sonarcloud.io/dashboard?id=twenzel_NUnit.Extensions.Helpers)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=twenzel_NUnit.Extensions.Helpers&metric=coverage)](https://sonarcloud.io/dashboard?id=twenzel_NUnit.Extensions.Helpers)


Helpers to generate NUnit tests

> This is not an official [NUnit](https://github.com/nunit/nunit) package.

## Install

Add the NuGet package [NUnit.Extensions.Helpers](https://nuget.org/packages/NUnit.Extensions.Helpers/) to any project supporting .NET Standard 2.0 or higher.

> &gt; dotnet add package NUnit.Extensions.Helpers

### Information

> Currently the generated source requires [Moq](https://nuget.org/packages/Moq) and [Shouldly](https://nuget.org/packages/Shouldly).

## Usage

### Constructor parameter null tests

Use the `GenerateConstructorParameterNullTests` attribute to define the SUT (class which should be tested) to generate constructor parameter tests for.

```csharp
[GenerateConstructorParameterNullTests(typeof(Document))]
internal partial class DocumentTests
{
    [Test]
    public void Test1()
    {
        Assert.Pass();
    }
}
```

If the SUT looks like

```csharp
public class Document
{
    private Stream _stream;
    private IFileTester _fileTester;
    private IOtherFilter _filter;

    public Document(Stream myStream, IFileTester fileTester, IOtherFilter filter)
    {
        _stream = myStream ?? throw new ArgumentNullException(nameof(myStream));
        _fileTester = fileTester ?? throw new ArgumentNullException(nameof(fileTester));
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }
}
```

following code will be generated:

```csharp
partial class DocumentTests
{
    [Test]
    public void Throws_Exception_When_MyStream_Is_Null()
    {
        Action action = () => new Document(null, null, null);
        action.ShouldThrow<ArgumentNullException>().ParamName.Should().Be("myStream");
    }

    [Test]
    public void Throws_Exception_When_FileTester_Is_Null()
    {
        Action action = () => new Document(Mock.Of<System.IO.Stream>(), null, null);
        action.ShouldThrow<ArgumentNullException>().ParamName.Should().Be("fileTester");
    }

    [Test]
    public void Throws_Exception_When_Filter_Is_Null()
    {
        Action action = () => new Document(Mock.Of<System.IO.Stream>(), Mock.Of<Sample.IFileTester>(), null);
        action.ShouldThrow<ArgumentNullException>().ParamName.Should().Be("filter");
    }
}

```

#### Options

It's possible to generate a nested class with the `AsNestedClass` argument.

```csharp
[GenerateConstructorParameterNullTests(typeof(Document), AsNestedClass = true)]
internal partial class TestWithNested
{
}
```

### Web service tests

Use the `WebServiceTester` helper class. This class reads the OpenApi documentation and can execute arbitary tests.

```csharp
[Test]
public async Task TestEndpointSecurity()
{
    var tester = new WebServiceTester("swagger.json");

    await tester.VerifySecuredEndpointsRequiresAuthentication(httpClient, CancellationToken.None);
}
```