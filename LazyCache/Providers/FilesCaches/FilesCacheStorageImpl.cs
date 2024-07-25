using System.IO;
using System.Linq;

namespace LazyCache.Providers.FilesCaches;

public sealed class FilesCacheStorageImpl(DirectoryInfo rootStorageDirectory)
    : ICacheStorage
{
    public FilesCacheStorageImpl(string rootStorageDirectory)
        : this(new DirectoryInfo(rootStorageDirectory))
    {
    }

    public int Count() => rootStorageDirectory
        .EnumerateFiles("*.cache_entry", SearchOption.TopDirectoryOnly)
        .Count();
}