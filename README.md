# Wiki Client Library

A hand-crafted asynchronous [MediaWiki](https://www.mediawiki.org/) API client library for wiki sites (including [Wikipedia](https://www.wikipedia.org/) and its sister projects, as well as [Wikia](https://cxuesong.github.io/WikiClientLibrary/html/community.wikia.com)). This library aims for both human users and bots.

The packages `CXuesong.MW.WikiClientLibrary.*` are now available on NuGet. You may install the main package using the following command

```powershell
#  Package Management Console
Install-Package CXuesong.MW.WikiClientLibrary -Pre
#  .NET CLI
dotnet add package CXuesong.MW.WikiClientLibrary --version 0.6.0-intX1
```

The other related packages include

*   [CXuesong.MW.WikiClientLibrary.Flow](https://www.nuget.org/packages/CXuesong.MW.WikiClientLibrary.Flow)
*   [CXuesong.MW.WikiClientLibrary.Wikia](https://www.nuget.org/packages/CXuesong.MW.WikiClientLibrary.Wikia)
*   [CXuesong.MW.WikiClientLibrary.Wikibase](https://www.nuget.org/packages/CXuesong.MW.WikiClientLibrary.Wikibase)

If you bump into bugs, have any suggestions or feature requests, feel free to open an issue. Thank you.

## Overview

This portable & asynchronous MediaWiki API client provides an easy and asynchronous access to commonly-used MediaWiki API. Developed in Visual Studio 2017, the portable library targets at .NET Standard 1.1 (See [Supported Platforms](https://docs.microsoft.com/en-us/dotnet/standard/net-standard#net-platforms-support)). It has the following features

*   Queries for and edits to pages, categories, and files; page information inspection; file uploading.
*   Login/logout via simple asynchronous functions.

    *   Client code has access to `CookieContainer`, and therefore has chance to persist it.
*   Tokens are encapsulated in the library functions, so that client won't bother to retrieve them over and over again.
*   Query continuations are encapsulated by `IAsyncEnumerable`, which will ease the pain when using page generators.
*   Other miscellaneous MediaWiki API, such as

    *   OpenSearch
    *   Page parsing
    *   Patrol
*   StructuredDiscussions (aka. Flow) support
*   Basic Wikibase (Wikidata's backend) support
*   Basic Wikia API (Nirvana, Wikia AJAX, and Wikia REST-ful API v1) support


## See also

*   [Repository Wiki](https://github.com/CXuesong/WikiClientLibrary/wiki)
*   [Library References](https://cxuesong.github.io/WikiClientLibrary) (latest prerelease)
*   [Releases](https://github.com/CXuesong/WikiClientLibrary/releases)