using System;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace LazyCache.Mocks;

public class MockCacheEntry(string key)
    : ICacheEntry
{
    public void Dispose()
    {
    }

    public object Key => key;
    public object Value { get; set; }
    public DateTimeOffset? AbsoluteExpiration { get; set; }
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
    public TimeSpan? SlidingExpiration { get; set; }
    public IList<IChangeToken> ExpirationTokens { get; }
    public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks { get; }
    public CacheItemPriority Priority { get; set; }
    public long? Size { get; set; }
}