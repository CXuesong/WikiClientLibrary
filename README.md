# Wiki Client Library

[![CI](https://github.com/CXuesong/WikiClientLibrary/actions/workflows/ci.yml/badge.svg)](https://github.com/CXuesong/WikiClientLibrary/actions/workflows/ci.yml) [![Gitter](https://badges.gitter.im/CXuesong/WikiClientLibrary.svg?style=flat-square)](https://gitter.im/CXuesong/WikiClientLibrary?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)

A hand-crafted asynchronous [MediaWiki](https://www.mediawiki.org/) API client library for wiki sites (including [Wikipedia](https://www.wikipedia.org/) and its sister projects, as well as [FANDOM](<https://community.fandom.com/wiki/Community_Central>) and [Wikia.org](<https://www.wikia.org/>). The library targets at .NET 6.0 & 8.0 (See [Supported Platforms](https://docs.microsoft.com/en-us/dotnet/standard/net-standard#net-platforms-support)), and focuses on the API compatibility with MediaWiki 1.19, as well as the state-of-art APIs of MediaWiki (i.e. 1.34-wmf, as in 2019-05). Other versions in between are hopefully also compatible.

> For migrated FANDOM sites on MW 1.33+ (See [UCP migration post](https://community.fandom.com/wiki/User_blog:MisterWoodhouse/The_first_migrations_to_the_Unified_Community_Platform)), you may need to use Bot Password in order to login successfully.

> If you are on prior versions of .NET Core or .NET Framework, you may use prior version of the WCL packages.
> * WCL v0.7.x libraries target .NET Standard 1.1 and has dependency on `Newtonsoft.Json`
> * WCL v0.8.x libraries target .NET Standard 2.1, .NET 6.0 & .NET 8.0, and has dependency on `Newtonsoft.Json`

The packages `CXuesong.MW.WikiClientLibrary.*` are now available on NuGet. E.g. you may install the main package using the following command

```powershell
#  Package Management Console
Install-Package CXuesong.MW.WikiClientLibrary
#  .NET CLI
dotnet add package CXuesong.MW.WikiClientLibrary
```

| Package                                                      | Status                                                       |
| ------------------------------------------------------------ | ------------------------------------------------------------ |
| [CXuesong.MW.WikiClientLibrary](https://www.nuget.org/packages/CXuesong.MW.WikiClientLibrary) | ![NuGet version (CXuesong.MW.WikiClientLibrary)](https://img.shields.io/nuget/vpre/CXuesong.MW.WikiClientLibrary.svg?style=flat-square) ![NuGet version (CXuesong.MW.WikiClientLibrary)](https://img.shields.io/nuget/dt/CXuesong.MW.WikiClientLibrary.svg?style=flat-square) |
| [CXuesong.MW.WikiClientLibrary.Cargo](https://www.nuget.org/packages/CXuesong.MW.WikiClientLibrary.Cargo) | ![NuGet version (CXuesong.MW.WikiClientLibrary.Cargo)](https://img.shields.io/nuget/vpre/CXuesong.MW.WikiClientLibrary.Cargo.svg?style=flat-square)![NuGet version (CXuesong.MW.WikiClientLibrary)](https://img.shields.io/nuget/dt/CXuesong.MW.WikiClientLibrary.Cargo.svg?style=flat-square) |
| [CXuesong.MW.WikiClientLibrary.Flow](https://www.nuget.org/packages/CXuesong.MW.WikiClientLibrary.Flow) | ![NuGet version (CXuesong.MW.WikiClientLibrary.Flow)](https://img.shields.io/nuget/vpre/CXuesong.MW.WikiClientLibrary.Flow.svg?style=flat-square) ![NuGet version (CXuesong.MW.WikiClientLibrary.Flow)](https://img.shields.io/nuget/dt/CXuesong.MW.WikiClientLibrary.Flow.svg?style=flat-square) |
| [CXuesong.MW.WikiClientLibrary.Wikia](https://www.nuget.org/packages/CXuesong.MW.WikiClientLibrary.Wikia) | ![NuGet version (CXuesong.MW.WikiClientLibrary.Wikia)](https://img.shields.io/nuget/vpre/CXuesong.MW.WikiClientLibrary.Wikia.svg?style=flat-square) ![NuGet version (CXuesong.MW.WikiClientLibrary.Wikia)](https://img.shields.io/nuget/dt/CXuesong.MW.WikiClientLibrary.Wikia.svg?style=flat-square) |
| [CXuesong.MW.WikiClientLibrary.Wikibase](https://www.nuget.org/packages/CXuesong.MW.WikiClientLibrary.Wikibase) | ![NuGet version (CXuesong.MW.WikiClientLibrary.Wikibase)](https://img.shields.io/nuget/vpre/CXuesong.MW.WikiClientLibrary.Wikibase.svg?style=flat-square) ![NuGet version (CXuesong.MW.WikiClientLibrary.Wikibase)](https://img.shields.io/nuget/dt/CXuesong.MW.WikiClientLibrary.Wikibase.svg?style=flat-square) |

If you bump into bugs, have any suggestions or feature requests, feel free to open an issue. Any contributions on documentations (code annotations & repository wiki) are also welcomed. Thank you.

## See also

* [Repository Wiki](https://github.com/CXuesong/WikiClientLibrary/wiki) (getting started & conceptual documentations)
* [Library Documentation](https://cxuesong.github.io/WikiClientLibrary) (for latest pre-release)
* [FuGet Gallery](https://www.fuget.org/packages/CXuesong.MW.WikiClientLibrary) (inspect & diff public API)
* [Releases](https://github.com/CXuesong/WikiClientLibrary/releases)

## Overview

Developed in Visual Studio 2019, this portable & asynchronous MediaWiki API client provides an easy and asynchronous access to commonly-used MediaWiki API. The library has the following features

*   Queries for and edits to pages, categories, and files; page information inspection; file uploading.
*   Login/logout via simple asynchronous functions.

    *   Client code has access to `CookieContainer`, and therefore has chance to persist it.
*   Tokens are encapsulated in the library functions, so that client won't bother to retrieve them over and over again.
*   Query continuations are encapsulated by `IAsyncEnumerable`, which will ease the pain when using page generators.
*   Other miscellaneous MediaWiki API, such as

    *   OpenSearch
    *   Page parsing
    *   Patrol
*   Scribunto Lua console and server-side module execution support
*   StructuredDiscussions (aka. Flow) support
*   Basic Wikibase (Wikidata's back-end) API support; the library provides facility to consume Wikibase JSON dump
*   Basic FANDOM / Wikia API (Nirvana, Wikia AJAX, and Wikia REST-ful API v1) support
