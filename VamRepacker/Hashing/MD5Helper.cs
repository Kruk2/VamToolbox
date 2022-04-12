using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace VamRepacker.Hashing;

public class MD5Helper : IHashingAlgo
{
    public async Task<string> GetHash(Stream stream)
    {
        using var md5 = MD5.Create();
        var hash = await md5.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public static string GetHash(string initial, IEnumerable<string> values)
    {
        if (initial == null)
            return null;
        if (!values.Any())
            return initial;

        using var md5 = MD5.Create();
        var bytes = Encoding.ASCII.GetBytes(initial + string.Join(string.Empty, values));
        var hash = md5.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}