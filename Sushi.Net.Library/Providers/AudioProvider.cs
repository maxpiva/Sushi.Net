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
    public class AudioProvider : Provider<AudioStream>
    {
                

        public int SampleRate { get;}
        public SampleType SampleType { get;}
        public int PaddingInSeconds { get;  }
        private readonly AudioReader _reader;
        public string OutputPath { get; private set; }

        public override async Task<AudioStream> ObtainAsync()
        {
            await CheckExistance().ConfigureAwait(false);
            return await _reader.LoadAsync(Path, SampleRate,  Mux.AudioProcess, SampleType,PaddingInSeconds).ConfigureAwait(false);
        }

        public async Task<AudioStream> ObtainWithoutProcess()
        {
            await CheckExistance().ConfigureAwait(false);
            return await _reader.LoadAsync(Path, SampleRate, AudioPostProcess.None, SampleType,PaddingInSeconds).ConfigureAwait(false);

        }
        private void SetOuputPath(string path, string outputPath)
        {
            if (outputPath != null)
            {
                if (Directory.Exists(outputPath))
                    OutputPath = path.FormatFullPath("_sushi.flac", outputPath);
                else
                    OutputPath = outputPath;
            }
        }
        public AudioProvider(AudioReader reader, Mux original, int? index, int sample_rate, SampleType type, int padding, AudioPostProcess process, string temp_path=null,string outputPath=null)
        {
            Mux = original;
            _reader=reader;
            SampleRate = sample_rate;
            PaddingInSeconds = padding;
            SampleType=type;
            if (original.IsWav)
            {
                Path = original.Path;
                SetOuputPath(Path, outputPath);
            }
            else
            {
                if (index.HasValue && !original.MediaInfo.Audios.Any(a => a.Id == index.Value))
                {
                    if (index.Value == 0 && original.MediaInfo.Audios.Count == 1)
                        index = original.MediaInfo.Audios.First().Id;
                    else
                        throw new SushiException($"Invalid audio index {index.Value}.");
                }
                Path= original.Path.FormatFullPath("_sushi.wav", temp_path);
                original.SetAudio(index,Path,sample_rate, process);
                RequireDemuxing=true;
                SetOuputPath(original.Path, outputPath);


            }

        }

        public Task ShiftAudioAsync(List<Split> splits)
        {
            return Mux.ShiftAudioAsync(OutputPath, splits);


        }
        
    }
    
}
