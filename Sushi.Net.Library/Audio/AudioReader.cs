using System;
using System.Buffers;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

using Sushi.Net.Library.Common;
using Sushi.Net.Library.LibIO;
using Sushi.Net.Library.Tools;

namespace Sushi.Net.Library.Audio
{

    public enum AudioPostProcess
    {
        SubtitleSearch,
        AudioSearch,
        None
    }

    public class AudioReader
    {


        public Task<AudioStream> LoadAsync(string file, int sample_rate, AudioPostProcess process, SampleType type = SampleType.UInt8, int padding_seconds = 10)
        {

            return Task.Run(() =>
            {
                AudioFileReader original = null;
                try
                {
                    original = new AudioFileReader(file);
                    ISampleProvider reader = original;
                    float downsample_rate = sample_rate / (float)reader.WaveFormat.SampleRate;
                    if (downsample_rate != 1.0f ||reader.WaveFormat.Channels > 1)
                        reader = new WaveFormatConversionProvider(new WaveFormat(sample_rate, 1), original).ToSampleProvider();
                    float sampleCount = (float)Math.Ceiling(original.TotalTime.TotalSeconds * sample_rate);
                    int paddingSize = padding_seconds * sample_rate;
                    int total_size = (int)sampleCount + (paddingSize << 1);
                    ArrayPool<float> shared=ArrayPool<float>.Shared;
                    float[] val = shared.Rent(total_size);
                    reader.Read(val, paddingSize, (int)sampleCount);
                    if (process == AudioPostProcess.AudioSearch)
                        ClipForAudioSearch(val, type);
                    else if (process == AudioPostProcess.SubtitleSearch)
                        ClipForSubtitleSearch(val, type, paddingSize, (long)sampleCount);
                    if (type==SampleType.UInt8)
                    {
                        ArrayPool<byte> byteShared=ArrayPool<byte>.Shared;
                        byte[] ndata = byteShared.Rent(total_size);
                        for (int x = 0; x < total_size; x++)
                            ndata[x] = (byte) val[x];
                        shared.Return(val);
                        return new AudioStream(ndata, (long)sampleCount, paddingSize, type, sample_rate);
                    }
                    return new AudioStream(val, (long)sampleCount, paddingSize, type, sample_rate);
                }
                catch (Exception)
                {
                    throw new SushiException("Invalid Audio file");
                }
                finally
                {
                    original?.Close();
                    original?.Dispose();
                }
            });

        }
        
        private void ClipForSubtitleSearch(float[] data, SampleType type, long padding, long samplecount)
        {
            (double max, double min)=data.MedianPositiveNegative(padding, padding+ samplecount);
            data.Clipping((float)min*3,(float)max*3);
            if (type==SampleType.UInt8)
                data.NormalizeToByte();
        }
        
        public void ClipForAudioSearch(float[] data, SampleType type)
        {
            //(double max, double min)=Data.MedianPositiveNegative(Padding, Padding + SampleCount);
            
            float min = data.Min();
            float max = data.Max();
            if (Math.Abs(max) > Math.Abs(min) && min<0)
            {
                min=-max;
            }
            else if (Math.Abs(min) > Math.Abs(max) && max > 0)
            {
                max=-min;
            }
            data.Clipping((float)min,(float)max);
            //float mul = 1/ Math.Max(min, max);
            //Data.Mul(mul);
            if (type == SampleType.UInt8)
                data.NormalizeToByte();
        }
    }


}
