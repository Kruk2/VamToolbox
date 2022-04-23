using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using VamRepacker.Helpers;

namespace VamRepacker.Sqlite;

public interface IDatabase : IDisposable
{
    Task<ConcurrentDictionary<(string fullPath, string localAssetPath), string>> GetHashes();
    Task AddHashes(ConcurrentDictionary<(string fullPath, string localAssetPath), string> hashes);
    (long? size, DateTime? modifiedTime) GetFileInfo(string path);
    IEnumerable<ReferenceEntry> ReadReferenceCache();
    IEnumerable<string> ReadScannedFilesCache();
    public void SaveFiles(Dictionary<string, (long size, DateTime timestamp, long id)> files);
    void UpdateJson(Dictionary<(string filePath, string? jsonLocalPath), long> jsonFiles, Dictionary<string, long> files);
    void UpdateReferences(List<(string filePath, string? jsonLocalPath, IEnumerable<Reference> references)> batch, Dictionary<(string filePath, string? jsonLocalPath), long> jsonFiles);
}