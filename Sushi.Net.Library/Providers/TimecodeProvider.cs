using System.Threading.Tasks;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Decoding;
using Sushi.Net.Library.Timecoding;

namespace Sushi.Net.Library.Providers
{
    public class TimecodeProvider : Provider<ITimeCodes, DummyMedia>
    {
        public Mux Mux { get; private set; }
        public override async Task<ITimeCodes> ObtainAsync()
        {
            if (FPS.HasValue)
                return new CFR(FPS.Value);
            await CheckExistance().ConfigureAwait(false);
            return await VFR.CreateFromFileAsync(Media.ProcessPath).ConfigureAwait(false);
        }

        public float? FPS { get; }
        
        public TimecodeProvider(Mux original, string path, float? fps, string temp_path) 
        {
            Mux = original;
            Media = new DummyMedia(original);
            if (path!=null)
                Media.ProcessPath=path;
            else if (fps.HasValue)
                FPS = fps;
            else if (Mux!=null && Mux.Videos.Count>0)
            {
                Media.ProcessPath = original.Path.FormatFullPath("_sushi_timecodes.txt", temp_path);
                original.SetTimecodes(Media.ProcessPath);
            }
            else
                throw new SushiException("Fps, timecodes or video files must be provided if keyframes are used");
        }
    }
}