﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using WikiClientLibrary.Cargo.Linq;
using WikiClientLibrary.Cargo.Schema;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

var loggerFactory = LoggerFactory.Create(c => c
    // Use LogLevel.Debug to see the exact query parameters.
    .SetMinimumLevel(LogLevel.Debug)
    .AddSimpleConsole(c1 =>
    {
        c1.ColorBehavior = LoggerColorBehavior.Enabled;
        c1.IncludeScopes = true;
    })
);
using var client = new WikiClient
{
    ClientUserAgent = "WikiClientLibrary.Samples.CargoLinq/1.0",
    Logger = loggerFactory.CreateLogger<WikiClient>()
};
var site = new WikiSite(client, "https://lol.gamepedia.com/api.php");
site.Logger = loggerFactory.CreateLogger<WikiSite>();
await site.Initialization;

var context = new LolCargoQueryContext(site);
// Query starts here.
var query = context.RosterChanges
    .Where(x => x.DateSort > new DateTime(2020, 12, 11))
    .OrderBy(x => x.DateSort)
    .Take(100)
    // Cargo query expression ends here.
    .AsAsyncEnumerable()
    // Do some local conversion for better display on console.
    .Select(x => new
    {
        x.Page,
        x.DateSort,
        x.Player,
        x.Direction,
        Roles = string.Join(';', x.Roles),
        Tags = string.Join(';', x.Tags),
        Tournaments = string.Join(';', x.Tournaments)
    });
var counter = 0;
await foreach (var item in query)
{
    counter++;
    Console.WriteLine("{0,4}: {1}", counter, item);
}

class LolCargoQueryContext : CargoQueryContext
{

    /// <inheritdoc />
    public LolCargoQueryContext(WikiSite wikiSite) : base(wikiSite)
    {
        PaginationSize = 30;
    }

    public ICargoRecordSet<RosterChanges> RosterChanges => Table<RosterChanges>();

}

/// <summary>https://lol.gamepedia.com/Special:CargoTables/RosterChanges</summary>
class RosterChanges
{

    [Column(CargoSpecialColumnNames.PageName)]
    public string Page { get; set; }

    [Column("Date_Sort")]
    public DateTime DateSort { get; set; }

    public string Player { get; set; }

    public string Direction { get; set; }

    public ICollection<string> Roles { get; set; }

    public ICollection<string> Tags { get; set; }

    public ICollection<string> Tournaments { get; set; }

}