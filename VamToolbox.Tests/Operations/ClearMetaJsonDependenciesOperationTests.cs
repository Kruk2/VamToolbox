using System.IO.Abstractions.TestingHelpers;
using System.Security.AccessControl;
using AutoFixture;
using FluentAssertions;
using VamToolbox.Operations.Abstract;
using VamToolbox.Operations.Destructive;
using Xunit;

namespace VamToolbox.Tests.Operations;
public class ClearMetaJsonDependenciesOperationTests 
{
    private readonly MockFileSystem _fs;
    private readonly ClearMetaJsonDependenciesOperation _operation;
    private const string AddondsDir = "C:/VaM/AddonPackages/";

    public ClearMetaJsonDependenciesOperationTests()
    {
        var fixture = new CustomFixture();
        _fs = fixture.Create<MockFileSystem>();
        _operation = fixture.Create<ClearMetaJsonDependenciesOperation>();
    }

    [Fact]
    public async Task Execute_WhenMetaIsMissing_ShouldSkip()
    {
        CreateZipFile();
        await Execute();

        var metaFile = ReadMetaJson();
        metaFile.Should().BeNull();
    }


    [Fact]
    public async Task Execute_WhenMetaIsBroken_ShouldSkip()
    {
        CreateZipFile("broken-json");
        await Execute();

        var metaFile = ReadMetaJson();
        metaFile.Should().Be("broken-json");
    }

    [Fact]
    public async Task Execute_WhenMetaHasDependencies_ShouldClearThem()
    {
        CreateZipFile(TestMetaWithDepsOnly);
        await Execute();

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
    public async Task Execute_WhenMetaHasBrokenReferences_ShouldClearThem()
    {
        CreateZipFile(TestMetaWithBrokenReferences);
        await Execute();

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

    private Task Execute(bool dryRun = false) => _operation.Execute(new OperationContext {
        DryRun = dryRun,
        VamDir = "C:/VaM",
        Threads = 1
    });

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
