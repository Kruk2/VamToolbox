﻿namespace VamRepacker.Sqlite;

public class ReferenceEntry
{
    public string Value { get; init; }
    public int Index { get; init; }
    public int Length { get; init; }
    public string MorphName { get; set; }
    public string InternalId { get; set; }
    public string FilePath { get; set; }
    public string LocalJsonPath { get; set; }
}