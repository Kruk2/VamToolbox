using System.Collections.Generic;
using Newtonsoft.Json;

namespace VamRepacker.Models;

public class MetaFileJson
{
    [JsonProperty("dependencies")]
    public Dictionary<string, Dependency> Dependencies { get; private set; } = new();

}

public class Dependency
{
    [JsonProperty("dependencies")]
    public Dictionary<string, Dependency> Dependencies { get; private set; } = new();
}