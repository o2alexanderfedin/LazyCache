using System;

namespace LazyCache;

public static class LazyCacheEntryOptionsExtension {
    public static LazyCacheEntryOptions SetAbsoluteExpiration(this LazyCacheEntryOptions option, DateTimeOffset absoluteExpiration,
        ExpirationMode mode)
    {
        if (option == null) throw new ArgumentNullException(nameof(option));

        var delay = absoluteExpiration.Subtract(DateTimeOffset.UtcNow);
        option.AbsoluteExpiration = absoluteExpiration;
        option.ExpirationMode = mode;
        option.ImmediateAbsoluteExpirationRelativeToNow = delay;
        return option;
    }

    public static LazyCacheEntryOptions SetAbsoluteExpiration(this LazyCacheEntryOptions option, TimeSpan absoluteExpiration,
        ExpirationMode mode)
    {
        if (option == null) throw new ArgumentNullException(nameof(option));

        option.AbsoluteExpirationRelativeToNow = absoluteExpiration;
        option.ExpirationMode = mode;
        option.ImmediateAbsoluteExpirationRelativeToNow = absoluteExpiration;
        return option;
    }
}