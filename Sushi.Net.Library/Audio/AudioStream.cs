using System;
using System.Buffers;
using System.Collections.Generic;
using OpenCvSharp;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.LibIO;

namespace Sushi.Net.Library.Audio
{
    
    
    public class AudioStream : IDisposable
    {
        public CVMatrix Data { get;  }
        private byte[] byte_original_data;
        private float[] float_original_data;
        public long Padding { get; }
        public long SampleCount { get; }
        public int SampleRate { get; }
        public SampleType SampleType { get; }
        public float DurationInSeconds => SampleCount / (float)SampleRate;
        public float PaddingInSeconds => Padding / (float)SampleRate;


        private Mat<float> _mat;

        public Mat<float> Mat => _mat;
        
        public AudioStream(byte[] data, long sample_count, long padding, SampleType type, int sampleRate)
        {
            SampleCount = sample_count;
            byte_original_data = data;
            Data = new CVMatrix(data);
            SampleType=type;
            SampleRate = sampleRate;
            Padding = padding;
            _mat = new Mat<float>(data.Length, 1);
        }
        public AudioStream(float[] data, long sample_count, long padding, SampleType type, int sampleRate)
        {
            SampleCount = sample_count;
            float_original_data=data;
            Data = new CVMatrix(data);
            SampleType=type;
            SampleRate = sampleRate;
            Padding = padding;
            _mat = new Mat<float>(data.Length, 1);
        }

        public float FindSilence(float start, float end, float min_length=0.1f, int threshold=-50)
        {
            int start_sample = GetSampleForTime(start);
            int end_sample = GetSampleForTime(end);
            int length = (int)(min_length * SampleRate);
            int position=Data.FindSilence(start_sample, end_sample, length, threshold);
            return (position - Padding) / (float) SampleRate;
        }


        public int GetSampleForTime(float timestamp)
        {
            int value = (int)((SampleRate * timestamp) + Padding);
            if (value > Data.Size)
                value = (int)Data.Size;
            if (value < 0)
                value = 0;
            return value;
        }

        public SubStream GetSubStream(float start, float end)
        {

            int start_off = GetSampleForTime(start);
            int end_off = GetSampleForTime(end);
            CVMatrix slice = Data.Slice(start_off, end_off);
            return new SubStream(slice,start_off, end_off);
        }


        private Dictionary<string, (long, float)> cached_finds = new Dictionary<string, (long, float)>();
        



        public (float difference, float time) FindSubStream(SubStream pattern, float window_center, float window_size, Mode mode=Mode.SqDiffNormed)
        {
            float padleft = -PaddingInSeconds;
            float padright = DurationInSeconds+ PaddingInSeconds;
            float start_time = (window_center - window_size).Clip(padleft, padright);
            float end_time = (window_center + window_size+(pattern.Size / (float) SampleRate)).Clip(padleft, padright);
            int start_sample = GetSampleForTime(start_time);
            int end_sample = GetSampleForTime(end_time);

            
            CVMatrix window = Data.Slice(start_sample, end_sample);
            string key = pattern.OriginalStart + "_" + pattern.OriginalEnd + "_" + start_sample + "_" + end_sample;
            long position;
            float difference;
            if (cached_finds.ContainsKey(key))
            {
                (position, difference) = cached_finds[key];
            }
            else
            {
                (position, difference) = window.MatchTemplate(Mat, pattern.Matrix, mode);
                cached_finds.Add(key,(position, difference));
            }
            return (difference, start_time + (position / (float)SampleRate));
        }

        public void Dispose()
        {
            if (byte_original_data != null)
            {
                ArrayPool<byte> shared = ArrayPool<byte>.Shared;
                shared.Return(byte_original_data);
            }
            if (float_original_data != null)
            {
                ArrayPool<float> shared = ArrayPool<float>.Shared;
                shared.Return(float_original_data);
            }
        }
    }
}
