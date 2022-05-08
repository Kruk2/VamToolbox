using VamToolbox.Operations.Repo;

namespace VamToolbox.Sqlite;
public class AppSettings 
{
    public string? AdditionalVars { get; set; }
    public string? VamDir { get; set; }
    public int Threads { get; set; }
    public bool RemoveSoftLinksBefore { get; set; }
    public bool ShallowDependencies { get; set; } = true;
    public List<ProfileModel> Profiles { get; set; } = new();
}
