using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sushi.Net.Library.Audio;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Decoding;
using Sushi.Net.Library.Events;

namespace Sushi.Net.Library.Providers
{
    public class AudioProvider : Provider<AudioStream, AudioMedia>
    {
                

        public int SampleRate { get;}
        public SampleType SampleType { get;}
        public int PaddingInSeconds { get;  }
        private readonly AudioReader _reader;

        public override async Task<AudioStream> ObtainAsync()
        {
            await CheckExistance().ConfigureAwait(false);
            return await _reader.LoadAsync(Media.ProcessPath, SampleRate, Media.AudioProcess, SampleType,PaddingInSeconds).ConfigureAwait(false);
        }

        public async Task<AudioStream> ObtainWithoutProcess()
        {
            await CheckExistance().ConfigureAwait(false);
            return await _reader.LoadAsync(Media.ProcessPath, SampleRate, AudioPostProcess.None, SampleType,PaddingInSeconds).ConfigureAwait(false);

        }

        public AudioProvider(AudioReader reader, AudioMedia original, int sample_rate, SampleType type, int padding, bool normalize, bool voiceRemoval , bool downmix, AudioPostProcess process, string temp_path = null, string outputPath = null)
        {
            Media = original;
            _reader = reader;
            SampleRate = sample_rate;
            PaddingInSeconds = padding;
            SampleType = type;
            Media.SetAudioProcessing(sample_rate, normalize, process,voiceRemoval, downmix);
            Media.ShouldProcess = true;
        }

    }
    
}
