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
            string summary = "Word hit-testing is active. Hook ILookupProvider to your local dictionary, browser helper, or offline lexicon next.";
            string hint = "This popup proves Alt interaction and token-level click handling are wired end-to-end.";

            return Task.FromResult(new LookupResult(normalized, phonetic, summary, hint));
        }
    }
}
