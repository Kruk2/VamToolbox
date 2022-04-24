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
    (long? size, DateTime? modifiedTime, string? uuid) GetFileInfo(string path, string? localPath);
    IEnumerable<ReferenceEntry> ReadReferenceCache();
    public void SaveFiles(Dictionary<FileReferenceBase, long> files);
    void UpdateReferences(Dictionary<FileReferenceBase, long> batch, List<(FileReferenceBase file, IEnumerable<Reference> references)> jsonFiles);
    Task ClearCache();
    void EnsureCreated();
}