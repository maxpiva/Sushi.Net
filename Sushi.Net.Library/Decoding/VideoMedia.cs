using System;
using Sushi.Net.Library.Media;

namespace Sushi.Net.Library.Decoding
{
    public class VideoMedia : Media
    {
        public VideoMedia(Mux mux, MediaStreamInfo info) : base(mux, info)
        {
        }


        public override void SetPaths(string processPath, string output_path)
        {
            throw new NotImplementedException();
        }
    }
}