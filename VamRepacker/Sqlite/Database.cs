using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using VamRepacker.Helpers;

namespace VamRepacker.Sqlite;

public sealed class Database : IDatabase
{
    private const string FilesTable = "Files";
    private const string JsonTable = "JsonFiles";
    private const string RefTable= "JsonReferences";
    private readonly SqliteConnection _connection;

    public Database(string rootDir)
    {
        var currentDir = Path.Combine(rootDir, "vamRepacker.sqlite");
        _connection = new SqliteConnection($@"data source={currentDir}");
        _connection.Open();
        _connection.Query("PRAGMA journal_mode=WAL");

        CreateHashTable();
        CreateFilesTable();
        CreateJsonFilesTable();
        CreateJsonReferencesTable();
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        _connection.Execute($"Create Index if not exists IX_ParentJsonId on {RefTable} (ParentJsonId)");
        _connection.Execute($"Create Index if not exists IX_ParentFileId on {JsonTable} (ParentFileId)");
    }

    private void CreateJsonReferencesTable()
    {
        if (!TableExists(RefTable)) return;

        _connection.Execute($"Create Table {RefTable} (" +
            "Value TEXT NOT NULL," +
            "MorphName TEXT," +
            "InternalId TEXT," +
            "[Index] INTEGER NOT NULL," +
            "Length INTEGER NOT NULL," +
            "ParentJsonId integer NOT NULL," +
            "CONSTRAINT FK_ParentJsonId FOREIGN KEY(ParentJsonId) REFERENCES JsonFiles(Id) ON DELETE CASCADE);");
    }

    private void CreateJsonFilesTable()
    {
        if (!TableExists(JsonTable)) return;

        _connection.Execute($"Create Table {JsonTable} (" +
                            "Id integer PRIMARY KEY AUTOINCREMENT NOT NULL," +
            "LocalPath TEXT collate nocase," +
            "ParentFileId integer NOT NULL," +
            "CONSTRAINT FK_ParentFileId FOREIGN KEY(ParentFileId) REFERENCES Files(Id) ON DELETE CASCADE);");

        _connection.Execute($"CREATE UNIQUE INDEX IX_JsonFiles ON {JsonTable}(LocalPath, ParentFileId);");
    }

    private void CreateFilesTable()
    {
        if (!TableExists(FilesTable)) return;

        _connection.Execute($"Create Table {FilesTable} (" +
                            "Id integer PRIMARY KEY AUTOINCREMENT NOT NULL," +
                            "Path TEXT collate nocase NOT NULL," +
                            "FileSize integer NOT NULL," +
                            "ModifiedTime integer NOT NULL);");

        _connection.Execute($"CREATE UNIQUE INDEX IX_Files ON {FilesTable}(Path);");
    }

    private void CreateHashTable()
    {
        if (!TableExists("Hashes")) return;

        _connection.Execute("Create Table Hashes (" +
                            "FullPath TEXT collate nocase NOT NULL," +
                            "LocalAssetPath TEXT collate nocase NOT NULL," +
                            "Hash VARCHAR(42) collate nocase NOT NULL);");
    }

    private bool TableExists(string tableName)
    {
        var table = _connection.Query<string>("SELECT name FROM sqlite_master WHERE type='table' AND name = @tableName;", new { tableName });
        var foundTable = table.FirstOrDefault();
        return string.IsNullOrEmpty(tableName) || foundTable != tableName;
    }

    public async Task<ConcurrentDictionary<(string fullPath, string localAssetPath), string>> GetHashes()
    {
        var hashes = await _connection.QueryAsync<(string fullPath, string localAssethPath, string hash)>("SELECT * FROM hashes");

        var grouped = hashes
            .Select(t => new KeyValuePair<(string fullPath, string localAssetPath), string>((t.fullPath, t.localAssethPath), t.hash));
        return new ConcurrentDictionary<(string fullPath, string localAssetPath), string>(grouped);

    }

