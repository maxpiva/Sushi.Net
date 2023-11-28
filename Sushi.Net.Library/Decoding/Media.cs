using Sushi.Net.Library.Media;

namespace Sushi.Net.Library.Decoding
{
    public abstract class Media
    {
        private Mux _mux;
        private Demuxer _demuxer;

        public MediaStreamInfo Info { get; set; }
        public bool Processed { get; set; }
        public bool ShouldProcess { get; set; }
        public string ProcessPath { get;  set; }
        public string OutputPath { get; set; }
        public Mux Mux => _mux;
        public Demuxer Demuxer => _demuxer;
        public string OutputCodec { get; set; }
        public string OutputParameters {get; set; }
        public bool RequireDemuxing => Mux!=null & ShouldProcess && !Processed;

        public Media(Mux mux, MediaStreamInfo info)
        {
            _mux = mux;
            _demuxer = mux?.Demuxer;
            Info = info;
            Processed = false;
        }

        public abstract void SetPaths(string processPath, string output_path);
    }
}