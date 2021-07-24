using System.IO;
using System.Threading.Tasks;

namespace VamRepacker.Hashing
{
    public interface IHashingAlgo
    {
        Task<string> GetHash(Stream stream);
    }
}