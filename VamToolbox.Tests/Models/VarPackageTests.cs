using FluentAssertions;
using FluentAssertions.Execution;
using VamToolbox.Models;
using Xunit;

namespace VamToolbox.Tests.Models;
public class VarPackageTests
{
    [Theory, CustomAutoData]
    public void Create_ShouldInitAllProperties(VarPackageName varName, long size, bool isInVamDir)
    {
        var fakePath = @"C:/a\q/e\smtH.assetbundlE";
        var varPackage = new VarPackage(varName, fakePath, softLinkPath: null, isInVamDir, size);

        using var _ = new AssertionScope();
        varPackage.FullPath.Should().Be("C:/a/q/e/smtH.assetbundlE");
        varPackage.Size.Should().Be(size);
        varPackage.IsInVaMDir.Should().Be(true);
    }

    [Theory, CustomAutoData]
    public void Create_AddingVarFiles(VarPackage varPackage)
    {
        var varFile1 = new VarPackageFile("a", 1, false, varPackage, DateTime.Now);
        var varFile2 = new VarPackageFile("A", 1, false, varPackage, DateTime.Now);

        using var _ = new AssertionScope();
        varPackage.Files.Should().BeEquivalentTo(new[] { varFile1, varFile2 });
        varPackage.FilesDict.Should().HaveCount(1);
        varPackage.FilesDict.Should().ContainKey("a");
        varPackage.FilesDict.Should().ContainValue(varFile1);
    }
}
