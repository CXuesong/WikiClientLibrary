A .NET Portable & asynchronous client library for MediaWiki sites. This library aims for human users, as well as bots.

The repository is still under preliminary constructions.

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
            var url = site.SiteInfo.ServerUrl;
            if (url.Contains("wikipedia.org"))
                Login(site, "Anne", "password" );
          // Though we do not login into wikia, for now.
            else if (url.Contains("wikia.com"))
                Login(site, "Bob", "password");
          // Add other login routine if you need to.
            else
                throw new NotSupportedException();
        }
    }
}
```

You need to put a valid username and password into this file. As is set in `.gitignore`, this file WILL NOT be included in the repository.