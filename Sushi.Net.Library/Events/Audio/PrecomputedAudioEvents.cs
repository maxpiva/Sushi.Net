using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Decoding;
using Sushi.Net.Library.Settings;

namespace Sushi.Net.Library.Events.Audio
{
    public class PrecomputedAudioEvents
    {
        public AudioEvents AudioEvents { get; set; }
        public Dictionary<AudioMedia, List<ComputedMovement>> Streams { get; set; }
        public List<float> Chapters { get; set; }


        private (float start, float end, bool warn) FindInSilences(List<(float start, float end)> src_sils, float s, float e, float max)
        {
            foreach ((float start, float end) r in src_sils)
            {
                if (r.start >= s && r.start <= e)
                    return (r.start, r.end, false);
                if (r.end >=s && r.end <= e)
                    return (r.start, r.end, false);
                if (s >= r.start && s <= r.end)
                    return (r.start, r.end, false);
                if (e >= r.start && e <= r.end)
                    return (r.start, r.end, false);

            }
            foreach ((float start, float end) r in src_sils)
            {
                if (r.start >= s-max && r.start <= e+max)
                    return (r.start, r.end, true);
                if (r.end >=s-max && r.end <= e+max)
                    return (r.start, r.end, true);
                if (s >= r.start-max && s <= r.end+max)
                    return (r.start, r.end, true);
                if (e >= r.start-max && e <= r.end+max)
                    return (r.start, r.end, true);

            }
            return (0, 0, true);
        }

        public PrecomputedAudioEvents(AudioEvents events)
        {
            AudioEvents = events;
            Streams = new Dictionary<AudioMedia, List<ComputedMovement>>();
        }

        public class LineCounter 
        {
            public List<string> Lines { get;  }
            public int Count { get; private set; }
            public LineCounter(List<string> lines)
            {
                Lines = lines;
                Count = 0;
            }

