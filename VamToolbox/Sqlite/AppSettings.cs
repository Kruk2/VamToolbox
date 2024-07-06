using System.Diagnostics.CodeAnalysis;
using VamToolbox.Operations.Repo;

namespace VamToolbox.Sqlite;

[ExcludeFromCodeCoverage]
public class AppSettings 
{
    public string? AdditionalVars { get; set; }
    public string? VamDir { get; set; }
    public int Threads { get; set; }
    public bool RemoveSoftLinksBefore { get; set; }
    public List<ProfileModel> Profiles { get; set; } = new();
    public CopyMode? CopyMode { get; set; }
}
