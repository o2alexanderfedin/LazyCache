using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.ObjectPool;
using Microsoft.IO;
// ReSharper disable HeapView.ObjectAllocation.Evident

namespace LazyCache.Providers.FilesCaches;

public sealed class FilesCacheStorageImpl(DirectoryInfo storageDir)
    : ICacheStorage
{
    public FilesCacheStorageImpl(string rootStorageDirectory)
        : this(new DirectoryInfo(rootStorageDirectory))
    {
    }

    public int Count() => storageDir
        .EnumerateFiles("*.cache_entry", SearchOption.TopDirectoryOnly)
        .Count();

    public bool Remove(KeyValuePair<object, CacheEntry> entry)
    {
        var file = new FileInfo(Path.Combine(storageDir.FullName, KeyToHashString(entry.Key)));
        if (!file.Exists) 
            return false;
        
        file.Delete();
        return true;
    }

    private string KeyToHashString(object key)
    {
        using var stream = MemoryStreamsPool.GetStream();
        JsonSerializer.Serialize(stream, key);
        stream.Position = 0;
        
        using var hasher = SHA256.Create();
        var hashBytes = hasher.ComputeHash(stream);
        
        return ToStringHash(hashBytes);
    }

    private static string ToStringHash(byte[] hashBytes)
    {
        var strHash = Convert.ToBase64String(hashBytes);
        
        var sb = StringBuilderPool.Get();
        foreach (var ch in strHash) sb.Append(InvalidPathCharsMapping.GetValueOrDefault(ch, ch));
        StringBuilderPool.Return(sb);
        
        return sb.ToString();
    }

    private static ObjectPool<StringBuilder> StringBuilderPool
        => _stringBuilderPool ??= new DefaultObjectPoolProvider().Create(new StringBuilderPooledObjectPolicy());

    private static IReadOnlyDictionary<char, char> InvalidPathCharsMapping
        => _invalidPathCharsMapping ??= Path
            .GetInvalidPathChars()
            .Select((ch, index) => (ch, index))
            .ToDictionary(x => x.ch, x => ValidPathChars[x.index % ValidPathChars.Length]);

    private static ObjectPool<StringBuilder>? _stringBuilderPool;
    private const string ValidPathChars = "0123456789abcdefjhijklmnopqrstuvwxyz";
    private static IReadOnlyDictionary<char, char>? _invalidPathCharsMapping;
    private static readonly RecyclableMemoryStreamManager MemoryStreamsPool = new();
}