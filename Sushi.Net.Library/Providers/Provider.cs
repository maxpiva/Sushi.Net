using System.IO;
using System.Threading.Tasks;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Decoding;

namespace Sushi.Net.Library.Providers
{
    public abstract class Provider<T,S> where S: Decoding.Media
    {

        public S Media { get; internal set;}

        public abstract Task<T> ObtainAsync();
        
        public async Task CheckExistance()
        {
            await ResolveAsync().ConfigureAwait(false);
            if (!File.Exists(Media.ProcessPath))
                throw new SushiException($"Unable to find {Media.ProcessPath ?? "null"}, aborting...");
        }
        public async Task ResolveAsync()
        {
            if (Media.Mux == null || !Media.ShouldProcess)
                return;
            if (Media.Mux != null && !Media.Processed)
                await Media.Mux.ProcessAsync().ConfigureAwait(false);
        }
    }
}