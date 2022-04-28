namespace VamToolbox.Sqlite;

public sealed class HashesTable
{
    public HashesTable(string varFileName, string localAssetPath, string hash)
    {
        VarFileName = varFileName;
        LocalAssetPath = localAssetPath;
        Hash = hash;
    }

    private bool Equals(HashesTable other)
    {
        return string.Equals(VarFileName, other.VarFileName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(LocalAssetPath, other.LocalAssetPath, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is HashesTable other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(VarFileName, StringComparer.OrdinalIgnoreCase);
        hashCode.Add(LocalAssetPath, StringComparer.OrdinalIgnoreCase);
        return hashCode.ToHashCode();
    }

    public string VarFileName { get; set; }
    public string LocalAssetPath { get; set; }
    public string Hash { get; set; }
}