    public async Task AddHashes(ConcurrentDictionary<(string fullPath, string localAssetPath), string> hashes)
    {
        await using var transaction = _connection.BeginTransaction();
        var command = _connection.CreateCommand();
        command.CommandText =
            @"INSERT INTO hashes VALUES($fullPath, $localAssetPath, $hash) ";

        var parameterFullPath = command.CreateParameter();
        parameterFullPath.ParameterName = "$fullPath";
        command.Parameters.Add(parameterFullPath);
        var parameterAsset = command.CreateParameter();
        parameterAsset.ParameterName = "$localAssetPath";
        command.Parameters.Add(parameterAsset);
        var parameterHash = command.CreateParameter();
        parameterHash.ParameterName = "$hash";
        command.Parameters.Add(parameterHash);

        // Insert a lot of data
        foreach (var hash in hashes)
        {
            parameterFullPath.Value = hash.Key.fullPath;
            parameterAsset.Value = hash.Key.localAssetPath;
            parameterHash.Value = hash.Value;
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public (long? size, DateTime? modifiedTime) GetFileInfo(string path)
    {
        return _connection.QueryFirstOrDefault<(long?, DateTime?)>($"select FileSize, ModifiedTime from {FilesTable} where Path = @path", new { path });
    }

    public IEnumerable<ReferenceEntry> ReadReferenceCache()
    {
        return _connection.Query<ReferenceEntry>(
            $"select file.Path as FilePath, json.LocalPath as LocalJsonPath, ref.Value, ref.[Index], ref.Length, ref.MorphName, ref.InternalId from {RefTable} ref " +
            $"inner join {JsonTable} json on json.Id = ref.ParentJsonId " +
            $"inner join {FilesTable} file on file.Id = json.ParentFileId ");
    }

    public IEnumerable<string> ReadScannedFilesCache()
    {
        return _connection.Query<string>($"select Path from {FilesTable}");
    }

    public void UpdateReferences(List<(string filePath, string? jsonLocalPath, IEnumerable<Reference> references)> batch, Dictionary<(string filePath, string? jsonLocalPath), long> jsonFiles)
    {
        using var transaction = _connection.BeginTransaction();
        var command = _connection.CreateCommand();
        command.CommandText =
            $"insert into {RefTable} (Value, [Index], Length, MorphName, InternalId, ParentJsonId) VALUES " +
            "($Value, $Index, $Length, $MorphName, $InternalId, $jsonId)";

        var parameterValue = command.CreateParameter();
        parameterValue.ParameterName = "$Value";
        command.Parameters.Add(parameterValue);
        var parameterIndex = command.CreateParameter();
        parameterIndex.ParameterName = "$Index";
        command.Parameters.Add(parameterIndex);
        var parameterLength = command.CreateParameter();
        parameterLength.ParameterName = "$Length";
        command.Parameters.Add(parameterLength);
        var parameterMorph = command.CreateParameter();
        parameterMorph.ParameterName = "$MorphName";
        command.Parameters.Add(parameterMorph);
        var paramInternalId = command.CreateParameter();
        paramInternalId.ParameterName = "$InternalId";
        command.Parameters.Add(paramInternalId);
        var paramJsonId = command.CreateParameter();
        paramJsonId.ParameterName = "$jsonId";
        command.Parameters.Add(paramJsonId);

        foreach (var (filePath, jsonLocalPath, references) in batch)
        {
            foreach (var reference in references)
            {
                paramJsonId.Value = jsonFiles[(filePath, jsonLocalPath)];
                parameterValue.Value = reference.Value;
                parameterIndex.Value = reference.Index;
                parameterLength.Value = reference.Length;
                parameterMorph.Value = (object?)reference.MorphName ?? DBNull.Value;
                paramInternalId.Value = (object?)reference.InternalId ?? DBNull.Value;
                command.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    public async Task ClearCache()
    {
        await _connection.QueryAsync($"DELETE FROM {RefTable}");
        await _connection.QueryAsync($"DELETE FROM {JsonTable}");
        await _connection.QueryAsync($"DELETE FROM {FilesTable}");
        await _connection.QueryAsync("VACUUM");
    }

    public void SaveFiles(Dictionary<string, (long size, DateTime timestamp, long id)> files)
    {
        using var transaction = _connection.BeginTransaction();
        var commandInsert = _connection.CreateCommand();
        commandInsert.CommandText = $"insert or replace into {FilesTable} (Path, FileSize, ModifiedTime) VALUES ($fullPath, $size, $timestamp); SELECT last_insert_rowid();";

        var paramPath = commandInsert.CreateParameter();
        paramPath.ParameterName = "$fullPath";
        commandInsert.Parameters.Add(paramPath);
        var paramSize = commandInsert.CreateParameter();
        paramSize.ParameterName = "$size";
        commandInsert.Parameters.Add(paramSize);
        var paramTimestamp = commandInsert.CreateParameter();
        paramTimestamp.ParameterName = "$timestamp";
        commandInsert.Parameters.Add(paramTimestamp);

        foreach (var (filePath, (size, timestamp, _)) in files.ToList())
        {
            paramPath.Value = filePath;
            paramSize.Value = size;
            paramTimestamp.Value = timestamp;
            files[filePath] = (size, timestamp, (long)commandInsert.ExecuteScalar()!);
        }

        transaction.Commit();
    }

    public void UpdateJson(Dictionary<(string filePath, string? jsonLocalPath), long> jsonFiles,  Dictionary<string, long> files)
    {
        using var transaction = _connection.BeginTransaction();
        var commandInsert = _connection.CreateCommand();
        commandInsert.CommandText = $"insert into {JsonTable} (LocalPath, ParentFileId) VALUES ($localPath, $fileId); SELECT last_insert_rowid();";

        var paramPath = commandInsert.CreateParameter();
        paramPath.ParameterName = "$localPath";
        commandInsert.Parameters.Add(paramPath);
        var paramFileId = commandInsert.CreateParameter();
        paramFileId.ParameterName = "$fileId";
        commandInsert.Parameters.Add(paramFileId);


        foreach (var ((filePath, jsonLocalPath), _) in jsonFiles.ToList())
        {
            paramPath.Value = (object?)jsonLocalPath ?? DBNull.Value;
            paramFileId.Value = files[filePath];
            jsonFiles[(filePath, jsonLocalPath)] = (long)commandInsert.ExecuteScalar()!;
        }

        transaction.Commit();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}