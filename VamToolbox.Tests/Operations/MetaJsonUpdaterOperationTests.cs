using System.IO.Abstractions.TestingHelpers;
using System.Security.AccessControl;
using AutoFixture;
using FluentAssertions;
using VamToolbox.Operations.Abstract;
using VamToolbox.Operations.Destructive;
using Xunit;

namespace VamToolbox.Tests.Operations;
public class MetaJsonUpdaterOperationTests 
{
    private readonly MockFileSystem _fs;
    private readonly MetaJsonUpdaterOperation _operation;
    private const string AddondsDir = "C:/VaM/AddonPackages/";

    public MetaJsonUpdaterOperationTests()
    {
        var fixture = new CustomFixture();
        _fs = fixture.Create<MockFileSystem>();
        _operation = fixture.Create<MetaJsonUpdaterOperation>();
    }

    [Fact]
    public async Task Execute_WhenMetaIsMissing_ShouldSkip()
    {
        CreateZipFile();
        await Execute(removeDeps: true, disableMorphs: true);

        var metaFile = ReadMetaJson();
        metaFile.Should().BeNull();
    }


    [Fact]
    public async Task Execute_WhenMetaIsBroken_ShouldSkip()
    {
        CreateZipFile("broken-json");
        await Execute(removeDeps: true, disableMorphs: true);

        var metaFile = ReadMetaJson();
        metaFile.Should().Be("broken-json");
    }

    [Fact]
    public async Task RemoveDeps_WhenMetaHasDependencies_ShouldClearThem()
    {
        CreateZipFile(TestMetaWithDepsOnly);
        await Execute(removeDeps: true);

        var metaFile = ReadMetaJson();
        metaFile.Should().Be(@"{
  ""licenseType"": ""PC"",
  ""creatorName"": ""Chill_PopRun"",
  ""contentList"": [
    ""Saves/scene/private_clubII1.402.json""
  ],
  ""dependencies"": {}
}");
    }


    [Fact]
    public async Task RemoveDeps_WhenMetaHasBrokenReferences_ShouldClearThem()
    {
        CreateZipFile(TestMetaWithBrokenReferences);
        await Execute(removeDeps: true);

        var metaFile = ReadMetaJson();
        metaFile.Should().Be(@"{
  ""licenseType"": ""PC"",
  ""creatorName"": ""Chill_PopRun"",
  ""contentList"": [
    ""Saves/scene/private_clubII1.402.json"",
    ""Saves/scene/private_clubII1.402.jpg""
  ],
  ""dependencies"": {},
  ""customOptions"": {
    ""preloadMorphs"": ""false""
  }
}");
    }

    [Fact]
    public async Task DisableMorphs_WhenMorphsAreEnabled_ShouldDisable()
    {
        CreateZipFile(TestMetaWithMorphPreloadEnabled);
        await Execute(disableMorphs: true);

        var metaFile = ReadMetaJson();
        metaFile.Should().Be(@"{
  ""licenseType"": ""PC"",
  ""creatorName"": ""Chill_PopRun"",
  ""contentList"": [
    ""Saves/scene/private_clubII1.402.json""
  ],
  ""dependencies"": {},
  ""customOptions"": {
    ""test2"": true,
    ""preloadMorphs"": ""false""
  }
}");
    }

    [Fact]
    public async Task DisableMorphs_WhenMorphsAreDisabled_ShouldNotChangeAnything()
    {
        CreateZipFile(TestMetaWithMorphPreloadDisabled);
        await Execute(disableMorphs: true);

        var metaFile = ReadMetaJson();
        metaFile.Should().Be(TestMetaWithMorphPreloadDisabled);
    }

    [Fact]
    public async Task DisableMorphs_WhenCustomOptionsAreMissing_ShouldNotChangeAnything()
    {
        CreateZipFile(TestMetaWithMorphPreloadMissingCustomOptions);
        await Execute(disableMorphs: true);

        var metaFile = ReadMetaJson();
        metaFile.Should().Be(TestMetaWithMorphPreloadMissingCustomOptions);
    }

    [Fact]
    public async Task Execute_WhenThereIsNothingToFix_SkipWriting()
    {
        CreateZipFile(TestMetaWithNothingToFix);
        await Execute(removeDeps: true, disableMorphs: true);

        var metaFile = ReadMetaJson();
        metaFile.Should().Be(TestMetaWithNothingToFix);
    }

    private string? ReadMetaJson()
    {

        var file = _fs.GetFile(AddondsDir + "a.var");
        var files = ZipTestHelpers.ReadZipFile(file);
        files.TryGetValue("meta.json", out var metaContent);
        return metaContent;
    }

    private void CreateZipFile(string? metaContent = null)
    {
        var files = new Dictionary<string, string>();
        if (metaContent is not null) {
            files["meta.json"] = metaContent;
        }

        var zipFile = ZipTestHelpers.CreateZipFile(files);
        _fs.AddFile(AddondsDir + "a.var", zipFile);
    }

    private Task Execute(bool dryRun = false, bool removeDeps = false, bool disableMorphs = false) => _operation.Execute(new OperationContext {
        DryRun = dryRun,
        VamDir = "C:/VaM",
        Threads = 1
    }, removeDeps, disableMorphs);

