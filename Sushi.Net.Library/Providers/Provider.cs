using System.IO;
using System.Threading.Tasks;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Decoding;

namespace Sushi.Net.Library.Providers
{
    public abstract class Provider<T>
    {
        public string Path { get; internal set;}
        public Mux Mux { get; internal set;}
        public bool RequireDemuxing { get; internal set;}
        
        public abstract Task<T> ObtainAsync();
        
        public async Task CheckExistance()
        {
            await ResolveAsync().ConfigureAwait(false);
            if (!File.Exists(Path))
                throw new SushiException($"Unable to find {Path ?? "null"}, aborting...");
        }
        public async Task ResolveAsync()
        {
            if (!RequireDemuxing)
                return;
            if (Mux != null && !Mux.Processed)
                await Mux.ProcessAsync().ConfigureAwait(false);
        }

    }
}