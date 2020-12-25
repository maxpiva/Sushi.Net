using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Sushi.Net.Library.LibIO
{

    public static class ArrayExtensions
    {
        public static (double positive, double negative) MedianPositiveNegative(this float[] raw, long start, long end)
        {
            ArrayPool<float> pool=ArrayPool<float>.Shared;
            float[] r=pool.Rent((int)(end - start));
            int pos = 0;
            int negpos=(int)(end - start);
            int total = negpos;
            for (int x = (int)start; x < end ; x++)
            {
                float v = raw[x];
                if (v > 0)
                {
                    r[pos] = v;
                    pos++;
                }
                else if (v < 0)
                {
                    negpos--;
                    r[negpos] = v;
                }
            }

            float max = pos>0 ? Median(r, 0, pos) : 0;
            float min = negpos!=total ? Median(r, negpos, total) : 0;
            pool.Return(r);
            return (max, min);
        }
        public static float Median(this float[] raw)
        {
            ArrayPool<float> pool=ArrayPool<float>.Shared;
            float[] r=pool.Rent(raw.Length);
            raw.CopyTo(new Span<float>(r));
            float res = Median(r, 0, raw.Length);
            pool.Return(r);
            return res;
        }
        public static float Max(this float[] raw)
        {
            float val = float.MinValue;
            for (int x = 0; x < raw.Length; x++)
            {
                float v = raw[x];
                if (v > val)
                    val = v;
            }
            return val;
        }
        public static float Min(this float[] raw)
        {
            float val = float.MaxValue;
            for (int x = 0; x < raw.Length; x++)
            {
                float v = raw[x];
                if (v < val)
                    val = v;
            }
            return val;
        }
        public static void Clipping(this float[] raw, float min, float max)
        {
            float mul = 1F / (max - min);
            for (int x = 0; x < raw.Length; x++)
            {
                float v = raw[x];
                if (v>max)
                    v=max;
                else if (v<min)
                    v=min;
                v -= min;
                v *= mul;
                raw[x] = v;

            }
        }

        public static void Mul(this float[] raw, float f)
        {
            for (int x = 0; x < raw.Length; x++)
                raw[x] *= f;
        }
        public static float Mean(this float[] raw)
        {
            double n = 0;
            for (int x = 0; x < raw.Length; x++)
                n += raw[x];
            n/=(double)raw.Length;
            return (float)n;
        }
        public static float Std(this float[] raw)
        {
            ArrayPool<float> pool=ArrayPool<float>.Shared;
            float[] r=pool.Rent(raw.Length);
            float mean=raw.Mean();
            for (int x = 0; x < raw.Length; x++)
            {
                float b = raw[x] - mean;
                r[x] = b * b;
            }

            float ret = (float)Math.Sqrt(r.Mean());
            pool.Return(r);
            return ret;
        }
        public static void NormalizeToByte(this float[] raw)
        {
            for (int x = 0; x < raw.Length; x++)
                raw[x] = raw[x] * 255.0f + 0.5f;
        }
        public static float Average(this float[] raw, float[] weights)
        {
            double sum = 0;
            double ws = 0;
            for (int x = 0; x < raw.Length; x++)
            {
                sum += raw[x] * weights[x];
                ws += weights[x];
            }
            return (float)(sum/ws);
        }

        public static float[] Slice(this float[] raw, int start, int end)
        {
            Span<float> f = new Span<float>(raw);
            return f.Slice(start, end - start).ToArray();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void SwapElements(float* p, float* q)
        {
            float temp = *p;
            *p = *q;
            *q = temp;
        }

        private static unsafe float Median(float[] raw, int start, int n)
        {
            int end = n - 1;
            int median = (start + end) / 2;
            fixed (float* ptr = raw)
            {
                while(true)
                {
                    if (end <= start)
                        return raw[median];

                    if (end == start + 1)
                    {
                        if (raw[start] > raw[end]) 
                            SwapElements(ptr + start, ptr + end);
                        return raw[median];
                    }

                    int middle = (start + end) / 2;
                    if (raw[middle] > raw[end]) 
                        SwapElements(ptr + middle, ptr + end);

                    if (raw[start] > raw[end]) 
                        SwapElements(ptr + start, ptr + end);

                    if (raw[middle] > raw[start]) 
                        SwapElements(ptr + middle, ptr + start);

                    SwapElements(ptr + middle, ptr + start + 1);

                    int start2 = start + 1;
                    int end2 = end;
                    while(true)
                    {
                        do
                        {
                            start2++;
                        } while (raw[start] > raw[start2]);
                        do
                        {
                            end2--;
                        } while (raw[end2] > raw[start]);

                        if (end2 < start2)
                            break;

                        SwapElements(ptr + start2, ptr + end2);
                    }

                    SwapElements(ptr + start, ptr + end2);

                    if (end2 <= median)
                        start = start2;
                    if (end2 >= median)
                        end = end2 - 1;
                }
            }
        }
    }
}