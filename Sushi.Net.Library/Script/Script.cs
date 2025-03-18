using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Decoding;
using Sushi.Net.Library.Events;
using Sushi.Net.Library.Events.Audio;
using Sushi.Net.Library.Providers;
using Sushi.Net.Library.Settings;

namespace Sushi.Net.Library.Script
{
    public class Script : BasicParser
    {
        public Mux Mux { get; private set; }
        public List<StreamCommands> Streams { get; set; }
        private Demuxer _demuxer;
        private ILogger _logger;
        private bool _muxFound;
        private HashSet<int> _usedIndexes = new HashSet<int>();
        private StreamCommands _current;
        private float SubtitleDelay { get; set;}
        public float Duration { get; set;}

        public double ReScale { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public static async Task<Script> ParseScriptAsync(Demuxer demuxer, ILogger logger,  List<string> lines)
        {
            HashSet<int> Indexes = new HashSet<int>();
            Script s = new Script(demuxer, logger, new LineCounter(lines));
            await s.ProcessAsync().ConfigureAwait(false);
            if (s._current!=null)
                await s.ProcessCurrentAsync().ConfigureAwait(false);
            return s;
        }
        public static Task<Script> CreateScriptAsync(Demuxer demuxer, ILogger logger,  Mux mux, Dictionary<List<ComputedMovement>, List<Decoding.Media>> movements, float subtitle_delay, float duration, int width, int height)
        {
            Script s = new Script(demuxer, logger, mux);
            s.SubtitleDelay = subtitle_delay;
            s.Duration = duration;
            s.Width = width;
            s.Height = height;
            s.ReScale = mux.ReScale;
            foreach (List<ComputedMovement> m in movements.Keys)
            {
                StreamCommands cmd = new StreamCommands(movements[m], m);
                s.Streams.Add(cmd);
            }
            return Task.FromResult(s);
        }

        public Script(Demuxer demuxer, ILogger logger,  LineCounter counter) : base(counter)
        {
            _demuxer=demuxer;
            _logger=logger;
            Streams=new List<StreamCommands>();
        }
        public Script(Demuxer demuxer, ILogger logger,  Mux mux) 
        {
            _demuxer=demuxer;
            _logger=logger;
            Mux = mux;
            Streams=new List<StreamCommands>();
        }
        private float GetStart(StreamCommands str, int index, float duration)
        {
            do
            {
                if (str.Commands.Count <= index)
                    return duration;
                if (!(str.Commands[index] is CopyCommand))
                    return str.ToAbsolute(str.Commands[index]);
                index++;
            } while (true);

        }


        public List<AudioShift> GetAudioShifts()
        {

            List<AudioShift> results = new List<AudioShift>();
            foreach (StreamCommands str in Streams)
            {
                if (str.Medias.Any(a=>a is AudioMedia))
                {
                    float duration = str.Duration.HasValue && str.Duration.Value > 0 ? str.Duration.Value : Duration;
                    List<IShiftBlock> splits = GetBlocks(str);
                    foreach (AudioMedia media in str.Medias.Where(a => a is AudioMedia).Cast<AudioMedia>())
                        results.Add(new AudioShift { Blocks = splits, Media=media, Duration=duration});
                }
            }
            return results;
       
        }

        private List<Event> FilterEvents(List<Event> events, Block block, float dst_duration)
        {
            List<Event> ret = new List<Event>();
            List<Event> evs = events.Where(a => (a.End >block.Start && a.End <= block.End) || (a.Start < block.End && a.Start >= block.Start)).ToList();
            foreach (Event e in evs)
            {
                Event n = e.Clone();
                n.SetShift(block.Shift,1);
                if (n.ShiftedStart < dst_duration && n.ShiftedEnd > 0)
                {
                    if (n.ShiftedStart < 0)
                        n.Start = 0 - n.Shift;
                    if (n.ShiftedEnd > dst_duration)
                        n.End = dst_duration - n.Shift;
                }

                ret.Add(n);
                if (!block.IsMove)
                    events.Remove(e);
            }
            return ret;
        }



        private List<IShiftBlock> GetBlocks(StreamCommands str)
        {
            List<IShiftBlock> blocks = new List<IShiftBlock>();

            float duration = str.Duration.HasValue && str.Duration.Value > 0 ? str.Duration.Value : Duration;
            float processed = 0;
            float original = 0;
            int cnt = 1;
            List<Event> ret = new List<Event>();
            for (int x = 0; x < str.Commands.Count; x++)
            {
                BaseCommand cmd = str.Commands[x];
                float pos = str.ToAbsolute(cmd);
                Block b = new Block { Start = original, End = pos, Shift = processed - original, IsMove = false };
                if (original!=pos)
                    blocks.Add(b);
                processed += (pos - original);
                if (b.Start!=0 || b.End!=0)
                    _logger.LogInformation($"Block {cnt++} {b} -  {processed.FormatTime2()}");
                original=pos;
                switch (cmd)
                {
                    case CutCommand cc:
                        original += cc.Duration;
                        break;
                    case FillCommand fill:
                        blocks.Add(new SilenceBlock { Duration = fill.Duration });
                        processed += fill.Duration;
                        break;
                    case CopyCommand copy:
                        blocks.Add(new Block { Start = str.ToAbsoluteMove(copy), End = str.ToAbsoluteMove(copy) + copy.Duration, Shift=processed-str.ToAbsolute(copy), IsMove = true });
                        processed += copy.Duration;
                        break;
                }
            }

            if (processed < duration)
            {
                float end = duration - processed + original;
                Block b = new Block { Start = original, End = end, IsMove = false, Shift = processed - original };
                if (b.Start != 0 || b.End != 0)
                    _logger.LogInformation($"Block {cnt++} {b} -  {processed.FormatTime2()}");
                blocks.Add(b);
            }
            return blocks;
        }



        public async Task<List<SubtitleShift>> GetSubtitleShiftAsync()
        {
            List<SubtitleShift> evs=new List<SubtitleShift>();

            foreach (StreamCommands str in Streams)
            {
                if (str.Commands.Count>0)
                    str.Commands[0].Time = 0;
                List<IShiftBlock> blocks = GetBlocks(str);
                foreach (SubtitleMedia media in str.Medias.Where(a => a is SubtitleMedia).Cast<SubtitleMedia>())
                {
                    SubtitleProvider prov = new SubtitleProvider(media);
                    _logger.LogInformation($"Loading Subtitle {prov.Media.ProcessPath}...");
                    IEvents events = await prov.ObtainAsync().ConfigureAwait(false);
                    List<Event> move_events = events.Events.Select(a => a.Clone()).ToList();
                    float duration = str.Duration.HasValue && str.Duration.Value > 0 ? str.Duration.Value : Duration;
                    float processed = 0;
                    float original = 0;
                    List<Event> ret = new List<Event>();
                    foreach (Block block in blocks.Where(a => a is Block).Cast<Block>())
                        ret.AddRange(FilterEvents(events.Events, block, duration));
                    events.Events = ret;
                    evs.Add(new SubtitleShift
                    {
                        Events = events,
                        Width = this.Width,
                        Height = this.Height,
                        SubDelay = str.SubtitleDelay ?? SubtitleDelay,
                        SubtitleMedia = media,
                        Duration = duration
                    });

                }
            }
            return evs;
        }
        /*
        private static List<Event> ProcessEvents(List<IShiftBlock> blocks, List<Event> events, float dst_duration)
        {
            List<Event> filter = new List<Event>();
            float dstpos = 0;
            foreach (IShiftBlock block in blocks)
            {
                switch (block)
                {
                    case SilenceBlock sb:
                        dstpos += sb.Duration;
                        break;
                    case Block b:
                        List<Event> evs = events.Where(a => (a.End > b.Start && a.End <= b.End) || (a.Start < b.End && a.Start >= b.Start)).ToList();
                        foreach (Event e in evs)
                        {
                            Event n = e.Clone();
                            n.SetShift(dstpos - n.Start,1);
                            if (n.ShiftedStart < dst_duration && n.ShiftedEnd > 0)
                            {
                                if (n.ShiftedStart < 0)
                                    n.Start = 0 - n.Shift;
                                if (n.ShiftedEnd > dst_duration)
                                    n.End = dst_duration - n.Shift;
                            }
                            filter.Add(n);
                        }
                        dstpos += b.End - b.Start;
                        break;
                }
            }

            int x = 0;
            while (x+1<filter.Count)
            {
                if (filter[x].Text == filter[x + 1].Text && filter[x].End >= filter[x + 1].Start)
                {
                    filter[x + 1].Start = filter[x].Start;
                    filter.RemoveAt(x);
                }
                else
                    x++;
            } 
            return filter;
        }
        */
        private async Task ProcessCurrentAsync()
        {
            Streams.Add(_current);
            await _current.ProcessAsync().ConfigureAwait(false);
            _current=null;
        }
        public override async Task<bool> ProcessAsync(string[] command)
        {
            switch (command[0].ToUpperInvariant())
            {
                case "F":
                    if (command.Length <= 1)
                        throw new ArgumentException("File Command should contains a filename as second parameter.");
                    string file = command[1].Trim().Strip();
                    if (!System.IO.File.Exists(file))
                        throw new ArgumentException($"File {file} not found.");
                    if (_muxFound)
                        throw new ArgumentException("There is already an F command in this script, only one per script is supported.");
                    _muxFound=true;
                    Mux = new Mux(_demuxer, file, _logger);
                    await Mux.GetMediaInfoAsync().ConfigureAwait(false);
                    break;
                case "R":
                    if (command.Length < 3)
                        throw new ArgumentException("Resolution Command should contains the width and height of the video stream.");
                    int width = 0;
                    int.TryParse(command[1], out width);
                    Width = width;
                    int height = 0;
                    int.TryParse(command[2], out height);
                    Height = height;
                    break;
                case "P":
                    if (command.Length < 2)
                        throw new ArgumentException("File Command should contains the duration and optionally the subtitle delay and rescaling.");
                    float subd = 0;
                    float.TryParse(command[1], out subd);
                    Duration = subd;
                    subd = 0;
                    if (command.Length >= 3)
                        float.TryParse(command[2], out subd);
                    double rescale = 0;
                    if (command.Length >= 4)
                        double.TryParse(command[3], out rescale);
                    ReScale = rescale;
                    SubtitleDelay = subd;
                    break;
                case "S":
                    if (command.Length <= 1)
                        throw new ArgumentException("File Command should contains a subtitle file as second parameter.");
                    string sub = command[1].Trim().Strip();
                    if (!System.IO.File.Exists(sub))
                        throw new ArgumentException($"File {sub} not found.");
                    _current ??= new StreamCommands(Lines);
                    _current.Medias.Add(new SubtitleMedia(sub) { ShouldProcess=true });;
                    break;
                case "I":
                    if (command.Length <= 1)
                        throw new ArgumentException("Stream Index Command should contains at least one index.");
                    List<string>  indexes =  command[1].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim().ToUpperInvariant()).ToList();
                    HashSet<int> used = new HashSet<int>();
                    foreach (string index in indexes)
                    {
                        if (int.TryParse(index, out int idx))
                        {
                            if (!used.Contains(idx))
                                used.Add(idx);
                        }
                    }
                    List<int> intersect = used.Intersect(_usedIndexes).ToList();
                    if (intersect.Any())
                    {
                        string stridx = string.Join(", ", intersect.Select(a => a.ToString()));
                        throw new ArgumentException($"Stream Index/es {stridx} are already used in the script.");
                    }
                    _current ??= new StreamCommands(Lines);
                    foreach (int idx in used)
                    {
                        Decoding.Media m = Mux.GetShiftableMedia(idx);
                        if (m != null)
                        {
                            _usedIndexes.Add(idx);
                            if (m is SubtitleMedia)
                                m.ShouldProcess=true;
                            _current.Medias.Add(m);
                        }
                    }
                    break;
                default:
                    if (_current != null)
                    {
                        Lines.Count--;
                        await ProcessCurrentAsync().ConfigureAwait(false);
                    }
                    break;
            }
            return false;
        }

