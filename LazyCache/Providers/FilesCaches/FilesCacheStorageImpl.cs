using System.IO;

namespace LazyCache.Providers.FilesCaches;

public sealed class FilesCacheStorageImpl(DirectoryInfo rootStorageDirectory)
    : ICacheStorage
{
    public FilesCacheStorageImpl(string rootStorageDirectory)
        : this(new DirectoryInfo(rootStorageDirectory))
    {
    }
}