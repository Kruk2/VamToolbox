using System.Security.Cryptography;
using System.Text;

namespace VamToolbox.Hashing;
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms

public sealed class MD5Helper : IHashingAlgo
{
    public async Task<string> GetHash(Stream stream)
    {
        using var md5 = MD5.Create();
        var hash = await md5.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public static string GetHash(string initial, IEnumerable<string> values)
    {
        if (!values.Any())
            return initial;

        using var md5 = MD5.Create();
        var bytes = Encoding.ASCII.GetBytes(initial + string.Join(string.Empty, values));
        var hash = md5.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}