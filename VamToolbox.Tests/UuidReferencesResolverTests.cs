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
        var reference = new Reference(KnownNames.FemaleMorphsDir + "morph.vmi", 0, 0, _freeFiles.First());
        reference.MorphName = "internal id";
        var matchedFile = CreateFile(KnownNames.MaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, reference, sourceVar: null, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_MatchingMaleAgainstFemale_WithFallbackReference_ShouldReturnFallbackReference()
    {
        var fallbackReference = CreateFile(KnownNames.MaleMorphsDir + "/morph.vmi", isInVamDir: false);
        var reference = new Reference(KnownNames.FemaleMorphsDir + "morph.vmi", 0, 0, _freeFiles.First());
        reference.MorphName = "internal id";
        var matchedFile = CreateFile(KnownNames.MaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, reference, sourceVar: null, fallBackResolvedAsset: fallbackReference);

        jsonReference!.ToFile.Should().Be(fallbackReference);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_MatchingFemaleMorphAgainstMale_ShouldReturnNothing()
    {
        var reference = new Reference(KnownNames.MaleMorphsDir + "morph.vmi", 0, 0, _freeFiles.First());
        reference.MorphName = "internal id";
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, reference, sourceVar: null, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_MatchingMaleGenMorphAgainstMale_ShouldMatch()
    {
        var reference = new Reference(KnownNames.MaleGenMorphsDir + "morph.vmi", 0, 0, _freeFiles.First());
        reference.MorphName = "internal id";
        var matchedFile = CreateFile(KnownNames.MaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, reference, sourceVar: null, fallBackResolvedAsset: null);

        jsonReference!.ToFile.Should().Be(matchedFile);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_MatchingFemaleGenMorphAgainstFemale_ShouldReturnNothing()
    {
        var reference = new Reference(KnownNames.FemaleGenMorphsDir + "morph.vmi", 0, 0, _freeFiles.First());
        reference.MorphName = "internal id";
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, reference, sourceVar: null, fallBackResolvedAsset: null);

        jsonReference!.ToFile.Should().Be(matchedFile);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_NoMatchingUuids_WhenFileMatchedOutsideMorphDirectory_ShouldReturnNothing()
    {
        var matchedFile = CreateFile(KnownNames.FemaleClothDir + "/morph.vmi");
        matchedFile.MorphName = _reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, sourceVar: null, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public void Resolve_NoMatchingUuidsWithFallBackReference_ShouldReturnFallbackReference()
    {
        var fallbackReference = CreateFile(KnownNames.FemaleGenMorphsDir + "morph.vmi", isInVamDir: false);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, sourceVar: null, fallbackReference);

        jsonReference.Should().NotBeNull();
        jsonReference!.ToFile.Should().Be(fallbackReference);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_FallbackReference_WhenInVamDir_ShouldReturnFallbackReference()
    {
        var fallbackReference = CreateFile(KnownNames.FemaleGenMorphsDir + "morph.vmi");
        var matchedFile = CreateFile(KnownNames.FemaleGenMorphsDir + "morph.vmi", isInVamDir: false);
        matchedFile.MorphName = _reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, sourceVar: null, fallbackReference);

        jsonReference!.ToFile.Should().Be(fallbackReference);
        isDelayed.Should().BeFalse();
    }


    [Theory, CustomAutoData]
    public async Task Resolve_FallbackVarFileReference_WhenInVamDir_ShouldReturnFallbackReference(VarPackage package)
    {
        var fallbackReference = CreateVarFile(KnownNames.FemaleGenMorphsDir + "morph.vmi", package);
        var matchedFile = CreateFile(KnownNames.FemaleGenMorphsDir + "morph.vmi", isInVamDir: false);
        matchedFile.MorphName = _reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, sourceVar: null, fallbackReference);

        jsonReference!.ToFile.Should().Be(fallbackReference);
        isDelayed.Should().BeFalse();
    }

    [Theory, CustomAutoData]
    public async Task Resolve_FallbackVarFileReference_WhenItsOutsideVamDirButInScannedVarPackage_ShouldReturnFallbackReference(VarPackageName packageName)
    {
        var varOutsideVamDir = new VarPackage(packageName, "", isInVamDir: false, 1);
        var fallbackReference = CreateVarFile(KnownNames.FemaleGenMorphsDir + "morph.vmi", varOutsideVamDir, isInVamDir: false);
        var matchedFile = CreateFile(KnownNames.FemaleGenMorphsDir + "morph.vmi", isInVamDir: false);
        matchedFile.MorphName = _reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, sourceVar: varOutsideVamDir, fallbackReference);

        jsonReference!.ToFile.Should().Be(fallbackReference);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_FallbackFileReference_WhenItsOutsideVamDirButReferencesParticularVarVersion_ShouldReturnFallbackReference()
    {
        var fallbackReference = CreateFile(KnownNames.FemaleGenMorphsDir + "morph.vmi",  isInVamDir: false);
        var reference = new Reference("Author.Name.1:" + KnownNames.FemaleGenMorphsDir + "morph.vmi", 0, 0, _freeFiles.First());
        var matchedFile = CreateFile(KnownNames.FemaleGenMorphsDir + "morph.vmi", isInVamDir: false);
        reference.MorphName =  matchedFile.MorphName = _reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, reference, sourceVar: null, fallbackReference);

        jsonReference!.ToFile.Should().Be(fallbackReference);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public void Resolve_FallbackFileReference_WhenItsOutsideVamDirButReferencesParticularVarMinVersion_ShouldReturnFallbackReference()
    {
        var fallbackReference = CreateFile(KnownNames.FemaleGenMorphsDir + "morph.vmi", isInVamDir: false);
        var reference = new Reference("Author.Name.min1:" + KnownNames.FemaleGenMorphsDir + "morph.vmi", 0, 0, _freeFiles.First());
        reference.MorphName = _reference.MorphName!;
        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, reference, sourceVar: null, fallbackReference);

        jsonReference!.ToFile.Should().Be(fallbackReference);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_FallbackFileReference_WhenItsOutsideVamDirAndReferencesLatestVersion_ShouldReturnDelayedFlag()
    {
        var fallbackReference = CreateFile(KnownNames.FemaleGenMorphsDir + "morph.vmi", isInVamDir: false);
        var matchedFile = CreateFile(KnownNames.FemaleGenMorphsDir + "morph.vmi", isInVamDir: false);
        matchedFile.MorphName = _reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var reference = new Reference("Author.Name.latest:" + KnownNames.FemaleGenMorphsDir + "morph.vmi", 0, 0, _freeFiles.First());
        reference.MorphName = _reference.MorphName!;
        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, reference, sourceVar: null, fallbackReference);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeTrue();
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

    [Fact] public async Task ResolvePreset_MaleHairReferenceMatchedInFemaleHairDirectory_ShouldReturnNothing()
    {
        var matchedFile = CreateFile(KnownNames.MaleHairDir + "/hair.vam");
        var reference = new Reference(KnownNames.FemaleHairDir + "hair.vam", 0, 0, _freeFiles.First());
        reference.InternalId = "internal id";
        matchedFile.InternalId = reference.InternalId!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchVamJsonReferenceById(_jsonFile, reference, sourceVar: null, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task ResolvePreset_MaleClothReferenceMatchedInFemaleClothDirectory_ShouldReturnNothing()
    {
        var matchedFile = CreateFile(KnownNames.MaleClothDir + "/cloth.vam");
        var reference = new Reference(KnownNames.FemaleClothDir + "cloth.vam", 0, 0, _freeFiles.First());
        reference.InternalId = "internal id";
        matchedFile.InternalId = reference.InternalId!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchVamJsonReferenceById(_jsonFile, reference, sourceVar: null, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task ResolvePreset_HairFileMatchedOutsideHairDirectory_ShouldReturnNothing()
    {
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/hair.vam");
        matchedFile.InternalId = _reference.InternalId!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchVamJsonReferenceById(_jsonFile, _reference, sourceVar: null, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task ResolvePreset_ClothFileMatchedOutsideClothDirectory_ShouldReturnNothing()
    {
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/cloth.vam");
        matchedFile.InternalId = _reference.InternalId!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchVamJsonReferenceById(_jsonFile, _reference, sourceVar: null, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task ResolvePreset_SingleHairFileMatched_ShouldReturnIt()
    {
        var matchedFile = CreateFile(KnownNames.FemaleHairDir + "/hair.vam");
        matchedFile.InternalId = _reference.InternalId!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchVamJsonReferenceById(_jsonFile, _reference, sourceVar: null, fallBackResolvedAsset: null);

        jsonReference!.ToFile.Should().Be(matchedFile);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task ResolvePreset_SingleClothFileMatched_ShouldReturnIt()
    {
        var matchedFile = CreateFile(KnownNames.FemaleClothDir + "/cloth.vam");
        matchedFile.InternalId = _reference.InternalId!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchVamJsonReferenceById(_jsonFile, _reference, sourceVar: null, fallBackResolvedAsset: null);

        jsonReference!.ToFile.Should().Be(matchedFile);
        isDelayed.Should().BeFalse();
    }

    private FreeFile CreateFile(string localPath, bool isInVamDir = true) => new("a", localPath, 1, isInVamDir, DateTime.Now);
    private VarPackageFile CreateVarFile(string localPath, VarPackage varPackage, bool isInVamDir = true) => new(localPath, 1, isInVamDir, varPackage, DateTime.Now);
}