using Microsoft.Extensions.Logging;
using WikiClientLibrary.Client;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Samples.ConsoleTestApplication1;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            HelloWikiWorld().Wait();
            //HelloWikiGenerators().Wait();
            //HelloRecentChanges().Wait();
            //InteractivePatrol().Wait();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }

    static async Task HelloWikiWorld()
    {
        var loggerFactory = LoggerFactory.Create(c => c
            .SetMinimumLevel(LogLevel.Information)
            .AddSimpleConsole(o => o.IncludeScopes = true)
        );
        // Create a MediaWiki API client.
        var wikiClient = new WikiClient
        {
            // UA of Client Application. The UA of WikiClientLibrary will
            // be append to the end of this when sending requests.
            ClientUserAgent = "ConsoleTestApplication1/1.0",
            Logger = loggerFactory.CreateLogger<WikiClient>(),
        };
        // Create a MediaWiki Site instance with the URL of API endpoint.
        var site = new WikiSite(wikiClient, "https://test2.wikipedia.org/w/api.php")
        {
            Logger = loggerFactory.CreateLogger<WikiSite>(),
        };
        // Waits for the WikiSite to initialize
        await site.Initialization;
        // Access site information via Site.SiteInfo
        Console.WriteLine("API version: {0}", site.SiteInfo.Generator);
        // Access user information via Site.UserInfo
        Console.WriteLine("Hello, {0}!", site.AccountInfo.Name);
        // Site login
        Console.WriteLine("We will edit [[Project:Sandbox]].");
        if (Confirm($"Do you want to login into {site.SiteInfo.SiteName}?"))
        {
            LOGIN_RETRY:
            try
            {
                await site.LoginAsync(Input("User name"), Input("Password"));
            }
            catch (OperationFailedException ex)
            {
                Console.WriteLine(ex.ErrorMessage);
                goto LOGIN_RETRY;
            }
            Console.WriteLine("You have successfully logged in as {0}.", site.AccountInfo.Name);
            Console.WriteLine("You're in the following groups: {0}.", string.Join(",", site.AccountInfo.Groups));
        }
        // Find out more members in Site class, such as
        //  page.Namespaces
        //  page.InterwikiMap

        // Page Operations
        // Fetch information and content
        var page = new WikiPage(site, site.SiteInfo.MainPage);
        Console.WriteLine("Retrieving {0}…", page);
        await page.RefreshAsync(PageQueryOptions.FetchContent);

        Console.WriteLine("Last touched at {0}.", page.LastTouched);
        if (page.LastRevision == null)
            Console.WriteLine("No last revision available: {0}", page.LastRevisionId);
        else
            Console.WriteLine("Last revision {0} by {1} at {2}.", page.LastRevisionId, page.LastRevision.UserName, page.LastRevision.TimeStamp);
        Console.WriteLine("Content length: {0} bytes ----------", page.ContentLength);
        Console.WriteLine(page.Content);
        // Purge the page
        if (await page.PurgeAsync())
            Console.WriteLine("  The page has been purged successfully.");
        // Edit the page
        page = new WikiPage(site, "Project:Sandbox");
        await page.RefreshAsync(PageQueryOptions.FetchContent);
        if (!page.Exists) Console.WriteLine("Warning: The page {0} doesn't exist.", page);
        await page.EditAsync(new WikiPageEditOptions
        {
            Content = page.Content + "\n\n'''Hello''' ''world''!",
            Summary = "Test edit from WikiClientLibrary.",
        });
        Console.WriteLine("{0} has been saved. RevisionId = {1}.", page, page.LastRevisionId);
        // Find out more operations in Page class, such as
        //  page.MoveAsync()
        //  page.DeleteAsync()
        // Logout
        await site.LogoutAsync();
        Console.WriteLine("You have successfully logged out.");
    }

    static async Task HelloWikiGenerators()
    {
        // Create a MediaWiki API client.
        var wikiClient = new WikiClient();
        // Create a MediaWiki Site instance.
        var site = new WikiSite(wikiClient, "https://en.wikipedia.org/w/api.php");
        await site.Initialization;
        // List all pages starting from item "Wiki", without redirect pages.
        var allpages = new AllPagesGenerator(site)
        {
            StartTitle = "Wiki",
            RedirectsFilter = PropertyFilterOption.WithoutProperty,
        };
        // Take the first 1000 results
        var pages = await allpages.EnumPagesAsync().Take(1000).ToListAsync();
        foreach (var p in pages)
            Console.WriteLine("{0, -30} {1, 8}B {2}", p, p.ContentLength, p.LastTouched);

        // List the first 10 subcategories in Category:Cats
        Console.WriteLine();
        Console.WriteLine("Cats");
        var catmembers = new CategoryMembersGenerator(site, "Category:Cats")
        {
            MemberTypes = CategoryMemberTypes.Subcategory,
        };
        pages = await catmembers.EnumPagesAsync().Take(10).ToListAsync();
        foreach (var p in pages)
            Console.WriteLine("{0, -30} {1, 8}B {2}", p, p.ContentLength, p.LastTouched);
    }

    static async Task HelloRecentChanges()
    {
        // Create a MediaWiki API client.
        var wikiClient = new WikiClient();
        // Create a MediaWiki Site instance.
        var site = new WikiSite(wikiClient, "https://en.wikipedia.org/w/api.php");
        await site.Initialization;
        var rcg = new RecentChangesGenerator(site)
        {
            TypeFilters = RecentChangesFilterTypes.Create,
            PaginationSize = 50, // We already know we're not going to fetch results as many as 500 or 5000
            // so this will help.
        };
        // List the 10 latest new pages
        var pages = await rcg.EnumPagesAsync().Take(10).ToListAsync();
        Console.WriteLine("New pages");
        foreach (var p in pages)
            Console.WriteLine("{0, -30} {1, 8}B {2}", p, p.ContentLength, p.LastTouched);
        // List the 10 latest recent changes
        rcg.TypeFilters = RecentChangesFilterTypes.All;
        var rcs = await rcg.EnumItemsAsync().Take(10).ToListAsync();
        Console.WriteLine();
        Console.WriteLine("Recent changes");
        foreach (var rc in rcs)
            Console.WriteLine(rc);
    }

    static async Task InteractivePatrol()
    {
        // Patrol the last unpatrolled change.
        // Usually a user should have the patrol right to perform such operation.

        // Create a MediaWiki API client.
        var wikiClient = new WikiClient();
        // Create a MediaWiki Site instance.
        var site = new WikiSite(wikiClient, Input("Wiki site API URL"));
        await site.Initialization;
        await site.LoginAsync(Input("User name"), Input("Password"));
        var rcg = new RecentChangesGenerator(site)
        {
            TypeFilters = RecentChangesFilterTypes.Create,
            PaginationSize = 5,
            PatrolledFilter = PropertyFilterOption.WithoutProperty,
        };
        // List the first unpatrolled result.
        var rc = await rcg.EnumItemsAsync().FirstOrDefaultAsync();
        if (rc == null)
        {
            Console.WriteLine("Nothing to patrol.");
            return;
        }
        Console.WriteLine("Unpatrolled:");
        Console.WriteLine(rc);
        // Show the involved revisions.
        if (rc.OldRevisionId > 0 && rc.RevisionId > 0)
        {
            var rev = await Revision.FetchRevisionsAsync(site, rc.OldRevisionId, rc.RevisionId).ToListAsync();
            // Maybe we'll use some 3rd party diff lib
            Console.WriteLine("Before, RevId={0}, {1}", rev[0]!.Id, rev[0]!.TimeStamp);
            Console.WriteLine(rev[0]!.Content);
            Console.WriteLine("After, RevId={0}, {1}", rev[1]!.Id, rev[1]!.TimeStamp);
            Console.WriteLine(rev[1]!.Content);
        }
        else if (rc.RevisionId > 0)
        {
            var rev = await Revision.FetchRevisionAsync(site, rc.RevisionId);
            Console.WriteLine("RevId={0}, {1}", rev.Id, rev.TimeStamp);
            Console.WriteLine(rev.Content);
        }
        if (Confirm("Mark as patrolled?"))
        {
            await rc.PatrolAsync();
            Console.WriteLine("The change {0} has been marked as patrolled.", (object) rc.Title ?? rc.Id);
        }
    }

#region Console Utilities

    static string Input(string prompt)
    {
        Console.Write(prompt);
        Console.Write("> ");
        return Console.ReadLine() ?? throw new InvalidOperationException("EOF reached.");
    }

    static bool Confirm(string prompt)
    {
        Console.Write(prompt);
        Console.Write("[Y/N]> ");
        while (true)
        {
            var input = Console.ReadLine()!.ToUpperInvariant();
            if (input == "Y") return true;
            if (input == "N") return false;
            Console.Write("> ");
        }
    }

#endregion

}