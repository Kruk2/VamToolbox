using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Sqlite;
using VamRepacker.Helpers;
using VamRepacker.Models;

namespace VamRepacker.Sqlite
{
    public interface IDatabase : IDisposable
    {
        Task<ConcurrentDictionary<HashesTable, string>> GetHashes();
        Task AddHashes(IEnumerable<HashesTable> hashes);
        (long? id, long? size) GetFileSize(string path);
        IEnumerable<ReferenceEntry> ReadReferenceCache();
        void SaveFiles(Dictionary<string, (long size, long id)> files);
        void UpdateJson(Dictionary<(string filePath, string jsonLocalPath), long> jsonFiles, Dictionary<string, (long size, long id)> files);
        void UpdateReferences(List<(string filePath, string jsonLocalPath, IEnumerable<Reference> references)> references, Dictionary<(string filePath, string jsonLocalPath), long> jsonFiles);
    }
}
