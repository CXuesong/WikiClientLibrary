using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace WikiClientLibrary.Tests.UnitTestProject1
{

    public enum CISkippedReason
    {
        Unknown = 0,
        Unstable
    }

    /// <summary>
    /// Mark the unit test with <c>CI=Skipped</c> trait.
    /// This will cause the test not being executed in CI environment.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    [TraitDiscoverer("WikiClientLibrary.Tests.UnitTestProject1." + nameof(CISkippedTraitDiscoverer), "UnitTestProject1")]
    public class CISkippedAttribute : Attribute, ITraitAttribute
    {

        public CISkippedReason Reason { get; set; }

    }

    public class CISkippedTraitDiscoverer : ITraitDiscoverer
    {
        private static readonly KeyValuePair<string, string>[] traits = new[] { new KeyValuePair<string, string>("CI", "Skipped") };

        /// <inheritdoc />
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            return traits;
        }
    }

}