        private List<string> Usage()
        {
            List<string> l = new List<string>();
            l.Add("// script commands");
            l.Add("// ");
            l.Add("// F \"filename\" - File to process. ");
            l.Add("// Example:    F \"file.mkv\"");
            l.Add("//");
            l.Add("// R width height - Destination Video Resolution, important if you want to resize the subtitles");
            l.Add("// Example:    R 1920 1080");
            l.Add("//");
            l.Add("// P duration subtitle_audio_adjust - Stream duration & Subtitle audio adjust in seconds and possible rescaling");
            l.Add("// Example:    P 2812.23 0");
            l.Add("//");
            l.Add("// I comma separated stream indexes to process.  ");
            l.Add("// Example:    I 2,3,4  (Process stream 2,3 & 4) ");
            l.Add("// ");
            l.Add("// S \"filename\" - Additional external subtitle to process");
            l.Add("// Example:    S \"subt.ass\"");
            l.Add("//");
            l.Add("// X time size - Cut at Time ");
            l.Add("// Example:    X 00:01:23.56 1.23 ");
            l.Add("//");
            l.Add("// Notice the time is relative which means, is the time of the stream after previous operations.");
            l.Add("//");
            l.Add("// Z time size - Insert a silence");
            l.Add("// Example:    Z 00:01:23.56 2.50");
            l.Add("//");
            l.Add("// Notice the time is relative which means, is the time of the stream after previous operations.");
            l.Add("//");
            l.Add("// C time1 time2 size - Copy the Block at [time2 size] to time1");
            l.Add("// Example:    C 00:01:23.56 00:03:42.50 1.50");
            l.Add("//");
            l.Add("// Notice the time is relative which means, is the time of the stream after previous operations.");
            l.Add("//");
            l.Add("// If you want to use absolute time, you can use XA, ZA & CA commands");
            l.Add("//");
            return l;
        }

        public override List<string> Serialize(bool absolute=false)
        {
            List<string> ls = new List<string>();
            ls.AddRange(Usage());
            ls.Add($"F {Mux.Path.Quote()}");
            ls.Add($"R {Width} {Height}");
            string pbase = $"P {Duration.ToString(CultureInfo.InvariantCulture)}";
            if (SubtitleDelay != 0 || ReScale!=0)
                pbase+=$" {SubtitleDelay.ToString(CultureInfo.InvariantCulture)}";
            if (ReScale!=0)
                pbase += $" {ReScale:F8}";
            ls.Add(pbase);
            foreach (StreamCommands cmds in Streams)
                ls.AddRange(cmds.Serialize(absolute));
            return ls;
        }
    }
}
