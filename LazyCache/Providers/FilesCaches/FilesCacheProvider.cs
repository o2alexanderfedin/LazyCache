using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace LazyCache.Providers.FilesCaches
{
    public sealed class FilesCacheProvider
        : ICacheProvider
    {
        private readonly IMemoryCache _cache;

        public FilesCacheProvider(IMemoryCache cache)
        {
            _cache = cache;
        }

        public void Set(string key, object item, MemoryCacheEntryOptions policy)
        {
            _cache.Set(key, item, policy);
        }

        public object Get(string key)
        {
            return _cache.Get(key);
        }

        public object GetOrCreate<T>(string key, Func<ICacheEntry, T> factory)
        {
            return _cache.GetOrCreate(key, factory);
        }

        public object GetOrCreate<T>(string key, MemoryCacheEntryOptions policy, Func<ICacheEntry, T> factory)
        {
            if (policy == null)
                return _cache.GetOrCreate(key, factory);

            if (!_cache.TryGetValue(key, out var result))
            {
                var entry = _cache.CreateEntry(key);
                // Set the initial options before the factory is fired so that any callbacks
                // that need to be wired up are still added.
                entry.SetOptions(policy);

                if (policy is LazyCacheEntryOptions lazyPolicy && lazyPolicy.ExpirationMode != ExpirationMode.LazyExpiration)
                {
                    var expiryTokenSource = new CancellationTokenSource();
                    var expireToken = new CancellationChangeToken(expiryTokenSource.Token);
                    entry.AddExpirationToken(expireToken);
                    entry.RegisterPostEvictionCallback((keyPost, value, reason, state) =>
                        expiryTokenSource.Dispose());

                    result = factory(entry);

                    expiryTokenSource.CancelAfter(lazyPolicy.ImmediateAbsoluteExpirationRelativeToNow);
                }
                else
                {
                    result = factory(entry);
                }
                entry.SetValue(result);
                // need to manually call dispose instead of having a using
                // in case the factory passed in throws, in which case we
                // do not want to add the entry to the cache
                entry.Dispose();
            }

            return (T)result;
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
        }

        public Task<T> GetOrCreateAsync<T>(string key, Func<ICacheEntry, Task<T>> factory)
        {
            return _cache.GetOrCreateAsync(key, factory);
        }

        public bool TryGetValue<T>(object key, out T value)
        {
            return _cache.TryGetValue(key, out value);
        }


        public void Dispose()
        {
            _cache?.Dispose();
        }
    }
}