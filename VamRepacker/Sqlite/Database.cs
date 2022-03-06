using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace VamRepacker.Sqlite;

public class Database : IDatabase
{
    private const string FilesTable = "Files";
    private const string JsonTable = "JsonFiles";
    private const string RefTable= "JsonReferences";
    private SqliteConnection _connection;

    public void Open()
    {
        if (_connection != null) return;

        var currentDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "vamRepacker.sqlite");
        _connection = new SqliteConnection($@"data source={currentDir}");
        _connection.Open();

        CreateHashTable();
        CreateFilesTable();
        CreateJsonFilesTable();
        CreateJsonReferencesTable();
    }

    private void CreateJsonReferencesTable()
    {
        if (!TableExists(RefTable)) return;

        _connection.Execute($"Create Table {RefTable} (" +
                            "Id integer PRIMARY KEY AUTOINCREMENT NOT NULL," +
            "LocalPath TEXT collate nocase NOT NULL," +
            "ParentJsonId integer NOT NULL," +
            "FOREIGN KEY(ParentJsonId) REFERENCES JsonFiles(Id));");
    }

    private void CreateJsonFilesTable()
    {
        if (!TableExists(JsonTable)) return;

        _connection.Execute($"Create Table {JsonTable} (" +
                            "Id integer PRIMARY KEY AUTOINCREMENT NOT NULL," +
            "LocalPath TEXT collate nocase NOT NULL," +
            "ParentFileId integer NOT NULL," +
            "FOREIGN KEY(ParentFileId) REFERENCES Files(Id));");
    }

    private void CreateFilesTable()
    {
        if (!TableExists(FilesTable)) return;

        _connection.Execute($"Create Table {FilesTable} (" +
                            "Id integer PRIMARY KEY AUTOINCREMENT NOT NULL," +
                            "Path TEXT collate nocase NOT NULL," +
                            "FileSize integer NOT NULL);");

        _connection.Execute("CREATE UNIQUE INDEX IX_Files ON Files(Path);");
    }

    private void CreateHashTable()
    {
        if (!TableExists("Hashes")) return;

        _connection.Execute("Create Table Hashes (" +
                            "VarFileName TEXT collate nocase NOT NULL," +
                            "LocalAssetPath TEXT collate nocase NOT NULL," +
                            "Hash VARCHAR(42) collate nocase NOT NULL);");
    }

    private bool TableExists(string tableName)
    {
        var table = _connection.Query<string>("SELECT name FROM sqlite_master WHERE type='table' AND name = @tableName;", new { tableName });
        var foundTable = table.FirstOrDefault();
        return string.IsNullOrEmpty(tableName) || foundTable != tableName;
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

    public Task<long?> GetFileSize(string path)
    {
        return _connection.QueryFirstOrDefaultAsync<long?>($"select FileSize from {FilesTable} where Path = @path", new { path });
    }

    public Task AddFile(string path, long fileSize)
    {
        return _connection.QueryAsync($"insert into {FilesTable} (Path, FileSize) VALUES (@path, @fileSize)",
            new { path, fileSize });
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}