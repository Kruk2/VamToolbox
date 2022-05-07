namespace VamToolbox.Sqlite;

public sealed class ReferenceEntry
{
    public string? Value { get; init; }
    public int Index { get; init; }
    public int Length { get; init; }
    public string? MorphName { get; set; }
    public string? InternalId { get; set; }
    public string FilePath { get; set; } = null!;
    public string LocalPath { get; set; } = null!;
}