A .NET Portable & asynchronous MediaWiki API client library for wiki sites. This library aims for human users, as well as bots.

The repository is still under constructions. The roadmap can be found in the [repository wiki](https://github.com/CXuesong/WikiClientLibrary/wiki).

Before running the test cases, please take a look at the [last section](#setting-up-test-cases).

[TOC]

## Overview

This portable & asynchronous MediaWiki API client provides an easy and asynchronous access to commonly-used MediaWiki API. Developed in Visual Studio 2015, The portable library targets at .NET Framework 4.5, ASP.NET Core 1.0, Xamarin.iOS, and Xamarin.Android. It has the following features

*   Queries and edits for pages, including standard pages, category pages, and file pages.

    *   Queries for category statistical info and its members.
    *   Queries for basic file info, and file uploading.

*   Login/logout via simple asynchronous functions, as shown in the demo below.

    *   Client code has access to `CookieContainer`, and have chance to persist it.

*   Tokens are hidden in the library functions, so that client won't bother to retrieve them over and over again.

*   Query continuations are hidden by `IAsyncEnumerable`, which will ease the pain when using page generators.

*   Other miscellaneous MediaWiki API, such as

    *   OpenSearch
    *   Page parsing
    *   Patrol

    ​

## A Brief Demo

You can find a demo in `ConsoleTestApplication1`. It's also suggested that you take a look at `UnitTestProject1` to find out what can be done with the library.

```c#
static async Task HelloWikiWorld()
{
    // Create a MediaWiki API client.
    var wikiClient = new WikiClient
    {
        // UA of Client Application. The UA of WikiClientLibrary will
        // be append to the end of this when sending requests.
        ClientUserAgent = "ConsoleTestApplication1/1.0",
    };
    // Create a MediaWiki Site instance with the URL of API endpoint.
    var site = await Site.CreateAsync(wikiClient, "https://test2.wikipedia.org/w/api.php");
    // Access site information via Site.SiteInfo
    Console.WriteLine("API version: {0}", site.SiteInfo.Generator);
    // Access user information via Site.UserInfo
    Console.WriteLine("Hello, {0}!", site.UserInfo.Name);
    Console.WriteLine("You're in the following groups: {0}.", string.Join(",", site.UserInfo.Groups));
    // Site login
    if (Confirm($"Do you want to login into {site.SiteInfo.SiteName}?"))
    {
        await site.LoginAsync(Input("Username"), Input("Password"));
        Console.WriteLine("You have successfully logged in as {0}.", site.UserInfo.Name);
    }
    // Find out more members in Site class, such as
    //  site.Namespaces
    //  site.InterwikiMap

    // Page Operations
    // Fetch information and content
    var page = new Page(site, site.SiteInfo.MainPage);
    Console.WriteLine("Retriving {0}...", page);
    await page.RefreshAsync(PageQueryOptions.FetchContent);

    Console.WriteLine("Last touched at {0}.", page.LastTouched);
    Console.WriteLine("Last revision {0} by {1} at {2}.", page.LastRevisionId,
        page.LastRevision.UserName, page.LastRevision.TimeStamp);
    Console.WriteLine("Content length: {0} bytes ----------", page.ContentLength);
    Console.WriteLine(page.Content);
    // Purge the page
    if (await page.PurgeAsync())
        Console.WriteLine("  The page has been purged successfully.");
    // Edit the page
    page = new Page(site, "Project:Sandbox");
    await page.RefreshAsync(PageQueryOptions.FetchContent);
    if (!page.Exists) Console.WriteLine("Warning: The page {0} doesn't exist.", page);
    page.Content += "\n\n'''Hello''' ''world''!";
    await page.UpdateContentAsync("Test edit from WikiClientLibrary.");
    Console.WriteLine("{0} has been saved. RevisionId = {1}.", page, page.LastRevisionId);
    // Find out more operations in Page class, such as
    //  page.MoveAsync()
    //  page.DeleteAsync()
    // Logout
    await site.LogoutAsync();
    Console.WriteLine("You have successfully logged out.");
}
```

Here's the output

```
API version: MediaWiki 1.28.0-wmf.16
Hello, 115.154.***.***!
You're in the following groups: *.
Do you want to login into Wikipedia? [Y/N]>y
Username >XuesongBot
Password >******
You have successfully logged in as XuesongBot.
Retriving Main Page...
Last touched at 2016/8/27 AM 6:10:56.
Last revision 285903 by Luke081515Bot at 2016/5/27 上午 1:54:34.
Content length: 3527 bytes ----------
Welcome to '''test2.wikipedia.org'''! This wiki is currently running a test release of the {{CURRENTVERSION}} version of MediaWiki.

== What is the purpose of test2? ==
test2 is primarily used to trial and debug global and cross-wiki features in conjunction with test.wikipedia.org and test.wikidata.org.

The primary wiki that is used for testing new features and code in MediaWiki is [https://test.wikipedia.org test.wikipedia.org]. Although you're more than welcome to test things out on this wiki as well, you may wish to concentrate your testing efforts on [https://test.wikipedia.org test.wikipedia.org] wiki instead to help keep everything in one place.

[The text has been trucated.]

  The page has been purged successfully.
Wikipedia:Sandbox has been saved. RevisionId = 296329.
You have successfully logged out.
```



## Bulk fetching and generators

You can fetch multiple pages at one time with `PageExtensions.RefreshAsync` extension method. However, it's often the case that you're actually fetching pages using [generators](https://www.mediawiki.org/wiki/API:Generator), so that's what we're discussing in the section.

You can query a list of pages, fetching their information (with or without content) with lists (i.e. generators), such as [`allpages`](https://www.mediawiki.org/wiki/API:Allpages), [`allcategories`](https://www.mediawiki.org/wiki/API:Allcategories), [`querypage`](https://www.mediawiki.org/wiki/API:Querypage), and [`recentchanges`](https://www.mediawiki.org/wiki/API:Recentchanges). Such generators are implemented in `WikiClientLibrary.Generators` namespace. Though there're still a lot of types of generators yet to be supported, the routine for implementing a `Generator` class is quite the same. Up till now, the following generators have been implemented

*   allpages
*   allcategories
*   categorymembers
*   querypage
*   recentchanges

Here's a demo that can be found in `ConsoleTestApplication1`. Note here we used `IAsyncEnumerable` form Ix-async (System.Interactive.Async) package to enable a LINQ-like query expression. To get a synchronous `IEnumerable`, consider using `AsyncEnumerable.ToEnumerable`. Also keep in mind that the sequence is so long that we often use `Take()` to prevent getting to many results. (You may also want to take a look at `PageGenerator.PagingSize`.) And of course do not attempt to inverse the sequence, unless you know what you're doing. Instead, you can take a look at properties of Generators, which may include such sort options.

```c#
static async Task HelloWikiGenerators()
{
    // Create a MediaWiki API client.
    var wikiClient = new WikiClient();
    // Create a MediaWiki Site instance.
    var site = await Site.CreateAsync(wikiClient, "https://en.wikipedia.org/w/api.php");
    // List all pages starting from item "Wiki", without redirect pages.
    var allpages = new AllPagesGenerator(site)
    {
        StartTitle = "Wiki",
        RedirectsFilter = PropertyFilterOption.WithoutProperty
    };
    // Take the first 1000 results
    var pages = await allpages.EnumPagesAsync().Take(1000).ToList();
    foreach (var p in pages)
        Console.WriteLine("{0, -30} {1, 8}B {2}", p, p.ContentLength, p.LastTouched);
            
    // List the first 10 subcategories in Category:Cats
    Console.WriteLine();
    Console.WriteLine("Cats");
    var catmembers = new CategoryMembersGenerator(site, "Category:Cats")
    {
        MemberTypes = CategoryMemberTypes.Subcategory
    };
    pages = await catmembers.EnumPagesAsync().Take(10).ToList();
    foreach (var p in pages)
        Console.WriteLine("{0, -30} {1, 8}B {2}", p, p.ContentLength, p.LastTouched);
}
```

## Recent changes and patrol

You can list pages using `RecentChangesGenerator.EnumPagesAsync`, just like other generators, while this generator has another powerful method `RecentChangesGenerator.EnumRecentChangesAsync`, which can generate a sequence of `RecentChangesEntry`, containing the detailed information of each recent change.

```c#
static async Task HelloRecentChanges()
{
    // Create a MediaWiki API client.
    var wikiClient = new WikiClient();
    // Create a MediaWiki Site instance.
    var site = await Site.CreateAsync(wikiClient, "https://en.wikipedia.org/w/api.php");
    var rcg = new RecentChangesGenerator(site)
    {
        TypeFilters = RecentChangesFilterTypes.Create,
        PagingSize = 50, // We already know we're not going to fetch results as many as 500 or 5000
        // so this will help.
    };
    // List the 10 latest new pages
    var pages = await rcg.EnumPagesAsync().Take(10).ToList();
    Console.WriteLine("New pages");
    foreach (var p in pages)
        Console.WriteLine("{0, -30} {1, 8}B {2}", p, p.ContentLength, p.LastTouched);
    // List the 10 latest recent changes
    rcg.TypeFilters = RecentChangesFilterTypes.All;
    var rcs = await rcg.EnumRecentChangesAsync().Take(10).ToList();
    Console.WriteLine();
    Console.WriteLine("Recent changes");
    foreach (var rc in rcs)
        Console.WriteLine(rc);
}
```

The code produces the following result

```
New pages
Bill Eigel                           43B 2016/8/27 AM 6:38:46
Cool woods                         3971B 2016/8/27 AM 6:46:52
Indian Institute Of Technical Computer Application       56B 2016/8/27 AM 6:59:20
Leslie Bieh Yhan                    371B 2016/8/27 AM 6:58:02
Maranathirkku Pinbu Helan           157B 2016/8/27 AM 6:46:17
Mitchell Bolewski                    27B 2016/8/27 AM 6:44:45
Palazzo Fani Mignanelli, Siena     1537B 2016/8/27 AM 6:39:52
Punjab Highway Department          1940B 2016/8/27 AM 6:58:32
Shiny waves                         968B 2016/8/27 AM 6:53:50
Thailand national football team results (2000-09)    28321B 2016/8/27 AM 6:58:00

Recent changes
855757198,2016/8/27 AM 6:59:39,Edit,None,Melbourne Football Club,update coach
855757197,2016/8/27 AM 6:59:39,Edit,None,The Ocean (Mike Perry song),Undid revision 734440122 by [[Special:Contributions/82.113.183.202|82.113.183.202]] ([[User talk:82.113.183.202|talk]]) Considering Shy Martin doesn't have an article, this gives a tidbit of info about her and shouldn't be removed.
855757193,2016/8/27 AM 6:59:35,Edit,None,User:Lesbyan,db-g3
855757192,2016/8/27 AM 6:59:35,Edit,None,Kashmiri phonology,[[WP:CHECKWIKI]] error fix. Broken bracket problem. Do [[Wikipedia:GENFIXES|general fixes]] and cleanup if needed. - using [[Project:AWB|AWB]] (12082)
855757191,2016/8/27 AM 6:59:34,Edit,Annonymous,ConBravo!,
855757188,2016/8/27 AM 6:59:32,Edit,None,User:Moxy,
855757187,2016/8/27 AM 6:59:31,Edit,Minor, Bot,Society for the Propagation of the Faith,Dating maintenance tags: {{Cn}}
855757186,2016/8/27 AM 6:59:31,Edit,None,1530 in India,
855757185,2016/8/27 AM 6:59:30,Edit,None,User talk:101.98.246.169,Warning [[Special:Contributions/101.98.246.169|101.98.246.169]] - #1
855757184,2016/8/27 AM 6:59:30,Edit,Minor,Bay of Plenty Region,Reverting possible vandalism by [[Special:Contribs/101.98.246.169|101.98.246.169]] to version by Villianifm. [[WP:CBFP|Report False Positive?]] Thanks, [[WP:CBNG|ClueBot NG]]. (2741830) (Bot)
```

Once you've got an instance of `RecentChangesEntry`, you can patrol it, as long as you have the `patrol` or `autopatrol` right.

```c#
static async Task InteractivePatrol()
{
    // Patrol the last unpatrolled change.
    // Ususally a user should have the patrol right to perform such operation.

    // Create a MediaWiki API client.
    var wikiClient = new WikiClient();
    // Create a MediaWiki Site instance.
    var site = await Site.CreateAsync(wikiClient, Input("Wiki site URL"));
    await site.LoginAsync(Input("Username"), Input("Password"));
    var rcg = new RecentChangesGenerator(site)
    {
        TypeFilters = RecentChangesFilterTypes.Create,
        PagingSize = 5,
        PatrolledFilter = PropertyFilterOption.WithoutProperty
    };
    // List the first unpatrolled result.
    var rc = await rcg.EnumRecentChangesAsync().FirstOrDefault();
    if (rc == null)
    {
        Console.WriteLine("Nothing to patrol.");
        return;
    }
    Console.WriteLine("Unpatrolled:");
    Console.WriteLine(rc);
    // TODO show diff. But for now we do not even have an
    // approach to get a revision directly.
    if (Confirm("Mark as patrolled?"))
    {
        await rc.PatrolAsync();
        Console.WriteLine("The change {0} has been marked as patrolled.", (object) rc.Title ?? rc.Id);
    }
}
```

## Miscellaneous

You can get/set/persist cookies with `WikiClient.CookieContainer` property.

The following APIs have also been taken or partially taken into the library

*   `opensearch`: See `Site.OpenSearch`.
*   `parse`: See `Site.ParsePage` . There's also a demo in `WpfTestApplication1`.

## Behavior

The following behavior has been implemented

*   Request timeout and retry (See `WikiClient`)
*   Throttle before edit/move/delete
*   Logging (See `ILogger`, `WikiClient.Logger`, and `Site.Logger`.)

The following behavior has yet to be implemented

*   Detect {{inuse}}
*   Detect {{bots}}, {{nobots}}

### See also

*   [API:Etiquette](https://www.mediawiki.org/wiki/API:Etiquette)


*   [API:Faq](https://www.mediawiki.org/wiki/API:FAQ)

## Setting up test cases

Before you can run most of the test cases, please create a new file named `credentials.cs` and place it under `\UnitTestProject1\_private` folder. The content should be like this

```c#
using System;
using WikiClientLibrary;

namespace UnitTestProject1
{
    partial class CredentialManager
    {
        static partial void LoginCore(Site site)
        {
            var url = site.ApiEndpoint;
          // We'll make changes to test2.wikipedia.org
            if (url.Contains("wikipedia.org"))
                Login(site, "Anne", "password" );
          // We'll make changes to MediaWiki 119 test Wiki
            else if (url.Contains("wikia.com"))
                Login(site, "Bob", "password");
          // Add other login routines if you need to.
            else if (url.contains("domain.com"))
                Login(site, "Calla", "password");
            else
                throw new NotSupportedException();
        }

        static partial void Initialize()
        {
          // A place to perform page moving and deleting
          // You should have the bot or sysop right there
            DirtyTestsEntryPointUrl = "http://testwiki.domain.com/api.php";
        }
    }
}
```

You need to put valid user names and passwords into this file. As is set in `.gitignore`, this file WILL NOT be included in the repository.