using System;
using System.Threading;

namespace LazyCache.Providers.FilesCaches;

internal class CacheEntryHelper
{
    private static readonly AsyncLocal<CacheEntryStack> _scopes = new AsyncLocal<CacheEntryStack>();

    internal static CacheEntryStack Scopes
    {
        get => _scopes.Value;
        set => _scopes.Value = value;
    }

    internal static CacheEntry Current => GetOrCreateScopes().Peek();

    internal static IDisposable EnterScope(CacheEntry entry)
    {
        CacheEntryStack scopes = GetOrCreateScopes();
        ScopeLease scopeLease = new ScopeLease(scopes);
        Scopes = scopes.Push(entry);
        return scopeLease;
    }

    private static CacheEntryStack GetOrCreateScopes()
    {
        CacheEntryStack scopes = Scopes;
        if (scopes == null)
        {
            scopes = CacheEntryStack.Empty;
            Scopes = scopes;
        }
        return scopes;
    }

    private sealed class ScopeLease : IDisposable
    {
        private readonly CacheEntryStack _cacheEntryStack;

        public ScopeLease(CacheEntryStack cacheEntryStack) => _cacheEntryStack = cacheEntryStack;

        public void Dispose() => Scopes = _cacheEntryStack;
    }
}