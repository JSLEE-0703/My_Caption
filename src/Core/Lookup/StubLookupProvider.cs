using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MyCaption.Core.Models;

namespace MyCaption.Core.Lookup
{
    public sealed class StubLookupProvider : ILookupProvider
    {
        public string DisplayName
        {
            get { return "Stub dictionary"; }
        }

        public Task<LookupResult> LookupAsync(string word, CancellationToken cancellationToken)
        {
            string normalized = string.IsNullOrWhiteSpace(word)
                ? string.Empty
                : word.Trim().ToLowerInvariant();

            string phonetic = string.Format(CultureInfo.InvariantCulture, "/{0}/", normalized);
            List<LookupMeaning> meanings = new List<LookupMeaning>
            {
                new LookupMeaning("noun", "Stub dictionary entry for provider integration testing.")
            };

            return Task.FromResult(new LookupResult(normalized, phonetic, meanings, string.Empty, string.Empty, "Stub dictionary provider result.", true));
        }
    }
}
