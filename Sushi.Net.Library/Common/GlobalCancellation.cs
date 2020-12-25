using System.Threading;

namespace Sushi.Net.Library.Common
{
    public class GlobalCancellation : IGlobalCancellation
    {
        public CancellationTokenSource Source { get; }
            
        public GlobalCancellation()
        {
            Source = new CancellationTokenSource();
        }
        public CancellationToken GetToken()
        {
            return Source.Token;
        }
    }
}