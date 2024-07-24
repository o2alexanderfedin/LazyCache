using System;
// ReSharper disable UnusedMember.Global

namespace CacheDatabaseQueriesApiSample;

/// <summary>
/// Simulates loading a record from a table, but really just gets the current datatime from the database
/// </summary>
public sealed class DbTimeEntity
{
    public DbTimeEntity(DateTime now)
    {
        TimeNowInTheDatabase = now;
    }

    public DbTimeEntity()
    {
    }

    public int Id { get; set; }

    public DateTime TimeNowInTheDatabase { get; set; }
}