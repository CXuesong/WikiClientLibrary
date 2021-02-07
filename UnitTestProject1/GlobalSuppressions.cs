﻿// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

// https://docs.microsoft.com/en-us/visualstudio/code-quality/in-source-suppression-overview?view=vs-2019
using System.Diagnostics.CodeAnalysis;

[assembly:
    SuppressMessage("Style", "VSTHRD200:Use Async suffix for async methods", Justification = "test methods",
    Scope = "namespaceanddescendants", Target = "~N:WikiClientLibrary.Tests.UnitTestProject1.Tests")
]
