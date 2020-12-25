using System.Threading;

namespace Sushi.Net.Library.Common
{
    public interface IGlobalCancellation
    {
        CancellationToken GetToken();
    }
}