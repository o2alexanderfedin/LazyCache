using System;
using Microsoft.Extensions.Caching.Memory;

namespace LazyCache;

public class LazyCacheEntryOptions : MemoryCacheEntryOptions
{
    public ExpirationMode ExpirationMode { get; set; }
    public TimeSpan ImmediateAbsoluteExpirationRelativeToNow { get; set; }

    public static LazyCacheEntryOptions WithImmediateAbsoluteExpiration(DateTimeOffset absoluteExpiration)
    {
        var delay = absoluteExpiration.Subtract(DateTimeOffset.UtcNow);
        return new LazyCacheEntryOptions
        {
            AbsoluteExpiration = absoluteExpiration,
            ExpirationMode = ExpirationMode.ImmediateEviction,
            ImmediateAbsoluteExpirationRelativeToNow = delay
        };
    }

    public static LazyCacheEntryOptions WithImmediateAbsoluteExpiration(TimeSpan absoluteExpiration)
    {
        return new LazyCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiration,
            ExpirationMode = ExpirationMode.ImmediateEviction,
            ImmediateAbsoluteExpirationRelativeToNow = absoluteExpiration
        };
    }
}