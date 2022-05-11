using FluentAssertions;
using FluentAssertions.Execution;
using VamToolbox.Helpers;
using VamToolbox.Models;
using Xunit;

namespace VamToolbox.Tests.Helpers;
public class JsonScannerTests
{
    private readonly JsonFileParser _scanner = new();

    [Theory]
    [InlineData(@"    ""presetFilePath"" : ""SELF:/Custom/Atom/Person/Pose/Preset_we-invaderfootjob.vap"" ", "SELF:/Custom/Atom/Person/Pose/Preset_we-invaderfootjob.vap")]
    [InlineData(@"    ""id"" : ""SELF:/Custom/Hair/Female/vecterror/kate hair 1b/kate hair 1b.vam"", ", "SELF:/Custom/Hair/Female/vecterror/kate hair 1b/kate hair 1b.vam")]
    [InlineData(@"	""plugin#0"" : ""Custom/Scripts/ImprovedPoV.cs""  ", "Custom/Scripts/ImprovedPoV.cs")]
    [InlineData(@"""plugin#0"" : ""./detention/RT_LipSync.dll""", "./detention/RT_LipSync.dll")]
    [InlineData(@"""assetDllUrl"" : ""Custom/Assets/!Favorite/merin-tail.dll""", "Custom/Assets/!Favorite/merin-tail.dll")]
    [InlineData(@"   ""customTexture_MainTex"" : ""a.jpg"" ", "a.jpg")]
    [InlineData(@"   ""customTexture_AlphaTex"" : ""a.png"" ", "a.png")]
    [InlineData(@"   ""customTexture_BumpMap"" : ""a.jpeg"" ", "a.jpeg")]
    [InlineData(@"   ""customTexture_GlossTex"" : ""a.tif"" ", "a.tif")]
    [InlineData(@"   ""customTexture_SpecTex"" : ""a.tiff"" ", "a.tiff")]
    [InlineData(@"   ""torsoGlossUrl"" : ""a.jpg"" ", "a.jpg")]
    [InlineData(@"   ""torsoSpecularUrl"" : ""a.png"" ", "a.png")]
    [InlineData(@"   ""torsoNormalUrl"" : ""a.jpeg"" ", "a.jpeg")]
    [InlineData(@"   ""torsoDiffuseUrl"" : ""a.tif"" ", "a.tif")]
    [InlineData(@"   ""genitalsGlossUrl"" : ""a.jpg"" ", "a.jpg")]
    [InlineData(@"   ""genitalsSpecularUrl"" : ""a.png"" ", "a.png")]
    [InlineData(@"   ""genitalsNormalUrl"" : ""a.jpeg"" ", "a.jpeg")]
    [InlineData(@"   ""genitalsDiffuseUrl"" : ""a.tif"" ", "a.tif")]
    [InlineData(@"   ""limbsGlossUrl"" : ""a.jpg"" ", "a.jpg")]
    [InlineData(@"   ""limbsSpecularUrl"" : ""a.png"" ", "a.png")]
    [InlineData(@"   ""limbsNormalUrl"" : ""a.jpeg"" ", "a.jpeg")]
    [InlineData(@"   ""limbsDiffuseUrl"" : ""a.tif"" ", "a.tif")]
    [InlineData(@"   ""simTexture"" : ""a.jpg"" ", "a.jpg")]
    [InlineData(@"   ""urlValue"" : ""a.png"" ", "a.png")]
    [InlineData(@"""Path"" : ""SELF:/Custom/Assets/MacGruber/Showcase/EvilCorpHQ/penguin_secure.json"",", "SELF:/Custom/Assets/MacGruber/Showcase/EvilCorpHQ/penguin_secure.json")]
    [InlineData(@"""auto#0"" : ""Custom/Scripts/Easy Moan/EasyMoan.cslist""", "Custom/Scripts/Easy Moan/EasyMoan.cslist")]
    [InlineData(@"""urlValue"" : ""SELF:/Custom/SubScene/VAMgrasshopper/1Mirror/1Mirror.json""", "SELF:/Custom/SubScene/VAMgrasshopper/1Mirror/1Mirror.json")]
    [InlineData(@"""pluginPath"" : ""Custom/Scripts/Blazedust/ClockSessionPlugin.cs""", "Custom/Scripts/Blazedust/ClockSessionPlugin.cs")]
    [InlineData(@"""filePath"" : ""JayJayWon.ActionGrouper.2:/Custom/Scripts/JayJayWon/ActionGrouper.cs""", "JayJayWon.ActionGrouper.2:/Custom/Scripts/JayJayWon/ActionGrouper.cs")]
    [InlineData(@"""decalTexturePath"" : ""Custom/Limbs Freckles Decal.png"",", "Custom/Limbs Freckles Decal.png")]
    [InlineData(@"""url"": ""Custom/Assets/cigarette.assetbundle"",", "Custom/Assets/cigarette.assetbundle")]
    [InlineData(@"""PeeUrl"" : ""SELF:/Custom/Assets/peeset.assetbundle"",", "SELF:/Custom/Assets/peeset.assetbundle")]
    [InlineData(@"""LensDirt Texture"" : ""MacGruber.PostMagic.latest:/Custom/Assets/MacGruber/PostMagic/Lens Dirt/LensDirt00.png""", "MacGruber.PostMagic.latest:/Custom/Assets/MacGruber/PostMagic/Lens Dirt/LensDirt00.png")]
    [InlineData(@"  ""AssetBundle"" : ""MacGruber.Life.13:/Custom/Scripts/MacGruber/Life/MacGruber_Effects.audiobundle"",", "MacGruber.Life.13:/Custom/Scripts/MacGruber/Life/MacGruber_Effects.audiobundle")]
    [InlineData(@"""Additional Button Scene"" : ""SELF:/Saves/scene/MainMenu_Page_2.json""", "SELF:/Saves/scene/MainMenu_Page_2.json")]
    [InlineData(@"   ""act1Target1FileURLValueName"" : ""a.b.latest:/Custom/Assets/Animated Lava Lamps.assetbundle"",", "a.b.latest:/Custom/Assets/Animated Lava Lamps.assetbundle")]
    [InlineData(@"""shader"" : ""Custom/Assets/emissiveshader.assetbundle""", "Custom/Assets/emissiveshader.assetbundle")]
    [InlineData(@"  ""url"" : ""a.b.4:/Custom/Sounds/SFX/Pee_ground.mp3"",", "a.b.4:/Custom/Sounds/SFX/Pee_ground.mp3")]
    [InlineData(@"    ""sourcePath"" : ""SELF:/Custom/Sounds/N3D Dakota/00.wav""", "SELF:/Custom/Sounds/N3D Dakota/00.wav")]
    [InlineData(@"   ""storePath"" : ""SELF:/Custom/SubScene/eponge/sec_base/bosskneelBJ.json""", "SELF:/Custom/SubScene/eponge/sec_base/bosskneelBJ.json")]
    [InlineData(@"""assetUrl"" : ""a.b.latest:/Custom/Assets/Molmark/cumpack_transparancy.assetbundle""", "a.b.latest:/Custom/Assets/Molmark/cumpack_transparancy.assetbundle")]
    [InlineData(@"""assetUrl"" : ""Custom/Assets/Simple-Room.scene""", "Custom/Assets/Simple-Room.scene")]
    [InlineData(@"""sceneFilePath"" : ""SELF:/Saves/scene/sapuzex/New magic trick/new magic trick.json""", "SELF:/Saves/scene/sapuzex/New magic trick/new magic trick.json")]
    [InlineData(@"""presetFilePath"" : ""SELF:/Custom/SubScene/VirtAmateur/Bits/ECUI0.json""", "SELF:/Custom/SubScene/VirtAmateur/Bits/ECUI0.json")]
    [InlineData(@"""Path"" : ""test.test.latest:/a.png""", "test.test.latest:/a.png")]
    [InlineData(@"""File"" : ""test.test.latest:/a.png""", "test.test.latest:/a.png")]
    [InlineData(@"""AudioBundle"" : ""MacGruber.Life.10:/Custom/Scripts/MacGruber/Life/MacGruber_Breathing.audiobundle""", "MacGruber.Life.10:/Custom/Scripts/MacGruber/Life/MacGruber_Breathing.audiobundle")]
    [InlineData(@"""UserLUT"" : ""BigPacks.Oeshii-Looks.latest:/Custom/Assets/MacGruber/PostMagic/LUT32/OLUT_Brighten.png""", "BigPacks.Oeshii-Looks.latest:/Custom/Assets/MacGruber/PostMagic/LUT32/OLUT_Brighten.png")]
    [InlineData(@"""UserLUT"" : ""test.png""", "test.png")]
    [InlineData(@"""Load Face Subdermis Texture"" : ""SELF:/Custom/Textures/akila/face.png"",", "SELF:/Custom/Textures/akila/face.png")]
    [InlineData(@"""Spectral LUT"" : ""MacGruber.PostMagic.3:/Custom/Assets/MacGruber/PostMagic/Spectral LUTs/SpectralLut_BlueRed.png""", "MacGruber.PostMagic.3:/Custom/Assets/MacGruber/PostMagic/Spectral LUTs/SpectralLut_BlueRed.png")]
    [InlineData(@"""Light Texture"" : ""LFE.LightTexture0.9:/Custom/Atom/InvisibleLight/Textures/chainlink-1.png""", "LFE.LightTexture0.9:/Custom/Atom/InvisibleLight/Textures/chainlink-1.png")]
    [InlineData(@"""storePath"" : ""3P.lights.json""", "3P.lights.json")]
    public void Scan_ShouldFindMatch(string line, string asset)
    {
        var (reference, error) = Scan(line);

        using var _ = new AssertionScope();
        error.Should().BeNull();
        reference!.Value.Should().Be(asset);
    }

    [Theory]
    [InlineData(@" ""audioClip"" : ""00 Female giberish Full.mp3""")]
    [InlineData(@" ""displayName"" : ""a.ogg""")]
    [InlineData(@" ""sourceClip"" : ""00.wav"", ")]
    [InlineData(@"""uid"" : ""AudioMateClipWhoosh down.wav"",")]
    [InlineData(@"""stringValue"" : ""queef_001.mp3|queef_002.mp3|queef_003.mp3|queef_012.mp3|queef_005.mp3|queef_004.mp3""")]
    [InlineData(@"""clip_12"" : ""sdt_cough24.wav""")]
    [InlineData(@"""selected"" : ""bj deep.mp3""")]
    [InlineData(@"""receiverTargetName"" : ""Speak:teamrocketjesse.mp3""")]
    [InlineData(@"""act2Target1ValueName"" : ""skin_2.wav""")]
    [InlineData(@"""act20Target1ValueName"" : ""20.ogg""")]
    [InlineData(@"""backgroundMusicClip"" : ""32 - Killer Klowns From Outer Space (reprise).mp3""")]
    [InlineData(@"""Audio Clips"" : ""You gonna take me home tonight.mp3""")]
    [InlineData(@"""Action1\nAudio1"" : ""Moan6.mp3""")]
    [InlineData(@"  ""expression_4"" : ""expressions/light 1.mp3.json""")]
    [InlineData(@"""receiverTargetName"" : ""keyframesUse_keyframes_b.json""")]
    public void Scan_ShouldFindNoMatch(string line)
    {
        var (reference, error) = Scan(line);

        using var _ = new AssertionScope();
        error.Should().BeNull();
        reference.Should().BeNull();
    }

    [Theory]
    [InlineData(@"""filePath"" : ""Custom/Atom/Person/Appearance/Preset_milana.vap""", "Custom/Atom/Person/Appearance/Preset_milana.vap")]
    [InlineData(@"""filePath"" : ""Custom/Atom/Person/Appearance/Preset_milana.vmi""", "Custom/Atom/Person/Appearance/Preset_milana.vmi")]
    [InlineData(@"  ""preset3FilePath"" : ""Custom/Atom/Person/Pose/UIADemo/Preset_ROAC Contra.vap""", "Custom/Atom/Person/Pose/UIADemo/Preset_ROAC Contra.vap")]
    public void Scan_ShouldFindMatchInUiapFile(string line, string asset)
    {
        var file = new FreeFile("", "test.uiap", 0, true, DateTime.Now, softLinkPath: null);
        var reference = _scanner.GetAsset(line.AsSpan(), 0, file, out var error);

        using var _ = new AssertionScope();
        error.Should().BeNull();
        reference!.Value.Should().Be(asset);
    }

    [Fact]
    public void IgnoreVAmMoanSounds()
    {
        var line = @"""audio"": ""Assets/VAMMoan/Seth/m3-13.wav"",";
        var file = new FreeFile("", "Custom/Scripts/VAMMoan/audio/Seth/voice.json", 0, true, DateTime.Now, softLinkPath: null);
        var file2 = new FreeFile("", "Custom/Scripts/voice.json", 0, true, DateTime.Now, softLinkPath: null);

        var reference = _scanner.GetAsset(line.AsSpan(), 0, file, out var error);
        var reference2 = _scanner.GetAsset(line.AsSpan(), 0, file2, out var error2);

        using var _ = new AssertionScope();
        error.Should().BeNull();
        reference.Should().BeNull();

        error2.Should().BeNull();
        reference2.Should().BeNull();
    }

    [Fact]
    public void IgnoreBlushingPngs()
    {
        var line = @"""File"" : ""blush_cartoonlike_large.png"",";
        var file = new FreeFile("", "Custom/Scripts/cotyounoyume/ExpressionBlushingAndTears/Config/defaultSettings.json", 0, true, DateTime.Now, softLinkPath: null);

        var reference = _scanner.GetAsset(line.AsSpan(), 0, file, out var error);

        using var _ = new AssertionScope();
        error.Should().BeNull();
        reference.Should().BeNull();
    }


    [Fact]
    public void IgnoreVamDeluxeSounds()
    {
        var line = @"""audio"": ""./Audio/Aiko/AikoMoan1.wav"",";
        var file = new FreeFile("", "Custom/Scripts/VAMDeluxe/whatever.json", 0, true, DateTime.Now, softLinkPath: null);

        var reference = _scanner.GetAsset(line.AsSpan(), 0, file, out var error);

        using var _ = new AssertionScope();
        error.Should().BeNull();
        reference.Should().BeNull();
    }

    [Fact]
    public void IgnoreDollmasterSounds()
    {
        var line = @"""audio"": ""./Audio/Aiko/AikoMoan1.wav"",";
        var file = new FreeFile("", "Custom/Scripts/Dollmaster - Blowjob version/Assets/Personas/Aiko/persona.json", 0, true, DateTime.Now, softLinkPath: null);

        var reference = _scanner.GetAsset(line.AsSpan(), 0, file, out var error);

        using var _ = new AssertionScope();
        error.Should().BeNull();
        reference.Should().BeNull();
    }

    [Theory]
    [InlineData(@"   ""simTaexture"" : ""a.jpg"" ")]
    [InlineData(@"   ""urlVaeqweqwlue"" : ""a.png"" ")]
    [InlineData(@"  ""xcasdas"" : ""Nuflauer.BDSM_session.4:/Sounds/SFX/Pee_ground.mp3"",")]
    [InlineData(@"  ""uvzsid"" : ""noheadnoleg.AllScenes.latest:whatever.vmi"", ")]
    [InlineData(@"	""plugtqin#0"" : ""/Scripts/ImprovedPoV.cs""  ")]
    [InlineData(@"""plugeqszin#0"" : ""./detention/RT_LipSync.dll""")]
    [InlineData(@"   ""stogdsrePath"" : ""SELF:/SubScene/eponge/sec_base/bosskneelBJ.json""")]

    public void UnknownCombinationReturnsError(string line)
    {
        var (reference, error) = Scan(line);

        using var _ = new AssertionScope();
        error.Should().NotBeNull();
        reference.Should().BeNull();
    }

    [Theory]
    [InlineData(@"   ""urlValue"" : ""https://a.png"" ")]
    [InlineData(@"   ""urlValue"" : ""http://a.png"" ")]
    public void IgnoreHttpHttps(string line)
    {
        var (reference, error) = Scan(line);

        using var _ = new AssertionScope();
        error.Should().BeNull();
        reference.Should().BeNull();
    }

    [Theory]
    [InlineData(@"  ""urlValue"" : ""a.png"" ")]
    [InlineData(@"  ""urlValue"": ""a.png"" ")]
    [InlineData(@"  ""urlValue"":""a.png"" ")]
    [InlineData(@"  ""urlValue"" :""a.png"" ")]
    //[InlineData(@"  ""urlValue""    :     ""a.png"" ")] // TODO use utf8 forward json reader?
    public void MatchesWhenWhitespacesAreDifferent(string line)
    {
        var (reference, error) = Scan(line);

        using var _ = new AssertionScope();
        error.Should().BeNull();
        reference!.Value.Should().Be("a.png");
    }

    private (Reference? reference, string? error) Scan(string line)
    {
        var reference = _scanner.GetAsset(line.AsSpan(), 0, new FreeFile("test", "test", 1, false, DateTime.Now, softLinkPath: null), out var error);
        return (reference, error);
    }
}
