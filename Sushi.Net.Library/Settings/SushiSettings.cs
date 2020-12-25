using System.IO;
using Sushi.Net.Library.Audio;
using Sushi.Net.Library.CommandLine;
using Sushi.Net.Library.Events;

namespace Sushi.Net.Library.Settings
{
    [CommandLineProgram("Automatic Subtitle & Audio Shifter")]
    public class SushiSettings
    {
        [CommandLine("--type","subtitle", "Type of stream to shift.", "audio, subtitle",false,false,"-t")]
        public string Type { get; set; }
        
        [CommandLine("--window", 10, "Search window size.", "seconds")]
        public int Window { get; set; }

        [CommandLine("--max-window", 30, "Maximum search size allowed to use when trying to recover from errors.", "seconds")]
        public int MaxWindow { get; set; }

        [CommandLine("--rewind-thresh", 5, "Number of consecutive errors to encounter to consider results broken and retry with larger window. Set to 0 to disable.","events")]
        public int RewindThresh { get; set; }

        [CommandLine("--no-grouping", false, "Don't events into groups before shifting. Also disables error recovery.")]
        public bool NoGrouping { get; set; }
        [CommandLine("--no-chapters", false, "Do not use any chapters found.")]
        public bool NoChapters { get; set; }
        
        [CommandLine("--silence-threshold", -50, "Threshold when searching for silences.","db")]
        public int SilenceThreshold { get; set; }
        [CommandLine("--silence-min-length", (float)0.5, "Minimal length of the silence.","seconds")]
        public float SilenceMinLength { get; set; }
        [CommandLine("--max-kf-distance", 2f, "Maximum keyframe snapping distance.", "frames")]
        public float MaxKFDistance { get; set; }
        [CommandLine("--kf-mode", KFMode.All, "Keyframes-based shift correction/snapping mode.", "All, Shift, Snap")]
        public KFMode KfMode { get; set; }

        [CommandLine("--resize-subtitles", true, "Resize subtitles to target video, when it exist.")]
        public bool ResizeSubtitles { get; set; }
        [CommandLine("--subtitles-dimensions", null, "Destination subtitles dimensions when video doesn't exists, or you want custom dimensions.","WidthxHeight")]
        public string SubtitleDimensions { get; set; }
        [CommandLine("--resize-borders", false, "When resizing subtitles also resize subtitle borders.")]
        public bool ResizeBorders { get; set; }
        
        [CommandLine("--smooth-radius", 3, "Radius of smoothing median filter.", "events")]
        public int SmoothRadius { get; set; }

        [CommandLine("--max-ts-duration", (float) (1001.0 / 24000.0 * 10), "Maximum duration of a line to be considered typesetting.", "seconds")]
        public float MaxTsDuration { get; set; }

        [CommandLine("--max-ts-distance", (float) (1001.0 / 24000.0 * 10), "Maximum distance between two adjacent typesetting lines to be merged.", "seconds")]
        public float MaxTsDistance { get; set; }

        [CommandLine("--sample-type", SampleType.Float32, "Sample Type.", "UInt8, Float32")]
        public SampleType SampleType { get; set; }

        [CommandLine("--sample-rate", 12000, "Downsampled audio sample rate.", "rate")]
        public int SampleRate { get; set; }

        [CommandLine("--padding", 10, "Add padding to the borders.", "seconds")]
        public int Padding { get; set; }

        [CommandLine("--allowed-difference", 0.01f, "Max allowed differences between audio streams (subtitles).", "seconds")]
        public float AllowedDifference { get; set; }
        [CommandLine("--audio-allowed-difference", 0.05f, "Max allowed differences between audio streams (audios).", "seconds")]
        public float AudioAllowedDifference { get; set; }

        [CommandLine("--max-group-std", 0.025f, "Max subtitle group standard deviation.", "deviation")]
        public float MaxGroupStd { get; set; }

        [CommandLine("--src-audio", null, "Audio stream index of the source video.", "id")]
        public int? SrcAudioIndex { get; set; }

        [CommandLine("--src-subtitle", null, "Subtitle stream index of the source video (pass -1 to process all subtitles)", "id", false, false, "--src-script")]
        public int? SrcSubtitleIndex { get; set; }

        [CommandLine("--dst-audio", null, "Audio stream index of the destination video", "id")]
        public int? DstAudioIndex { get; set; }

        [CommandLine("--no-cleanup", false, "Don't delete demuxed streams.")]
        public bool NoCleanup { get; set; }

        [CommandLine("--temp-dir", null, "Specify temporary folder to use when demuxing stream.", "directory")]
        public string TempDir { get; set; }

        [CommandLine("--chapters", null, "XML or OGM chapters to use instead of any found in the source.", "filename")]
        public string Chapters { get; set; }

        [CommandLine("--subtitle", null, "Subtitle/s to use instead (can be used multiple times).", "filenames", false, true,"--script","--subtitles")]
        public string[] Subtitle { get; set; }

        [CommandLine("--dst-keyframes", null, "Destination keyframes file", "filename")]
        public string DstKeyframes { get; set; }

        [CommandLine("--src-keyframes", null, "Source keyframes file", "filename")]
        public string SrcKeyframes { get; set; }

        [CommandLine("--make-dst-keyframes", false, "Make destination keyframes")]
        public bool MakeDstKeyframes { get; set; }

        [CommandLine("--make-src-keyframes", false, "Make Source keyframes file")]
        public bool MakeSrcKeyframes { get; set; }

        [CommandLine("--dst-fps", null, "FPS of the destination video. Must be provided if keyframes are used.", "fps")]
        public float? DstFPS { get; set; }

        [CommandLine("--src-fps", null, "FPS of the source video. Must be provided if keyframes are used.", "fps")]
        public float? SrcFPS { get; set; }

        [CommandLine("--src-timecodes", null, "Timecodes file to use instead of making one from the source (when possible)", "filename")]
        public string SrcTimecodes { get; set; }

        [CommandLine("--dst-timecodes", null, "Timecodes file to use instead of making one from the destination (when possible)", "filename")]
        public string DstTimecodes { get; set; }

        [CommandLine("--src", null, "Source audio/video", "filename", true)]
        public string Src { get; set; }

        [CommandLine("--dst", null, "Destination audio/video", "filename", true)]
        public string Dst { get; set; }

        [CommandLine("--output", null, "Output path.", "directory", false, false, "-o")]
        public string Output { get; set; }

        [CommandLine("--verbose", false, "Enable verbose logging.", null, false, false, "-v")]
        public bool Verbose { get; set; }
        [CommandLine("--verbose-verbose", false, "Enable more verbose logging.", null, false, false, "-vv")]
        public bool VerboseVerbose { get; set; }
        
        [CommandLine("--dry-run", false, "Run the program without output.")]
        public bool DryRun { get; set; }

    }
}
