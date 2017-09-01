using Xunit;

// This is a work-around for #11.
// https://github.com/CXuesong/WikiClientLibrary/issues/11
[assembly:CollectionBehavior(DisableTestParallelization = true)]
