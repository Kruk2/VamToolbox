using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using VamRepacker.Models;

namespace VamRepacker.Sqlite
{
    public interface IDatabase : IDisposable
    {
        Task<ConcurrentDictionary<HashesTable, string>> GetHashes();
        Task AddHashes(IEnumerable<HashesTable> hashes);
        Task<long?> GetFileSize(string path);
        Task AddFile(string path, long fileSize);
    }
}
