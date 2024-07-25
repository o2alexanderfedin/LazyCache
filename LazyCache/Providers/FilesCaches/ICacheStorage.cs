
// ReSharper disable HeapView.ObjectAllocation.Evident
// ReSharper disable ConvertToPrimaryConstructor

namespace LazyCache.Providers.FilesCaches;

public interface ICacheStorage
{
    int Count();
}