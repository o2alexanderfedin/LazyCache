using System;
using LazyCache;
using Microsoft.AspNetCore.Mvc;

namespace CacheDatabaseQueriesApiSample.Controllers;

public class DbTimeController : Controller
{
    private readonly IAppCache _cache;
    private const string _cacheKey = "DbTimeController.Get";
    private readonly DbTimeContext _dbContext;

    public DbTimeController(DbTimeContext context, IAppCache cache)
    {
        _dbContext = context;
        _cache = cache;
    }

    [HttpGet]
    [Route("api/dbtime")]
    public DbTimeEntity Get()
    {
        var actionThatWeWantToCache = () => _dbContext.GeDbTime();

        var cachedDatabaseTime = _cache.GetOrAdd(_cacheKey, actionThatWeWantToCache);

        return cachedDatabaseTime;
    }

    [HttpDelete]
    [Route("api/dbtime")]
    public IActionResult DeleteFromCache()
    {
        _cache.Remove(_cacheKey);
        var friendlyMessage = new {Message = $"Item with key '{_cacheKey}' removed from server in-memory cache"};
        return Ok(friendlyMessage);
    }
}