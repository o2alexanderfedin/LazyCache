using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
// ReSharper disable HeapView.DelegateAllocation
// ReSharper disable HeapView.ObjectAllocation.Evident

namespace LazyCache.Providers.FilesCaches;

/// <summary>
/// An implementation of <see cref="T:Microsoft.Extensions.Caching.Memory.IMemoryCache" /> using a dictionary to
/// store its entries.
/// </summary>
public sealed class FilesCacheImpl
    : IFilesCache, IDisposable
{
    private readonly ConcurrentDictionary<object, CacheEntry> _entries;
    
    private long _cacheSize;
    private bool _disposed;
    private readonly Action<CacheEntry> _setEntry;
    private readonly Action<CacheEntry> _entryExpirationNotification;
    private readonly MemoryCacheOptions _options;
    private DateTimeOffset _lastExpirationScan;

    /// <summary>
    /// Creates a new <see cref="T:LazyCache.Providers.FilesCaches.MemoryCache" /> instance.
    /// </summary>
    /// <param name="optionsAccessor">The options of the cache.</param>
    public FilesCacheImpl(IOptions<MemoryCacheOptions> optionsAccessor)
    {
        _options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
        _entries = new ConcurrentDictionary<object, CacheEntry>();
        _setEntry = SetEntry;
        _entryExpirationNotification = EntryExpired;
        _options.Clock ??= new SystemClock();
        _lastExpirationScan = _options.Clock.UtcNow;
    }

    /// <summary>Cleans up the background collection events.</summary>
    ~FilesCacheImpl() => Dispose(false);

    /// <summary>
    /// Gets the count of the current entries for diagnostic purposes.
    /// </summary>
    public int Count => _entries.Count;

    internal long Size => Interlocked.Read(ref _cacheSize);

    private ICollection<KeyValuePair<object, CacheEntry>> EntriesCollection
    {
        get => _entries;
    }

    /// <inheritdoc />
    public ICacheEntry CreateEntry(object key)
    {
        CheckDisposed();
        ValidateCacheKey(key);
        return new CacheEntry(key, _setEntry, _entryExpirationNotification);
    }

    private void SetEntry(CacheEntry entry)
    {
        if (_disposed)
            return;
        long? nullable1 = _options.SizeLimit;
        if (nullable1.HasValue)
        {
            nullable1 = entry.Size;
            if (!nullable1.HasValue)
                throw new InvalidOperationException(string.Format(
                    "Cache entry must specify a value for {0} when {1} is set.", "Size",
                    "SizeLimit"));
        }

        DateTimeOffset utcNow = _options.Clock.UtcNow;
        DateTimeOffset? nullable2 = new DateTimeOffset?();
        if (entry._absoluteExpirationRelativeToNow.HasValue)
        {
            DateTimeOffset dateTimeOffset = utcNow;
            TimeSpan? expirationRelativeToNow = entry._absoluteExpirationRelativeToNow;
            nullable2 = expirationRelativeToNow.HasValue
                ? new DateTimeOffset?(dateTimeOffset + expirationRelativeToNow.GetValueOrDefault())
                : new DateTimeOffset?();
        }
        else if (entry._absoluteExpiration.HasValue)
            nullable2 = entry._absoluteExpiration;

        if (nullable2.HasValue &&
            (!entry._absoluteExpiration.HasValue || nullable2.Value < entry._absoluteExpiration.Value))
            entry._absoluteExpiration = nullable2;
        entry.LastAccessed = utcNow;
        CacheEntry cacheEntry;
        if (_entries.TryGetValue(entry.Key, out cacheEntry))
            cacheEntry.SetExpired(EvictionReason.Replaced);
        bool flag1 = UpdateCacheSizeExceedsCapacity(entry);
        if (!entry.CheckExpired(utcNow) && !flag1)
        {
            bool flag2;
            if (cacheEntry == null)
            {
                flag2 = _entries.TryAdd(entry.Key, entry);
            }
            else
            {
                flag2 = _entries.TryUpdate(entry.Key, entry, cacheEntry);
                if (flag2)
                {
                    nullable1 = _options.SizeLimit;
                    if (nullable1.HasValue)
                    {
                        ref long local = ref _cacheSize;
                        nullable1 = cacheEntry.Size;
                        long num = -nullable1.Value;
                        Interlocked.Add(ref local, num);
                    }
                }
                else
                    flag2 = _entries.TryAdd(entry.Key, entry);
            }

            if (flag2)
            {
                entry.AttachTokens();
            }
            else
            {
                nullable1 = _options.SizeLimit;
                if (nullable1.HasValue)
                {
                    ref long local = ref _cacheSize;
                    nullable1 = entry.Size;
                    long num = -nullable1.Value;
                    Interlocked.Add(ref local, num);
                }

                entry.SetExpired(EvictionReason.Replaced);
                entry.InvokeEvictionCallbacks();
            }

            cacheEntry?.InvokeEvictionCallbacks();
        }
        else
        {
            if (flag1)
            {
                entry.SetExpired(EvictionReason.Capacity);
                TriggerOvercapacityCompaction();
            }

            entry.InvokeEvictionCallbacks();
            if (cacheEntry != null)
                RemoveEntry(cacheEntry);
        }

        StartScanForExpiredItems();
    }

    /// <inheritdoc />
    public bool TryGetValue(object key, out object result)
    {
        ValidateCacheKey(key);
        CheckDisposed();
        result = null;
        DateTimeOffset utcNow = _options.Clock.UtcNow;
        bool flag = false;
        CacheEntry entry;
        if (_entries.TryGetValue(key, out entry))
        {
            if (entry.CheckExpired(utcNow) && entry.EvictionReason != EvictionReason.Replaced)
            {
                RemoveEntry(entry);
            }
            else
            {
                flag = true;
                entry.LastAccessed = utcNow;
                result = entry.Value;
                entry.PropagateOptions(CacheEntryHelper.Current);
            }
        }

        StartScanForExpiredItems();
        return flag;
    }

    /// <inheritdoc />
    public void Remove(object key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        CheckDisposed();
        CacheEntry cacheEntry;
        if (_entries.TryRemove(key, out cacheEntry))
        {
            long? nullable = _options.SizeLimit;
            if (nullable.HasValue)
            {
                ref long local = ref _cacheSize;
                nullable = cacheEntry.Size;
                long num = -nullable.Value;
                Interlocked.Add(ref local, num);
            }

            cacheEntry.SetExpired(EvictionReason.Removed);
            cacheEntry.InvokeEvictionCallbacks();
        }

        StartScanForExpiredItems();
    }

    private void RemoveEntry(CacheEntry entry)
    {
        if (!EntriesCollection.Remove(new KeyValuePair<object, CacheEntry>(entry.Key, entry)))
            return;
        if (_options.SizeLimit.HasValue)
            Interlocked.Add(ref _cacheSize, -entry.Size.Value);
        entry.InvokeEvictionCallbacks();
    }

    private void EntryExpired(CacheEntry entry)
    {
        RemoveEntry(entry);
        StartScanForExpiredItems();
    }

    private void StartScanForExpiredItems()
    {
        DateTimeOffset utcNow = _options.Clock.UtcNow;
        if (!(_options.ExpirationScanFrequency < utcNow - _lastExpirationScan))
            return;
        _lastExpirationScan = utcNow;
        Task.Factory.StartNew((Action<object>)(state => ScanForExpiredItems((FilesCacheImpl)state)), this,
            CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
    }

    private static void ScanForExpiredItems(FilesCacheImpl cache)
    {
        DateTimeOffset utcNow = cache._options.Clock.UtcNow;
        foreach (CacheEntry entry in cache._entries.Values)
        {
            if (entry.CheckExpired(utcNow))
                cache.RemoveEntry(entry);
        }
    }

    private bool UpdateCacheSizeExceedsCapacity(CacheEntry entry)
    {
        if (!_options.SizeLimit.HasValue)
            return false;
        for (int index = 0; index < 100; ++index)
        {
            long comparand = Interlocked.Read(ref _cacheSize);
            long num1 = comparand;
            long? nullable = entry.Size;
            long num2 = nullable.Value;
            long num3 = num1 + num2;
            if (num3 >= 0L)
            {
                long num4 = num3;
                nullable = _options.SizeLimit;
                long valueOrDefault = nullable.GetValueOrDefault();
                if ((num4 > valueOrDefault ? (nullable.HasValue ? 1 : 0) : 0) == 0)
                {
                    if (comparand == Interlocked.CompareExchange(ref _cacheSize, num3, comparand))
                        return false;
                    continue;
                }
            }

            return true;
        }

        return true;
    }

    private void TriggerOvercapacityCompaction()
    {
        ThreadPool.QueueUserWorkItem((WaitCallback)(s => OvercapacityCompaction((FilesCacheImpl)s)), this);
    }

    private static void OvercapacityCompaction(FilesCacheImpl cache)
    {
        long num1 = Interlocked.Read(ref cache._cacheSize);
        long? sizeLimit = cache._options.SizeLimit;
        double? nullable1 = sizeLimit.HasValue ? new double?(sizeLimit.GetValueOrDefault()) : new double?();
        double num2 = 1.0 - cache._options.CompactionPercentage;
        double? nullable2 = nullable1.HasValue ? new double?(nullable1.GetValueOrDefault() * num2) : new double?();
        double num3 = num1;
        nullable1 = nullable2;
        double valueOrDefault = nullable1.GetValueOrDefault();
        if ((num3 > valueOrDefault ? (nullable1.HasValue ? 1 : 0) : 0) == 0)
            return;
        cache.Compact(num1 - (long)nullable2.Value, (Func<CacheEntry, long>)(entry => entry.Size.Value));
    }

    /// Remove at least the given percentage (0.10 for 10%) of the total entries (or estimated memory?), according to the following policy:
    ///             1. Remove all expired items.
    ///             2. Bucket by CacheItemPriority.
    ///             3. Least recently used objects.
    ///             ?. Items with the soonest absolute expiration.
    ///             ?. Items with the soonest sliding expiration.
    ///             ?. Larger objects - estimated by object graph size, inaccurate.
    public void Compact(double percentage)
    {
        Compact((int)(_entries.Count * percentage), (Func<CacheEntry, long>)(_ => 1L));
    }

    private void Compact(long removalSizeTarget, Func<CacheEntry, long> computeEntrySize)
    {
        List<CacheEntry> entriesToRemove = new List<CacheEntry>();
        List<CacheEntry> priorityEntries1 = new List<CacheEntry>();
        List<CacheEntry> priorityEntries2 = new List<CacheEntry>();
        List<CacheEntry> priorityEntries3 = new List<CacheEntry>();
        long removedSize = 0;
        DateTimeOffset utcNow = _options.Clock.UtcNow;
        foreach (CacheEntry cacheEntry in _entries.Values)
        {
            if (cacheEntry.CheckExpired(utcNow))
            {
                entriesToRemove.Add(cacheEntry);
                removedSize += computeEntrySize(cacheEntry);
            }
            else
            {
                switch (cacheEntry.Priority)
                {
                    case CacheItemPriority.Low:
                        priorityEntries1.Add(cacheEntry);
                        continue;
                    case CacheItemPriority.Normal:
                        priorityEntries2.Add(cacheEntry);
                        continue;
                    case CacheItemPriority.High:
                        priorityEntries3.Add(cacheEntry);
                        continue;
                    case CacheItemPriority.NeverRemove:
                        continue;
                    default:
                        throw new NotSupportedException("Not implemented: " + cacheEntry.Priority);
                }
            }
        }

        ExpirePriorityBucket(ref removedSize, removalSizeTarget, computeEntrySize, entriesToRemove,
            priorityEntries1);
        ExpirePriorityBucket(ref removedSize, removalSizeTarget, computeEntrySize, entriesToRemove,
            priorityEntries2);
        ExpirePriorityBucket(ref removedSize, removalSizeTarget, computeEntrySize, entriesToRemove,
            priorityEntries3);
        foreach (CacheEntry entry in entriesToRemove)
            RemoveEntry(entry);
    }

    /// Policy:
    ///             1. Least recently used objects.
    ///             ?. Items with the soonest absolute expiration.
    ///             ?. Items with the soonest sliding expiration.
    ///             ?. Larger objects - estimated by object graph size, inaccurate.
    private void ExpirePriorityBucket(
        ref long removedSize,
        long removalSizeTarget,
        Func<CacheEntry, long> computeEntrySize,
        List<CacheEntry> entriesToRemove,
        List<CacheEntry> priorityEntries)
    {
        if (removalSizeTarget <= removedSize)
            return;
        foreach (CacheEntry cacheEntry in Enumerable.OrderBy<CacheEntry, DateTimeOffset>(
                     priorityEntries,
                     (Func<CacheEntry, DateTimeOffset>)(entry => entry.LastAccessed)))
        {
            cacheEntry.SetExpired(EvictionReason.Capacity);
            entriesToRemove.Add(cacheEntry);
            removedSize += computeEntrySize(cacheEntry);
            if (removalSizeTarget <= removedSize)
                break;
        }
    }

    public void Dispose() => Dispose(true);

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        if (disposing)
            GC.SuppressFinalize(this);
        _disposed = true;
    }

    private void CheckDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(typeof(FilesCacheImpl).FullName);
    }

    private static void ValidateCacheKey(object key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
    }
}