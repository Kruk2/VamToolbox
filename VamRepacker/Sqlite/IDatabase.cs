using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using VamRepacker.Helpers;
using VamRepacker.Models;

namespace VamRepacker.Sqlite;

public interface IDatabase : IDisposable
{
    Task<ConcurrentDictionary<(string fullPath, string localAssetPath), string>> GetHashes();
    Task AddHashes(ConcurrentDictionary<(string fullPath, string localAssetPath), string> hashes);
    IEnumerable<ReferenceEntry> ReadReferenceCache();
    IEnumerable<(string fullPath, string localPath, long size, DateTime modifiedTime, string? uuid)> ReadVarFilesCache();
    IEnumerable<(string fullPath, long size, DateTime modifiedTime, string? uuid)> ReadFreeFilesCache();

    public void SaveFiles(Dictionary<FileReferenceBase, long> files);
    void UpdateReferences(Dictionary<FileReferenceBase, long> batch, List<(FileReferenceBase file, IEnumerable<Reference> references)> jsonFiles);
    Task ClearCache();
    void EnsureCreated();
}