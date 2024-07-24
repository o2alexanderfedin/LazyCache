using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace LazyCache.Providers.FilesCaches
{
    internal class CacheEntry
        : ICacheEntry, IDisposable
    {
        private bool _added;

        private static readonly Action<object> ExpirationCallback =
            new Action<object>(ExpirationTokensExpired);

        private readonly Action<CacheEntry> _notifyCacheOfExpiration;
        private readonly Action<CacheEntry> _notifyCacheEntryDisposed;
        private IList<IDisposable> _expirationTokenRegistrations;
        private IList<PostEvictionCallbackRegistration> _postEvictionCallbacks;
        private bool _isExpired;
        internal IList<IChangeToken> _expirationTokens;
        internal DateTimeOffset? _absoluteExpiration;
        internal TimeSpan? _absoluteExpirationRelativeToNow;
        private TimeSpan? _slidingExpiration;
        private long? _size;
        private IDisposable _scope;
        internal readonly object _lock = new object();

        internal CacheEntry(
            object key,
            Action<CacheEntry> notifyCacheEntryDisposed,
            Action<CacheEntry> notifyCacheOfExpiration)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (notifyCacheEntryDisposed == null)
                throw new ArgumentNullException(nameof(notifyCacheEntryDisposed));
            if (notifyCacheOfExpiration == null)
                throw new ArgumentNullException(nameof(notifyCacheOfExpiration));
            Key = key;
            _notifyCacheEntryDisposed = notifyCacheEntryDisposed;
            _notifyCacheOfExpiration = notifyCacheOfExpiration;
            _scope = CacheEntryHelper.EnterScope(this);
        }

        /// <summary>
        /// Gets or sets an absolute expiration date for the cache entry.
        /// </summary>
        public DateTimeOffset? AbsoluteExpiration
        {
            get => _absoluteExpiration;
            set => _absoluteExpiration = value;
        }

        /// <summary>
        /// Gets or sets an absolute expiration time, relative to now.
        /// </summary>
        public TimeSpan? AbsoluteExpirationRelativeToNow
        {
            get => _absoluteExpirationRelativeToNow;
            set
            {
                TimeSpan? nullable = value;
                TimeSpan zero = TimeSpan.Zero;
                if ((nullable.HasValue ? (nullable.GetValueOrDefault() <= zero ? 1 : 0) : 0) != 0)
                    throw new ArgumentOutOfRangeException(nameof(AbsoluteExpirationRelativeToNow), (object)value,
                        "The relative expiration value must be positive.");
                _absoluteExpirationRelativeToNow = value;
            }
        }

        /// <summary>
        /// Gets or sets how long a cache entry can be inactive (e.g. not accessed) before it will be removed.
        /// This will not extend the entry lifetime beyond the absolute expiration (if set).
        /// </summary>
        public TimeSpan? SlidingExpiration
        {
            get => _slidingExpiration;
            set
            {
                TimeSpan? nullable = value;
                TimeSpan zero = TimeSpan.Zero;
                if ((nullable.HasValue ? (nullable.GetValueOrDefault() <= zero ? 1 : 0) : 0) != 0)
                    throw new ArgumentOutOfRangeException(nameof(SlidingExpiration), (object)value,
                        "The sliding expiration value must be positive.");
                _slidingExpiration = value;
            }
        }

        /// <summary>
        /// Gets the <see cref="T:Microsoft.Extensions.Primitives.IChangeToken" /> instances which cause the cache entry to expire.
        /// </summary>
        public IList<IChangeToken> ExpirationTokens
        {
            get
            {
                if (_expirationTokens == null)
                    _expirationTokens = (IList<IChangeToken>)new List<IChangeToken>();
                return _expirationTokens;
            }
        }

        /// <summary>
        /// Gets or sets the callbacks will be fired after the cache entry is evicted from the cache.
        /// </summary>
        public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks
        {
            get
            {
                if (_postEvictionCallbacks == null)
                    _postEvictionCallbacks =
                        (IList<PostEvictionCallbackRegistration>)new List<PostEvictionCallbackRegistration>();
                return _postEvictionCallbacks;
            }
        }

        /// <summary>
        /// Gets or sets the priority for keeping the cache entry in the cache during a
        /// memory pressure triggered cleanup. The default is <see cref="F:Microsoft.Extensions.Caching.Memory.CacheItemPriority.Normal" />.
        /// </summary>
        public CacheItemPriority Priority { get; set; } = CacheItemPriority.Normal;

        /// <summary>Gets or sets the size of the cache entry value.</summary>
        public long? Size
        {
            get => _size;
            set
            {
                long? nullable = value;
                long num = 0;
                if ((nullable.GetValueOrDefault() < num ? (nullable.HasValue ? 1 : 0) : 0) != 0)
                    throw new ArgumentOutOfRangeException(nameof(value), (object)value,
                        string.Format("{0} must be non-negative.", (object)nameof(value)));
                _size = value;
            }
        }

        public object Key { get; private set; }

        public object Value { get; set; }

        internal DateTimeOffset LastAccessed { get; set; }

        internal EvictionReason EvictionReason { get; private set; }

        public void Dispose()
        {
            if (_added)
                return;
            _added = true;
            _scope.Dispose();
            _notifyCacheEntryDisposed(this);
            PropagateOptions(CacheEntryHelper.Current);
        }

        internal bool CheckExpired(DateTimeOffset now)
        {
            return _isExpired || CheckForExpiredTime(now) || CheckForExpiredTokens();
        }

        internal void SetExpired(EvictionReason reason)
        {
            if (EvictionReason == EvictionReason.None)
                EvictionReason = reason;
            _isExpired = true;
            DetachTokens();
        }

        private bool CheckForExpiredTime(DateTimeOffset now)
        {
            if (_absoluteExpiration.HasValue && _absoluteExpiration.Value <= now)
            {
                SetExpired(EvictionReason.Expired);
                return true;
            }

            if (_slidingExpiration.HasValue)
            {
                TimeSpan timeSpan = now - LastAccessed;
                TimeSpan? slidingExpiration = _slidingExpiration;
                if ((slidingExpiration.HasValue ? (timeSpan >= slidingExpiration.GetValueOrDefault() ? 1 : 0) : 0) != 0)
                {
                    SetExpired(EvictionReason.Expired);
                    return true;
                }
            }

            return false;
        }

        internal bool CheckForExpiredTokens()
        {
            if (_expirationTokens != null)
            {
                for (int index = 0; index < _expirationTokens.Count; ++index)
                {
                    if (_expirationTokens[index].HasChanged)
                    {
                        SetExpired(EvictionReason.TokenExpired);
                        return true;
                    }
                }
            }

            return false;
        }

        internal void AttachTokens()
        {
            if (_expirationTokens == null)
                return;
            lock (_lock)
            {
                for (int index = 0; index < _expirationTokens.Count; ++index)
                {
                    IChangeToken expirationToken = _expirationTokens[index];
                    if (expirationToken.ActiveChangeCallbacks)
                    {
                        if (_expirationTokenRegistrations == null)
                            _expirationTokenRegistrations = (IList<IDisposable>)new List<IDisposable>(1);
                        _expirationTokenRegistrations.Add(
                            expirationToken.RegisterChangeCallback(ExpirationCallback, (object)this));
                    }
                }
            }
        }

        private static void ExpirationTokensExpired(object obj)
        {
            Task.Factory.StartNew((Action<object>)(state =>
            {
                CacheEntry cacheEntry = (CacheEntry)state;
                cacheEntry.SetExpired(EvictionReason.TokenExpired);
                cacheEntry._notifyCacheOfExpiration(cacheEntry);
            }), obj, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }

        private void DetachTokens()
        {
            lock (_lock)
            {
                IList<IDisposable> tokenRegistrations = _expirationTokenRegistrations;
                if (tokenRegistrations == null)
                    return;
                _expirationTokenRegistrations = (IList<IDisposable>)null;
                for (int index = 0; index < tokenRegistrations.Count; ++index)
                    tokenRegistrations[index].Dispose();
            }
        }

        internal void InvokeEvictionCallbacks()
        {
            if (_postEvictionCallbacks == null)
                return;
            Task.Factory.StartNew((Action<object>)(state => InvokeCallbacks((CacheEntry)state)),
                (object)this, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }

        private static void InvokeCallbacks(CacheEntry entry)
        {
            IList<PostEvictionCallbackRegistration> callbackRegistrationList =
                Interlocked.Exchange<IList<PostEvictionCallbackRegistration>>(ref entry._postEvictionCallbacks,
                    (IList<PostEvictionCallbackRegistration>)null);
            if (callbackRegistrationList == null)
                return;
            for (int index = 0; index < callbackRegistrationList.Count; ++index)
            {
                PostEvictionCallbackRegistration callbackRegistration = callbackRegistrationList[index];
                try
                {
                    PostEvictionDelegate evictionCallback = callbackRegistration.EvictionCallback;
                    if (evictionCallback != null)
                        evictionCallback(entry.Key, entry.Value, entry.EvictionReason, callbackRegistration.State);
                }
                catch (Exception ex)
                {
                }
            }
        }

        internal void PropagateOptions(CacheEntry parent)
        {
            if (parent == null)
                return;
            if (_expirationTokens != null)
            {
                lock (_lock)
                {
                    lock (parent._lock)
                    {
                        foreach (IChangeToken expirationToken in (IEnumerable<IChangeToken>)_expirationTokens)
                            parent.AddExpirationToken(expirationToken);
                    }
                }
            }

            if (!_absoluteExpiration.HasValue)
                return;
            if (parent._absoluteExpiration.HasValue)
            {
                DateTimeOffset? absoluteExpiration1 = _absoluteExpiration;
                DateTimeOffset? absoluteExpiration2 = parent._absoluteExpiration;
                if ((absoluteExpiration1.HasValue & absoluteExpiration2.HasValue
                        ? (absoluteExpiration1.GetValueOrDefault() < absoluteExpiration2.GetValueOrDefault() ? 1 : 0)
                        : 0) == 0)
                    return;
            }

            parent._absoluteExpiration = _absoluteExpiration;
        }
    }
}