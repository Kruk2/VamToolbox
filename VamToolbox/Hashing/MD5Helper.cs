using System.Security.Cryptography;
using System.Text;

namespace VamToolbox.Hashing;
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms

public sealed class MD5Helper : IHashingAlgo
{
    public async Task<string> GetHash(Stream stream)
    {
        var hash = await MD5.HashDataAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();
    }

    public static string GetHash(string initial, IEnumerable<string> values)
    {
        if (!values.Any())
            return initial;

        var bytes = Encoding.ASCII.GetBytes(initial + string.Join(string.Empty, values));
        var hash = MD5.HashData(bytes);
        return BitConverter.ToString(hash).Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();
    }
}