using Xunit;

// This is a work-around for #11.
// https://github.com/CXuesong/WikiClientLibrary/issues/11
// We are using Bot Password on CI, which may naturally evade the issue.
#if ENV_CI_BUILD
[assembly:CollectionBehavior(DisableTestParallelization = true)]
#endif
