using FluentAssertions;
using FluentAssertions.Execution;
using VamToolbox.Models;
using Xunit;

namespace VamToolbox.Tests.Models;
public class AssetTypeTests
{
    [Theory]
    [InlineData(".vmi", KnownNames.FemaleMorphsDir + "/morph.vmi", AssetType.FemaleNormalMorph, true)]
    [InlineData(".vmb", KnownNames.FemaleGenMorphsDir + "/morph.vmb", AssetType.FemaleGenMorph, true)]
    [InlineData(".vmb", KnownNames.MaleMorphsDir + "/morph.vmb", AssetType.MaleNormalMorph, false)]
    [InlineData(".vmi", KnownNames.MaleGenMorphsDir + "/morph.vmi", AssetType.MaleGenMorph, false)]
    public void Classify_Morphs(string ext, string localPath, AssetType expectedType, bool isFemale)
    {
        var assetType = ext.ClassifyType(localPath);

        using var _ = new AssertionScope();
        assetType.Should().NotBe(AssetType.Unknown);
        assetType.Should().Be(expectedType);
        (assetType & AssetType.ValidMorph).Should().NotBe(0);
        (assetType & AssetType.Morph).Should().NotBe(0);
        (assetType & AssetType.ValidClothOrHairOrMorph).Should().NotBe(0);

        AssertFemaleMaleAssetTypes(isFemale, assetType);
        if (isFemale)
            (assetType & AssetType.FemaleMorph).Should().NotBe(0);
        else
            (assetType & AssetType.MaleMorph).Should().NotBe(0);
    }

    [Theory]
    [InlineData(".vmb", KnownNames.MaleHairDir + "morph.vmb")]
    [InlineData(".vmi", KnownNames.MaleHairDir + "morph.vmi")]
    public void Classify_UnknownMorph(string ext, string localPath)
    {
        var assetType = ext.ClassifyType(localPath);

        using var _ = new AssertionScope();

        assetType.Should().NotBe(AssetType.Unknown);
        assetType.Should().Be(AssetType.UnknownMorph);
        (assetType & AssetType.Morph).Should().NotBe(0);
        (assetType & AssetType.ValidMorph).Should().Be(0);
        assetType.IsFemale().Should().BeFalse();
        assetType.IsMale().Should().BeFalse();
    }

    [Theory]
    [InlineData(".vaj", KnownNames.FemaleHairDir + "/cloth.vaj", AssetType.FemaleHair, true)]
    [InlineData(".vam", KnownNames.FemaleHairDir + "/cloth.vam", AssetType.FemaleHair, true)]
    [InlineData(".vab", KnownNames.FemaleHairDir + "/cloth.vab", AssetType.FemaleHair, true)]
    [InlineData(".vaj", KnownNames.MaleHairDir + "/cloth.vmi", AssetType.MaleHair, false)]
    [InlineData(".vam", KnownNames.MaleHairDir + "/cloth.vmi", AssetType.MaleHair, false)]
    [InlineData(".vab", KnownNames.MaleHairDir + "/cloth.vmi", AssetType.MaleHair, false)]
    public void Classify_Hairs(string ext, string localPath, AssetType expectedType, bool isFemale)
    {
        var assetType = ext.ClassifyType(localPath);

        using var _ = new AssertionScope();
        assetType.Should().NotBe(AssetType.Unknown);
        assetType.Should().Be(expectedType);
        (assetType & AssetType.ValidHair).Should().NotBe(0);
        (assetType & AssetType.ValidClothOrHair).Should().NotBe(0);
        (assetType & AssetType.ValidClothOrHairOrMorph).Should().NotBe(0);

        AssertFemaleMaleAssetTypes(isFemale, assetType);
    }

    [Theory]
    [InlineData(".vaj", KnownNames.FemaleClothDir + "/cloth.vaj", AssetType.FemaleCloth, true)]
    [InlineData(".vam", KnownNames.FemaleClothDir + "/cloth.vam", AssetType.FemaleCloth, true)]
    [InlineData(".vab", KnownNames.FemaleClothDir + "/cloth.vab", AssetType.FemaleCloth, true)]
    [InlineData(".vaj", KnownNames.MaleClothDir + "/cloth.vmi", AssetType.MaleCloth, false)]
    [InlineData(".vam", KnownNames.MaleClothDir + "/cloth.vmi", AssetType.MaleCloth, false)]
    [InlineData(".vab", KnownNames.MaleClothDir + "/cloth.vmi", AssetType.MaleCloth, false)]

    [InlineData(".vab", KnownNames.SharedClothDir + "/cloth.vab", AssetType.OtherCloth, null)]
    [InlineData(".vaj", KnownNames.NeutralClothDir + "/cloth.vab", AssetType.OtherCloth, null)]
    public void Classify_Cloths(string ext, string localPath, AssetType expectedType, bool? isFemale)
    {
        var assetType = ext.ClassifyType(localPath);

        using var _ = new AssertionScope();
        assetType.Should().NotBe(AssetType.Unknown);
        assetType.Should().Be(expectedType);
        (assetType & AssetType.ValidCloth).Should().NotBe(0);
        (assetType & AssetType.ValidClothOrHair).Should().NotBe(0);
        (assetType & AssetType.ValidClothOrHairOrMorph).Should().NotBe(0);

        if (isFemale.HasValue)
            AssertFemaleMaleAssetTypes(isFemale.Value, assetType);
        else {
            assetType.IsFemale().Should().BeFalse();
            assetType.IsMale().Should().BeFalse();
        }
    }

    [Theory]
    [InlineData(".vaj", KnownNames.MaleMorphsDir + "a.vaj")]
    [InlineData(".vam", KnownNames.MaleMorphsDir + "b.vam")]
    public void Classify_UnknownHairOrClothes(string ext, string localPath)
    {
        var assetType = ext.ClassifyType(localPath);

        using var _ = new AssertionScope();

        assetType.Should().NotBe(AssetType.Unknown);
        assetType.Should().Be(AssetType.UnknownClothOrHair);
        (assetType & AssetType.ValidCloth).Should().Be(0);
        (assetType & AssetType.ValidClothOrHair).Should().Be(0);
        (assetType & AssetType.ValidHair).Should().Be(0);
        assetType.IsFemale().Should().BeFalse();
        assetType.IsMale().Should().BeFalse();
    }

    private static void AssertFemaleMaleAssetTypes(bool isFemale, AssetType assetType)
    {
        if (isFemale) {
            assetType.IsFemale().Should().BeTrue();
            assetType.IsMale().Should().BeFalse();
        } else {
            assetType.IsFemale().Should().BeFalse();
            assetType.IsMale().Should().BeTrue();
        }
    }
}
