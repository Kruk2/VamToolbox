using Newtonsoft.Json;

namespace VamToolbox.Models;

public sealed class MetaFileJson
{
    [JsonProperty("dependencies")]
    public Dictionary<string, Dependency> Dependencies { get; private set; } = new();

}

public sealed class Dependency
{
    [JsonProperty("dependencies")]
    public Dictionary<string, Dependency> Dependencies { get; private set; } = new();
}