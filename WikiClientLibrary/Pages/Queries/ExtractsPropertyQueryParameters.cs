using System;
using System.Collections.Generic;
using System.Text;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages.Queries
{
    /// <summary>
    /// Returns plain-text or limited HTML extracts of the given pages.
    /// <c>action=query&amp;prop=extracts</c>
    /// (<a href="https://www.mediawiki.org/wiki/Extension:TextExtracts#API">mw:Extension:TextExtracts#API</a>)
    /// </summary>
    public class ExtractsPropertyQueryParameters : WikiPagePropertyQueryParameters
    {

        /// <summary>
        /// How many characters to return. Actual text returned might be slightly longer.
        /// </summary>
        /// <value>The allowed maximum number of characters to return, or 0 for no such limitation.</value>
        /// <remarks>
        /// The effective value must be between 1 and 1,200.
        /// Either this property or <see cref="MaxSentences"/> should be 0.
        /// </remarks>
        public int MaxCharacters { get; set; }

        /// <summary>
        /// How many sentences to return.
        /// </summary>
        /// <value>The allowed maximum number of sentences to return, or 0 for no such limitation.</value>
        /// <remarks>
        /// The effective value must be between 1 and 10.
        /// Either this property or <see cref="MaxCharacters"/> should be 0.
        /// </remarks>
        public int MaxSentences { get; set; }

        /// <summary>
        /// Return only content before the first section.
        /// </summary>
        public bool IntroductionOnly { get; set; }

        /// <summary>
        /// Return extracts as plain text instead of limited HTML.
        /// </summary>
        public bool AsPlainText { get; set; }

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> EnumParameters()
        {
            var p = new KeyValuePairs<string, object>
            {
                {"exlimit", "max"},
                {"exintro", IntroductionOnly},
                {"exsectionformat", "plain"},
                {"explaintext", AsPlainText},
            };
            if (MaxCharacters > 0) p.Add("exchars", MaxCharacters);
            if (MaxSentences > 0) p.Add("exsentences", MaxSentences);
            return p;
        }

        /// <inheritdoc />
        public override int GetMaxPaginationSize(bool apiHighLimits)
        {
            return apiHighLimits ? 10 : 20;
        }

        /// <inheritdoc />
        public override string PropertyName => "extracts";
    }
}
