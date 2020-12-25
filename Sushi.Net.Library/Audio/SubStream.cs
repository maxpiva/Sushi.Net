using System.Collections.Generic;
using System.Linq;
using Sushi.Net.Library.LibIO;

namespace Sushi.Net.Library.Audio
{
    public class SubStream
    {
        public CVMatrix Matrix { get; private set; }
        public long Size => Matrix.Size;
        

        //public SampleType SampleType => Matrix.SampleType;

        public long OriginalStart { get; }
        public long OriginalEnd { get; }
        /*
        public SubStream(NPArray data, long start, long end)
        {
            OriginalStart=start;
            OriginalEnd = end;
            Matrix = new CVMatrix(data);
        }
        */
        public SubStream(CVMatrix matrix, long start, long end)
        {
            Matrix = matrix;
            OriginalStart=start;
            OriginalEnd = end;
        }

        public List<SubStream> SplitSubStream(int n)
        {
            long start = OriginalStart;
            return Matrix.Split(n).Select(a =>
            {
                SubStream ret = new SubStream(a, start, start + a.Size);
                start += a.Size;
                return ret;
            }).ToList();
        }

        public SubStream Slice(int start, int end)
        {
            return new SubStream(Matrix.Slice(start, end),OriginalStart+start, OriginalStart+end);
        }
        /*
        public void InverseNormalize(SubStream dest)
        {
            Matrix.InverseNormalize(dest.Matrix);
        }

        public void SaveAsWav(string path, int srate)
        {
            WaveFileWriter writer = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(srate, 1));
            Matrix<float> fl = (Matrix<float>) Matrix._matrix;
            float[] data = new float[fl.Data.GetUpperBound(1)];
            for (int x = 0; x < fl.Data.GetUpperBound(1); x++)
            {
                data[x] = fl.Data[0, x] * 2 - 1;
            }
            writer.WriteSamples(data,0,data.Length);
            writer.Close();
        }
        */
    }
}