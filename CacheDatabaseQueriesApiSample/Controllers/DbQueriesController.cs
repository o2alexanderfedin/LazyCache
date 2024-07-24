using Microsoft.AspNetCore.Mvc;

namespace CacheDatabaseQueriesApiSample.Controllers;

public class DbQueriesController : Controller
{
    [HttpGet]
    [Route("api/dbQueries")]
    public int GetDatabaseRequestCounter()
    {
        return DbTimeContext.DatabaseRequestCounter();
    }
}