using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace VamRepacker.Hashing
{
    public class Database : IDatabase
    {
        private SqliteConnection _connection;

        public void Open(string vamDir)
        {
            var currentDir = Path.Combine(vamDir, "hashes.sqlite");
            _connection = new SqliteConnection($@"data source={currentDir}");
            _connection.Open();

            var table = _connection.Query<string>("SELECT name FROM sqlite_master WHERE type='table' AND name = 'Hashes';");
            var tableName = table.FirstOrDefault();
            if (!string.IsNullOrEmpty(tableName) && tableName == "Hashes")
                return;

            _connection.Execute("Create Table Hashes (" +
                                "VarFileName TEXT collate nocase NOT NULL," +
                                "LocalAssetPath TEXT collate nocase NOT NULL," +
                                "Hash VARCHAR(42) collate nocase NOT NULL);");
        }

        public async Task<ConcurrentDictionary<HashesTable, string>> GetHashes()
        {
            var hashes = await _connection.QueryAsync<HashesTable>("SELECT * FROM hashes");

            var grouped = hashes
                .Select(t => new KeyValuePair<HashesTable, string>(t, t.Hash));
            return new ConcurrentDictionary<HashesTable, string>(grouped);

        }

        public async Task AddHashes(IEnumerable<HashesTable> hashes)
        {
            await using var transaction = _connection.BeginTransaction();
            var command = _connection.CreateCommand();
            command.CommandText =
                @"INSERT INTO hashes VALUES($varFileName, $localAssetPath, $hash) ";

            var parameterVar = command.CreateParameter();
            parameterVar.ParameterName = "$varFileName";
            command.Parameters.Add(parameterVar);
            var parameterAsset = command.CreateParameter();
            parameterAsset.ParameterName = "$localAssetPath";
            command.Parameters.Add(parameterAsset);
            var parameterHash = command.CreateParameter();
            parameterHash.ParameterName = "$hash";
            command.Parameters.Add(parameterHash);

            // Insert a lot of data
            foreach (var hash in hashes)
            {
                parameterVar.Value = hash.VarFileName;
                parameterAsset.Value = hash.LocalAssetPath;
                parameterHash.Value = hash.Hash;
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        public class HashesTable
        {
            protected bool Equals(HashesTable other)
            {
                return string.Equals(VarFileName, other.VarFileName, StringComparison.OrdinalIgnoreCase) && 
                       string.Equals(LocalAssetPath, other.LocalAssetPath, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
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
    }

    public interface IDatabase : IDisposable
    {
        void Open(string vamDir);
        Task<ConcurrentDictionary<Database.HashesTable, string>> GetHashes();
        Task AddHashes(IEnumerable<Database.HashesTable> hashes);
    }
}
