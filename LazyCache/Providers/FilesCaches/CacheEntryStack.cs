#nullable disable
using System;
// ReSharper disable HeapView.ObjectAllocation.Evident
// ReSharper disable NotAccessedField.Local

namespace LazyCache.Providers.FilesCaches;

internal class CacheEntryStack
{
    private readonly CacheEntryStack _previous;
    private readonly CacheEntry _entry;

    private CacheEntryStack()
    {
    }

    private CacheEntryStack(CacheEntryStack previous, CacheEntry entry)
    {
        _previous = previous ?? throw new ArgumentNullException(nameof (previous));
        _entry = entry;
    }

    public static CacheEntryStack Empty { get; } = new();

    public CacheEntryStack Push(CacheEntry c) => new(this, c);

    public CacheEntry Peek() => _entry;
}