    private const string TestMetaWithMorphPreloadMissingCustomOptions = @"{ 
   ""licenseType"" : ""PC"", 
   ""creatorName"" : ""Chill_PopRun"", 
   ""contentList"" : [ 
      ""Saves/scene/private_clubII1.402.json""
   ], 
   ""dependencies"" : { },
   ""customOptions"" : { 
      ""preloadMorphs"" : ""false""
   }
}";

    private const string TestMetaWithMorphPreloadDisabled = @"{ 
   ""licenseType"" : ""PC"", 
   ""creatorName"" : ""Chill_PopRun"", 
   ""contentList"" : [ 
      ""Saves/scene/private_clubII1.402.json""
   ], 
   ""dependencies"" : { },
   ""customOptions"" : { 
      ""preloadMorphs"" : ""false"",
      ""test1"" : ""qqq"",
   }
}";

    private const string TestMetaWithMorphPreloadEnabled = @"{ 
   ""licenseType"" : ""PC"", 
   ""creatorName"" : ""Chill_PopRun"", 
   ""contentList"" : [ 
      ""Saves/scene/private_clubII1.402.json""
   ], 
   ""dependencies"" : { },
   ""customOptions"" : { 
      ""test2"": true,
      ""preloadMorphs"" : ""true""
   }
}";

    private const string TestMetaWithNothingToFix = @"{ 
   ""licenseType"" : ""PC"", 
   ""creatorName"" : ""Chill_PopRun"", 
   ""contentList"" : [ 
      ""Saves/scene/private_clubII1.402.json"", 
      ""Saves/scene/private_clubII1.402.jpg""
   ], 
   ""dependencies"" : { },
   ""customOptions"" : { 
      ""preloadMorphs"" : ""false""
   }
}";

    private const string TestMetaWithDepsOnly = @"{ 
   ""licenseType"" : ""PC"", 
   ""creatorName"" : ""Chill_PopRun"", 
   ""contentList"" : [ 
      ""Saves/scene/private_clubII1.402.json""
   ], 
   ""dependencies"" : { 
      ""ToumeiHitsuji.SlapStuffAudioPack.latest"" : { 
         ""licenseType"" : ""CC BY-SA"", 
         ""dependencies"" : { 
         }
      }, 
      ""rz67vr.cyber_room_2.latest"" : { 
         ""licenseType"" : ""CC BY"", 
         ""dependencies"" : { 
         }
      }, 
      ""MacGruber.LogicBricks.12"" : { 
         ""licenseType"" : ""PC EA"", 
         ""dependencies"" : { 
         }
      }, 
      ""AcidBubbles.Timeline.252"" : { 
         ""licenseType"" : ""CC BY-SA"", 
         ""dependencies"" : { 
         }
      }, 
      ""ToumeiHitsuji.SlapStuff.2"" : { 
         ""licenseType"" : ""CC BY-SA"", 
         ""dependencies"" : { 
            ""ToumeiHitsuji.SlapStuffAudioPack.2"" : { 
               ""licenseType"" : ""CC BY-SA"", 
               ""dependencies"" : { 
               }
            }
         }
      }
   }
}";

    private const string TestMetaWithBrokenReferences = @"{ 
   ""licenseType"" : ""PC"", 
   ""creatorName"" : ""Chill_PopRun"", 
   ""contentList"" : [ 
      ""Saves/scene/private_clubII1.402.json"", 
      ""Saves/scene/private_clubII1.402.jpg""
   ], 
   ""dependencies"" : { 
      ""ToumeiHitsuji.SlapStuffAudioPack.latest"" : { 
         ""licenseType"" : ""CC BY-SA"", 
         ""dependencies"" : { 
         }
      }, 
      ""rz67vr.cyber_room_2.latest"" : { 
         ""licenseType"" : ""CC BY"", 
         ""dependencies"" : { 
         }
      }, 
      ""MacGruber.LogicBricks.12"" : { 
         ""licenseType"" : ""PC EA"", 
         ""dependencies"" : { 
         }
      }, 
      ""AcidBubbles.Timeline.252"" : { 
         ""licenseType"" : ""CC BY-SA"", 
         ""dependencies"" : { 
         }
      }, 
      ""ToumeiHitsuji.SlapStuff.2"" : { 
         ""licenseType"" : ""CC BY-SA"", 
         ""dependencies"" : { 
            ""ToumeiHitsuji.SlapStuffAudioPack.2"" : { 
               ""licenseType"" : ""CC BY-SA"", 
               ""dependencies"" : { 
               }
            }
         }
      }
   }, 
   ""customOptions"" : { 
      ""preloadMorphs"" : ""false""
   }, 
   ""hadReferenceIssues"" : ""true"", 
   ""referenceIssues"" : [ 
      { 
         ""reference"" : ""Chill_PopRun.private_clubII1_2.1:/private_clubII/f_start_pose"", 
         ""issue"" : ""BROKEN: Missing package that is referenced""
      }, 
      { 
         ""reference"" : ""Chill_PopRun.private_clubII1_2.1:/Custom/SubScene/Chill_PopRun/Chill_PopRun/3-light/chill_light.json"", 
         ""issue"" : ""BROKEN: Missing package that is referenced""
      }
   ]
}";
}