            public string GetNext()
            {
                if (Lines == null || Lines.Count == Count)
                    return null;
                return Lines[Count++];
            }
        }
        /*
        public static PrecomputedAudioEvents CreateFromFile(string file, List<AudioMedia> medias=null)
        {
            LineCounter lines = new LineCounter(File.ReadAllLines(file).ToList());
            List<Event> events = new List<Event>();
            Dictionary<AudioMedia, List<ComputedMovement>> streams = new Dictionary<AudioMedia, List<ComputedMovement>>();
            List<float> chaps = new List<float>();
            int pass = 0;
            AudioMedia current = null;
            List<ComputedMovement> movs = new List<ComputedMovement>();

            do
            {
                string line = lines.GetNext();
                if (line == null)
                    break;
                if (line.StartsWith("//** Events Shifts"))
                    pass = 1;
                else if (line.StartsWith("//** Chapters"))
                {
                    pass = 4;
                }
                else if (line.StartsWith("//** Adjust Points Audio Stream"))
                {
                    pass = 2;
                    if (medias == null)
                    {
                        pass = 3;
                        continue;
                    }
                    string ln = line.Substring(32);
                    int idx = ln.IndexOf(" ");
                    if (idx<0)
                    {
                        pass = 3;
                        continue;
                    }
                    int number = 0;
                    if (!int.TryParse(ln.Substring(0, idx).Trim(), out number))
                    {
                        pass = 3;
                        continue;
                    }

                    movs = new List<ComputedMovement>();
                    current = medias.FirstOrDefault(a => a.Info.Id == number);
                    if (current == null)
                    {
                        pass = 3;
                        continue;
                    }
                    streams.Add(current, movs);
                }
                else if (line.Trim() == string.Empty)
                    continue;
                else if (!line.StartsWith("//"))
                {
                    if (pass == 1)
                    {
                        string[] vals = line.Split(' ');
                        Event ev = new AudioEvent(vals[0].ParseTime(), vals[1].ParseTime());
                        ev.SetShift(float.Parse(vals[2]),0);
                        events.Add(ev);
                    }
                    else if (pass == 2)
                    {
                        ComputedMovement c = new ComputedMovement();
                        string[] vals = line.Split(' ');
                        c.RelativePosition = vals[0].ParseTime();
                        c.Difference = float.Parse(vals[1]);
                        c.AbsolutePosition = vals[2].ParseTime();
                        movs.Add(c);
                    }
                    else if (pass == 4)
                    {
                        string[] vals = line.Split(' ');
                        chaps = vals.Select(a => a.ParseTime()).ToList();
                    }
                }
            } while (true);

            PrecomputedAudioEvents preco = new PrecomputedAudioEvents(new AudioEvents(events));
            preco.Streams = streams;
            preco.Chapters = chaps;
            return preco;
        }

        public void Export(string file, SushiSettings args)
        {
            List<string> lines = new List<string>();
            lines.Add("//** Events Shifts");
            lines.Add("//Start End Shift");
            foreach(Event evn in AudioEvents.Events)
            {
                lines.Add($"{evn.Start.FormatTime2()} {evn.End.FormatTime2()} {evn.Shift.ToString(CultureInfo.InvariantCulture)}");
            }

            if (Chapters != null && Chapters.Count > 0)
            {
                lines.Add("//** Chapters");
                lines.Add(string.Join(" ",Chapters.Select(a=>a.FormatTime2())));
            }
            foreach (AudioMedia m in Streams.Keys)
            {
                lines.Add("//** Adjust Points Audio Stream "+m.Info.Id+" "+m.Mux.Path);
                lines.Add("//RelativePosition Change(Negative = Cut, Positive = Add) AbsolutePosition");
                List<ComputedMovement> movs = Streams[m];
                foreach (ComputedMovement c in movs)
                {
                    string line = $"{c.RelativePosition.FormatTime2()} {c.Difference} {c.AbsolutePosition.FormatTime2()}";
                    string err=GetErrorWarning(c, args.SilenceAssignThreshold);
                    if (c.Warning==1)
                        err="WARNING "+err;
                    else if (c.Warning==2)
                        err="ERROR "+err;
                    if (err!=null)
                        line += $" // [{err}]";
                    lines.Add(line);
                }
            }
            
            File.WriteAllLines(file, lines);
        }
        */
        public string GetErrorWarning(ComputedMovement c, float silenceThrehold)
        {
            if (c.Warning==2)
                return "no silence found in this adjust point, the destination audio files will be wrong, maybe you should change the silence threshold, or the minimum silence size.";
            else if (c.Warning==1)
                return $"no silence found in this adjust point, but one found with the Silence Threshold of {silenceThrehold}";
            return null;
            
        }
        public List<ComputedMovement> AddStream(AudioMedia media, List<Event> events, float maxseconds)
        {
            float startpos = 0;
            float last = 0;
            float total = 0;
            List<ComputedMovement> movs = new List<ComputedMovement>();
            Streams.Add(media, movs);
            List<string> second = new List<string>();
            for(int x=0;x<events.Count;x++)
            {
                ComputedMovement mov = new ComputedMovement();

                Event ev = events[x];
                float start = startpos;
                float end = ev.ShiftedStart;
                (float sstart, float ssend, bool warn) = FindInSilences(media.Silences, start, end, maxseconds);
                if (warn)
                    mov.Warning = 1;
                float shift = -ev.Shift;
                float r = shift - last;
                if (sstart == 0 && ssend == 0)
                {
                    if (start!=0)
                        mov.Warning = 2;
                    else
                        mov.Warning=0;
                }
                else
                {
                    float plus = 0;
                    if (r < 0)
                        plus = ((ssend - sstart) + r) / 2;
                    else
                        plus = (ssend - sstart) / 2;
                    if (plus > 0)
                        start = sstart + plus;
                    else
                        start = sstart;
                }
                mov.AbsolutePosition = start;
                start += total;
                if (r<0)
                    total += r;
                startpos = ev.ShiftedEnd;
                mov.RelativePosition = start;
                mov.Difference = r;
                last = shift;
                movs.Add(mov);
            }
            return movs;
        }


    }
}