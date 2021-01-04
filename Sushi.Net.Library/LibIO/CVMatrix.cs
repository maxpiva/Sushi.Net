using System;
using System.Buffers;
using System.Collections.Generic;
using OpenCvSharp;
using Sushi.Net.Library.Audio;

namespace Sushi.Net.Library.LibIO
{
    public class CVMatrix
    {
        public Memory<float> RawFloats { get; }
        public Memory<byte> RawBytes { get; }
        public bool IsByteArray { get; }
        public long Size => IsByteArray ? RawBytes.Length : RawFloats.Length;

        public class CVInputArray : IDisposable
        {
            private MemoryHandle handler;
            public InputArray Array { get; }
            public unsafe CVInputArray(CVMatrix matrix)
            {
                if (matrix.IsByteArray)
                {
                    handler = matrix.RawBytes.Pin();
                    Array = InputArray.Create(new Mat(matrix.RawBytes.Length, 1, MatType.CV_8UC1, (IntPtr)handler.Pointer));
                }
                else
                {
                    handler = matrix.RawFloats.Pin();
                    Array = InputArray.Create(new Mat(matrix.RawFloats.Length, 1, MatType.CV_32FC1, (IntPtr)handler.Pointer));
                }
            }

            public void Dispose()
            {
                handler.Dispose();
            }
        }


        public CVInputArray AsDisposableInputArray() => new CVInputArray(this);

        public CVMatrix(float[] data)
        {
            RawFloats = new Memory<float>(data);
            IsByteArray = false;
        }

        public CVMatrix(byte[] data)
        {
            RawBytes = new Memory<byte>(data);
            IsByteArray = true;
        }

        public CVMatrix(Memory<float> data)
        {
            RawFloats = data;
            IsByteArray = false;
        }

        public CVMatrix(Memory<byte> data)
        {
            RawBytes = data;
            IsByteArray = false;
        }

        public CVMatrix Slice(int start, int end)
        {
            return IsByteArray ? new CVMatrix(RawBytes.Slice(start, end - start)) : new CVMatrix(RawFloats.Slice(start, end - start));
        }

        public List<CVMatrix> Split(int n)
        {
            int sz = 0;
            int size = (int) Size;
            int pos = 0;
            List<CVMatrix> res = new();
            for (int x = 0; x < n; x++)
            {
                size -= sz;
                sz = size / (n - x);
                res.Add(Slice(pos, pos + sz));
                pos += sz;
            }

            return res;
        }



        public (long position, float difference) MatchTemplate(Mat<float> output, CVMatrix pattern, Mode mode=Mode.SqDiffNormed)
        {
            int size = (int) (Size - pattern.Size + 1);
            int pos = 0;
            float f;
            using (CVInputArray inp = AsDisposableInputArray())
            using (CVInputArray pat = pattern.AsDisposableInputArray())
            {
                Cv2.MatchTemplate(inp.Array, pat.Array, OutputArray.Create(output), (TemplateMatchModes) mode);
                MatIndexer<float> or = output.GetIndexer();
                if (mode == Mode.SqDiffNormed || mode == Mode.SqDiff)
                {
                    f = float.MaxValue;
                    for (int x = 0; x < size; x++)
                    {
                        if (or[x, 0] < f)
                        {
                            f = or[x, 0];
                            pos = x;
                        }
                    }
                }
                else
                {
                    f = float.MinValue;
                    for (int x = 0; x < size; x++)
                    {
                        if (or[x, 0] > f)
                        {
                            f = or[x, 0];
                            pos = x;
                        }
                    }
                }
            }
            return (pos, f);
        }
        public int FindSilence(int start, int end, int min_length, int db_threshold=-50)
        {
            int add = start > end ? -1 : 1;
            int count=0;
            
            if (IsByteArray)
            {
                Span<byte> array = RawBytes.Span;
                while (start != end)
                {
                    float val = (float)(array[start])/127.5f -1;
                    double db = 20 * Math.Log10(Math.Abs(val));
                    if (db < db_threshold)
                    {
                        count++;
                        if (count == min_length)
                            return start - ((count-1) * add);
                    }
                    else
                        count = 0;

                    start += add;
                }
                return end;

            }
            else
            {
                Span<float> array = RawFloats.Span;
                while (start != end)
                {
                    double db = 20 * Math.Log10(Math.Abs(array[start]));
                    if (db < db_threshold)
                    {
                        count++;
                        if (count == min_length)
                            return start -  ((count-1) * add);
                    }
                    else
                        count = 0;
                    start+=add;
                }
                return end;

            }
        }

    }
}
