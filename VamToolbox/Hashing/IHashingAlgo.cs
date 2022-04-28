namespace VamToolbox.Hashing;

public interface IHashingAlgo
{
    Task<string> GetHash(Stream stream);
}