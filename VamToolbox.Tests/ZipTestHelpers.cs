using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Text;

namespace VamToolbox.Tests;
public static class ZipTestHelpers 
{
    public static Dictionary<string, string> ReadZipFile(MockFileData file)
    {
        using var memoryStream = new MemoryStream(file.Contents);
        using var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read, true);

        var result = new Dictionary<string, string>();
        foreach (var archiveEntry in zipArchive.Entries)
        {
            using var stream = archiveEntry.Open();
            using var readStream = new StreamReader(stream, Encoding.UTF8);
            result[archiveEntry.FullName] = readStream.ReadToEnd();
        }

        return result;
    }

    public static MockFileData CreateMockFile(Dictionary<string, string> files)
    {
        using var memoryStream = CreateZipFile(files);
        return new MockFileData(memoryStream.ToArray());
    }

    public static MemoryStream CreateZipFile(Dictionary<string, string> files)
    {
        var memoryStream = new MemoryStream();
        {
            using var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true);
            foreach (var (key, value) in files) {
                var entry = zipArchive.CreateEntry(key);
                using var stream = entry.Open();
                using var writeStream = new StreamWriter(stream, Encoding.UTF8);
                writeStream.Write(value);
            }
        }

        memoryStream.Position = 0;
        return memoryStream;
    }
}
