using System.Threading;
using System.Threading.Tasks;
using MyCaption.Core.Models;

namespace MyCaption.Core.Lookup
{
    public interface ILookupProvider
    {
        string DisplayName { get; }

        Task<LookupResult> LookupAsync(string word, CancellationToken cancellationToken);
    }
}
