#if !BCL_FEATURE_REQUIRED_MEMBER

namespace System.Runtime.CompilerServices
{
    internal sealed class RequiredMemberAttribute : Attribute
    {

    }

    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {

        public CompilerFeatureRequiredAttribute(string name) { }

    }
}

namespace System.Diagnostics.CodeAnalysis
{
    internal sealed class SetsRequiredMembersAttribute : Attribute
    {

    }
}

#endif
