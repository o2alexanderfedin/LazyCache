using Microsoft.Extensions.Caching.Memory;

namespace LazyCache.Providers.FilesCaches;

public interface IFilesCache
    : IMemoryCache
{
}