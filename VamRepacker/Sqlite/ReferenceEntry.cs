﻿namespace VamRepacker.Sqlite;

public sealed class ReferenceEntry
{
    public string Value { get; init; } = null!;
    public int Index { get; init; }
    public int Length { get; init; }
    public string? MorphName { get; set; }
    public string? InternalId { get; set; }
    public string FilePath { get; set; } = null!;
    public string? LocalJsonPath { get; set; }
}