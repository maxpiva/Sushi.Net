using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Decoding;
using Sushi.Net.Library.Events;
using Sushi.Net.Library.Events.Audio;

namespace Sushi.Net.Library.Script
{
    public class StreamCommands : BasicParser
    {
        public List<Decoding.Media> Medias { get; set; }
        public List<BaseCommand> Commands { get; set; }
        public float? SubtitleDelay { get; set;}
        public float? Duration { get; set;}

        public StreamCommands(LineCounter counter) : base(counter)
        {
            Medias = new List<Decoding.Media>();
            Commands = new List<BaseCommand>();
        }

        public StreamCommands(List<Decoding.Media> medias, List<ComputedMovement> movs)
        {
            Medias=medias;
            Commands = new List<BaseCommand>();

            foreach (ComputedMovement c in movs)
            {
                
                if (c.Difference < 0)
                    Commands.Add(new CutCommand { Time = ToRelative(c.AbsolutePosition), Duration = -c.Difference });
                else
                    Commands.Add(new FillCommand { Time = ToRelative(c.AbsolutePosition), Duration = c.Difference });
            }

        }
        
        private static (float start, float end, bool warn) FindInSilences(List<(float start, float end)> src_sils, float s, float e, float max)
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
       
        private float ToRelative(float val)
        {
            foreach (BaseCommand cmd in Commands)
                val += cmd.OrderDuration;
            return val;
        }


        public float ToAbsolute(BaseCommand cmd)
        {
            int idx = Commands.IndexOf(cmd);
            float time = cmd.Time;
            for (int i = 0; i < idx; i++)
                time -= Commands[i].OrderDuration;
            return time;
        }
        public float ToAbsoluteMove(CopyCommand cmd)
        {
            int idx = Commands.IndexOf(cmd);
            float time = cmd.MoveTime;
            for (int i = 0; i < idx; i++)
                time -= Commands[i].OrderDuration;
            return time;
        }

        public override Task<bool> ProcessAsync(string[] command)
        {
            string cmd = command[0].ToUpperInvariant();
            if (cmd == "S" || cmd=="F" || cmd=="I")
            {
                Lines.Count--;
                return Task.FromResult(true);
            }

            switch (cmd)
            {
                case "P":
                    if (command.Length < 2)
                        throw new ArgumentException("File Command should contains the duration and optionaly the subtitle delay.");
                    float subd = 0;
                    float.TryParse(command[1], out subd);
                    Duration = subd;
                    subd = 0;
                    if (command.Length >= 3)
                        float.TryParse(command[2], out subd);
                    SubtitleDelay=subd;
                    break;
                case "X":
                case "XA":
                    if (command.Length <= 2)
                        throw new ArgumentException("Cut Command should contain time and duration");
                    CutCommand cut = new CutCommand();
                    cut.Time = command[1].ParseTime();
                    cut.Duration = float.Parse(command[2]);
                    if (cmd == "XA")
                        cut.Duration = ToRelative(cut.Duration);
                    Commands.Add(cut);
                    break;
                case "Z":
                case "ZA":
                    if (command.Length <= 2)
                        throw new ArgumentException("Fill Command should contain time and duration");
                    FillCommand fill = new FillCommand();
                    fill.Time = command[1].ParseTime();
                    fill.Duration = float.Parse(command[2]);
                    if (cmd == "ZA")
                        fill.Duration = ToRelative(fill.Duration);
                    Commands.Add(fill);
                    break;
                case "C":  
                case "CA":
                    if (command.Length <= 3)
                        throw new ArgumentException("Copy Command should contain two times and a duration");
                    CopyCommand copy = new CopyCommand();
                    copy.Time = command[1].ParseTime();
                    copy.MoveTime = command[2].ParseTime();
                    copy.Duration = float.Parse(command[3]);
                    if (cmd == "CA")
                        copy.Duration = ToRelative(copy.Duration);
                    Commands.Add(copy);
                    break;

            }
            return Task.FromResult(false);
        }


        
        public override List<string> Serialize(bool absolute=false)
        {
            List<string> ls = new List<string>();
            List<Decoding.Media> indexed = Medias.Where(a => a.Info.Id != -1).OrderBy(a => a.Info.Id).ToList();
            List<Decoding.Media> subs = Medias.Where(a => a.Info.Id == -1).OrderBy(a => a.Info.Id).ToList();
            if (indexed.Count > 0)
                ls.Add($"I {string.Join(',', indexed.Select(a => a.Info.Id.ToString()))}");
            foreach (Decoding.Media m in subs)
                ls.Add($"S {m.ProcessPath.Quote()}");
            if (Duration.HasValue && Duration.Value > 0)
            {
                string strbase = $"P {Duration.Value.ToString(CultureInfo.InvariantCulture)}";
                if (SubtitleDelay.HasValue && SubtitleDelay.Value != 0)
                    strbase += $" {SubtitleDelay.Value.ToString(CultureInfo.InvariantCulture)}";
                ls.Add(strbase); }

            foreach (BaseCommand b in Commands)
            {
                switch (b)
                {
                    case CutCommand c:
                        ls.Add(absolute ? $"XA {ToAbsolute(c).FormatTime2()} {c.Duration.ToString(CultureInfo.InvariantCulture)}" : $"X {c.Time.FormatTime2()} {c.Duration.ToString(CultureInfo.InvariantCulture)}");
                        break;
                    case FillCommand f:
                        ls.Add(absolute ? $"ZA {ToAbsolute(f).FormatTime2()} {f.Duration.ToString(CultureInfo.InvariantCulture)}" : $"Z {f.Time.FormatTime2()} {f.Duration.ToString(CultureInfo.InvariantCulture)}");
                        break;
                    case CopyCommand cc:
                        ls.Add(absolute ? $"CA {ToAbsolute(cc).FormatTime2()} {ToAbsoluteMove(cc).FormatTime2()} {cc.Duration.ToString(CultureInfo.InvariantCulture)}" : $"C {cc.Time.FormatTime2()} {cc.MoveTime.FormatTime2()} {cc.Duration.ToString(CultureInfo.InvariantCulture)}");
                        break;
                }

            }
            return ls;
        }
    }
}