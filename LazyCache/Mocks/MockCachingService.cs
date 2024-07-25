using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Moq;

// ReSharper disable HeapView.ObjectAllocation.Evident

namespace LazyCache.Mocks;

/// <summary>
///     A mock implementation IAppCache that does not do any caching.
///     Useful in unit tests or for feature switching to swap in a dependency to disable all caching
/// </summary>
public class MockCachingService<T>
    : IAppCache
{
    public ICacheProvider CacheProvider { get; } = MockCacheProvider().Object;

    private static Mock<ICacheProvider> MockCacheProvider()
    {
        var mock = new Mock<ICacheProvider>(MockBehavior.Loose);
        
        mock.Setup(x => x.Set(It.IsNotNull<string>(), It.IsNotNull<object>(), It.IsNotNull<MemoryCacheEntryOptions>()));
        
        mock.Setup(x => x.Get(It.IsNotNull<string>())).Returns(null!);
        
        mock.Setup(x => x.GetOrCreate<T>(It.IsNotNull<string>(), It.IsNotNull<Func<ICacheEntry, T>>()))
            .Callback<string, Func<ICacheEntry,T>>((key, func) => func(null!));
        
        mock.Setup(x => x.GetOrCreate<T>(It.IsNotNull<string>(), It.IsNotNull<MemoryCacheEntryOptions>(), It.IsNotNull<Func<ICacheEntry, T>>()))
            .Callback<string, MemoryCacheEntryOptions, Func<ICacheEntry,T>>((key, policy, func) => func(null!));

        mock.Setup(x => x.Remove(It.IsNotNull<string>()));

        mock.Setup(x => x.GetOrCreateAsync<T>(It.IsNotNull<string>(), It.IsNotNull<Func<ICacheEntry, Task<T>>>()))
            .Callback<string, Func<ICacheEntry, Task<T>>>((key, func) => func(null!));

        mock.Setup(x => x.TryGetValue<T>(It.IsNotNull<object>(), out It.Ref<T>.IsAny))
            .Throws<NotImplementedException>();

        mock.Setup(x => x.Dispose());
        
        return mock;
    }

    public CacheDefaults DefaultCachePolicy { get; set; } = new();

    public T Get<T>(string key)
    {
        return default(T);
    }

    public T GetOrAdd<T>(string key, Func<ICacheEntry, T> addItemFactory)
    {
        return addItemFactory(new MockCacheEntry(key));
    }

    public T GetOrAdd<T>(string key, Func<ICacheEntry, T> addItemFactory, MemoryCacheEntryOptions policy)
    {
        return addItemFactory(new MockCacheEntry(key));
    }

    public Task<T> GetOrAddAsync<T>(string key, Func<ICacheEntry, Task<T>> addItemFactory,
        MemoryCacheEntryOptions policy)
    {
        return addItemFactory(new MockCacheEntry(key));
    }

    public void Remove(string key)
    {
    }

    public Task<T> GetOrAddAsync<T>(string key, Func<ICacheEntry, Task<T>> addItemFactory)
    {
        return addItemFactory(new MockCacheEntry(key));
    }

    public Task<T> GetAsync<T>(string key)
    {
        return Task.FromResult(default(T));
    }

    public void Add<T>(string key, T item, MemoryCacheEntryOptions policy)
    {
    }

    public bool TryGetValue<T>(string key, out T value)
    {
        value = default(T);
        return true;
    }
}