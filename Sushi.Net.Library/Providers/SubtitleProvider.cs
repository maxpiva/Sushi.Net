using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Decoding;
using Sushi.Net.Library.Events;
using Sushi.Net.Library.Media;

namespace Sushi.Net.Library.Providers
{
    public class SubtitleProvider : Provider<IEvents, SubtitleMedia>
    {
        public override async Task<IEvents> ObtainAsync()
        {
            await CheckExistance().ConfigureAwait(false);
            return await IEvents.CreateFromFileAsync(Media.ProcessPath, Media.RequireDemuxing).ConfigureAwait(false);
        }

        public SubtitleProvider(SubtitleMedia media)
        {
            Media = media;
        }
       
        
    }
}