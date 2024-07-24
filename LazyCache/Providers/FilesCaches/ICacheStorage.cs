using System.IO;
// ReSharper disable HeapView.ObjectAllocation.Evident
// ReSharper disable ConvertToPrimaryConstructor

namespace LazyCache.Providers.FilesCaches;

public interface ICacheStorage
{
    
}

public sealed class FilesCacheStorage
    : ICacheStorage
{
    private readonly DirectoryInfo _rootStorageDirectory;

    public FilesCacheStorage(DirectoryInfo rootStorageDirectory)
    {
        _rootStorageDirectory = rootStorageDirectory;
    }

    public FilesCacheStorage(string rootStorageDirectory)
        : this(new DirectoryInfo(rootStorageDirectory))
    {
    }
} 