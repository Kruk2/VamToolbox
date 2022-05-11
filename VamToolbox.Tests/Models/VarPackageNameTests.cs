using FluentAssertions;
using FluentAssertions.Execution;
using VamToolbox.Models;
using Xunit;

namespace VamToolbox.Tests.Models;
public class VarPackageNameTests 
{

    [Theory]
    [InlineData("vamX.1.latest.var", "vamX", "1", -1)]
    public void ShouldParseCorrectly(string inputName, string author, string name, int version)
    {
        var ok = VarPackageName.TryGet(inputName, out var parsedVar);

        using var _ = new AssertionScope();
        ok.Should().BeTrue();
        parsedVar!.Filename.Should().Be(inputName);
        parsedVar.Author.Should().Be(author);
        parsedVar.Name.Should().Be(name);
        parsedVar.Version.Should().Be(version);
        parsedVar.MinVersion.Should().BeFalse();
    }
}
