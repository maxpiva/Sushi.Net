using System;

namespace Sushi.Net.Library.Decoding
{
    public class DummyMedia : Media
    {
        public DummyMedia(Mux m) : base(m, null)
        {

        }

        public override void SetPaths(string processPath, string output_path)
        {
            throw new NotImplementedException();
        }
    }
}