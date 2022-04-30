using FluentAssertions;
using VamToolbox.Helpers;
using VamToolbox.Models;
using Xunit;
using AutoFixture;

namespace VamToolbox.Tests;

public class UuidReferencesResolverTests
{
    private readonly UuidReferencesResolver _resolver = new();

    private readonly Reference _reference;
    private readonly JsonFile _jsonFile;
    private readonly List<FreeFile> _freeFiles;
    private readonly List<VarPackage> _vars;

    public UuidReferencesResolverTests()
    {
        var fixture = new CustomFixture();
        _reference = fixture.Create<Reference>();
        _jsonFile = fixture.Create<JsonFile>();
        _freeFiles = fixture.CreateMany<FreeFile>().ToList();
        _vars = fixture.CreateMany<VarPackage>().ToList();

        _resolver.InitLookups(Enumerable.Empty<FreeFile>(), Enumerable.Empty<VarPackage>()).GetAwaiter().GetResult();
    }

    [Fact]
    public void Resolve_NoMatchingUuids_ShouldReturnNothing()
    {
        var (jsonReference, isDelayed) =_resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, sourceVar: null, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_MatchingMaleMorphAgainstFemale_ShouldReturnNothing()
    {
        var matchedFile = CreateFile(KnownNames.MaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = _reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, sourceVar: null, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_MatchingFemaleMorphAgainstMale_ShouldReturnNothing()
    {
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/morph.vmi");

        matchedFile.MorphName = _reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, sourceVar: null, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_NoMatchingUuidsWithFileMatchedOutsideMorphDirectory_ShouldReturnNothing()
    {
        var matchedFile = CreateFile(KnownNames.ClothDirs + "/morph.vmi");
        matchedFile.MorphName = _reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, sourceVar: null, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Theory, CustomAutoData]
    public void Resolve_NotMatchingUuidsWithFallBackReference_ShouldReturnFallbackReference(FreeFile fallbackReference)
    {
        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, sourceVar: null, fallbackReference);

        jsonReference.Should().NotBeNull();
        jsonReference!.ToFile.Should().Be(fallbackReference);
        isDelayed.Should().BeFalse();
    }


    [Fact]
    public async Task Resolve_MatchingOneUuid_ShouldReturnExactMatch()
    {
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = _reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, sourceVar: null, fallBackResolvedAsset: null);

        jsonReference!.ToFile.Should().Be(matchedFile);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_TwoMatchingUuid_WhenOneIsOutsideVamDir_ShouldReturnExactMatch()
    {
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = _reference.MorphName!;
        var matchedFile2 = CreateFile(KnownNames.FemaleGenMorphsDir + "/morph.vmi", isInVamDir: false);
        matchedFile2.MorphName = _reference.MorphName!;

        _freeFiles.Add(matchedFile);
        _freeFiles.Add(matchedFile2);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, sourceVar: null, fallBackResolvedAsset: null);

        jsonReference!.ToFile.Should().Be(matchedFile);
        isDelayed.Should().BeFalse();
    }

    [Theory, CustomAutoData]
    public async Task Resolve_TwoMatchingUuid_WhenOneIsInsideScannedVar_ShouldReturnExactMatch(VarPackage varPackage)
    {
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = _reference.MorphName!;
        _freeFiles.Add(matchedFile);

        var varFile = CreateVarFile(KnownNames.FemaleMorphsDir + "/morph.vmi", varPackage);
        varFile.MorphName = _reference.MorphName!;
        _vars.Add(varPackage);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, sourceVar: varPackage, fallBackResolvedAsset: null);

        jsonReference!.ToFile.Should().Be(varFile);
        isDelayed.Should().BeFalse();
    }

    [Theory, CustomAutoData]
    public async Task Resolve_TwoMatchingUuid_ShouldReturnDelayedFlag(VarPackage varPackage)
    {
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = _reference.MorphName!;
        var matchedFile2 = CreateFile(KnownNames.FemaleGenMorphsDir + "/morph.vmi");
        matchedFile2.MorphName = _reference.MorphName!;

        _freeFiles.Add(matchedFile);
        _freeFiles.Add(matchedFile2);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, sourceVar: varPackage, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeTrue();
    }

    private FreeFile CreateFile(string localPath, bool isInVamDir = true) => new("a", localPath, 1, isInVamDir, DateTime.Now);
    private VarPackageFile CreateVarFile(string localPath, VarPackage varPackage) => new(localPath, 1, true, varPackage, DateTime.Now);
}