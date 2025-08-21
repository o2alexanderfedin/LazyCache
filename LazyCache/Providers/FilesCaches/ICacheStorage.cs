
// ReSharper disable HeapView.ObjectAllocation.Evident
// ReSharper disable ConvertToPrimaryConstructor

using System.Collections.Generic;

namespace LazyCache.Providers.FilesCaches;

public interface ICacheStorage
{
    int Count();
    bool Remove(KeyValuePair<object, CacheEntry> entry);
}