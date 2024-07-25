using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
// ReSharper disable UnusedMember.Global
// ReSharper disable HeapView.ObjectAllocation.Evident

namespace LazyCache.Providers.FilesCaches;

public record FilesCacheOptions
    : IOptions<FilesCacheOptions>
{
    private string? _storageDir;
    private long? _sizeLimit;
    private double _compactionPercentage = 0.05;

    public ISystemClock? Clock { get; set; }

    /// <summary>
    /// Gets or sets root storage directory
    /// </summary>
    public string StorageDir
    {
        get
        {
            return _storageDir ??= Path.Combine(new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? ".x"),
                "data_cache",
            }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray());
        }
        set => _storageDir = value;
    }

    /// <summary>
    /// Gets or sets the minimum length of time between successive scans for expired items.
    /// </summary>
    public TimeSpan ExpirationScanFrequency { get; set; } = TimeSpan.FromMinutes(1.0);

    /// <summary>Gets or sets the maximum size of the cache.</summary>
    public long? SizeLimit
    {
        get => _sizeLimit;
        set
        {
            const long zero = 0;
            if (value is < 0L)
                throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(value)} must be non-negative.");
            _sizeLimit = value;
        }
    }

    /// <summary>
    /// Gets or sets the amount to compact the cache by when the maximum size is exceeded.
    /// </summary>
    public double CompactionPercentage
    {
        get => _compactionPercentage;
        set => _compactionPercentage = value is >= 0.0 and <= 1.0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(value)} must be between 0 and 1 inclusive.");
    }

    FilesCacheOptions IOptions<FilesCacheOptions>.Value => this;
}