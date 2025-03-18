using System.Diagnostics.CodeAnalysis;
using System.IO;
using Sushi.Net.Library.Audio;
using Sushi.Net.Library.CommandLine;
using Sushi.Net.Library.Events;

namespace Sushi.Net.Library.Settings
{
    [CommandLineProgram("Automatic Subtitle & Audio Shifter")]
    public class SushiSettings
    {
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SushiSettings))]
        public SushiSettings() { }

        [CommandLine("--algo",AlgoType.Subtitle, "Algorithm Type", "audio, subtitle",false,false,"-al")]
        public AlgoType Algo { get; set; }
        [CommandLine("--mode",null, "Match Mode [Defaults, Sub: SqDiffNormed, Audio: CCoeffNormed]", "SqDiffNormed, CCoeffNormed, CCorrNormed, SQDiff, CCoeff, CCorr",false,false,"-m")]
        public Mode? Mode { get; set; }
        [CommandLine("--action",ActionType.Shift, "Action to perform","shift, export (export script), script (execute script)",false,false,"-a")]
        public ActionType Action { get; set; }
        [CommandLine("--voice-removal",false, "Apply voice removal")]
        public bool VoiceRemoval { get; set; }
        [CommandLine("--downmix-stereo",false, "Downmix only stereo channels")]
        public bool DownMixStereo { get; set; }
        [CommandLine("--output-audio-codec",null, "Output Audio Codec [Only for audio Algo]")]
        public string OutputAudioCodec { get; set; }
        [CommandLine("--output-audio-params",null, "Output Audio Parameters [Only for audio Algo]")]
        public string OutputAudioParams { get; set; }
        [CommandLine("--normalize",true,"Apply volume normalization")]
        public bool Normalize { get; set; }
        [CommandLine("--type",Types.All,"Process All Audio & Subtitles [Only for audio Algo]","all, subtitles, audios",false, false,"-t")]
        public Types Type { get; set; }
        [CommandLine("--window", 10, "Search window size. [Only for subtitle Algo]", "seconds")]
        public int Window { get; set; }
        [CommandLine("--max-window", 30, "Maximum search size allowed to use when trying to recover from errors. [Only for subtitle Algo]", "seconds")]
        public int MaxWindow { get; set; }
        [CommandLine("--rewind-thresh", 5, "Number of consecutive errors to encounter to consider results broken and retry with larger window. Set to 0 to disable. [Only for subtitle Algo]","events")]
        public int RewindThresh { get; set; }
        [CommandLine("--no-grouping", false, "Don't events into groups before shifting. Also disables error recovery. [Only for subtitle Algo]")]
        public bool NoGrouping { get; set; }
        [CommandLine("--no-chapters", false, "Do not use any chapters found. [Only for subtitle Algo]")]
        public bool NoChapters { get; set; }
        [CommandLine("--silence-threshold", -40, "Threshold when searching for silences. [Only for audio Algo]","db")]
        public int SilenceThreshold { get; set; }
        [CommandLine("--silence-min-length", (float)0.3, "Minimal length of the silence. [Only for audio Algo]","seconds")]
        public float SilenceMinLength { get; set; }
        [CommandLine("--silence-assign-threshold", 2f, "Silence re-assign threshold on the matching audio files. [Only for audio Algo]","seconds")]
        public float SilenceAssignThreshold { get; set; }
        [CommandLine("--max-kf-distance", 2f, "Maximum keyframe snapping distance. [Only for subtitle Algo]", "frames")]
        public float MaxKFDistance { get; set; }
        [CommandLine("--kf-mode", KFMode.All, "Keyframes-based shift correction/snapping mode. [Only for subtitle Algo]", "All, Shift, Snap")]
        public KFMode KfMode { get; set; }

        [CommandLine("--resize-subtitles", true, "Resize subtitles to target video, when it exist.")]
        public bool ResizeSubtitles { get; set; }
        [CommandLine("--subtitles-dimensions", null, "Destination subtitles dimensions when video doesn't exists, or you want custom dimensions.","WidthxHeight")]
        public string SubtitleDimensions { get; set; }
        [CommandLine("--resize-borders", false, "When resizing subtitles also resize subtitle borders.")]
        public bool ResizeBorders { get; set; }
        
        [CommandLine("--smooth-radius", 3, "Radius of smoothing median filter. [Only for subtitle Algo]", "events")]
        public int SmoothRadius { get; set; }

        [CommandLine("--max-ts-duration", (float) (1001.0 / 24000.0 * 10), "Maximum duration of a line to be considered typesetting. [Only for subtitle Algo]", "seconds")]
        public float MaxTsDuration { get; set; }

        [CommandLine("--max-ts-distance", (float) (1001.0 / 24000.0 * 10), "Maximum distance between two adjacent typesetting lines to be merged. [Only for subtitle Algo]", "seconds")]
        public float MaxTsDistance { get; set; }

        [CommandLine("--sample-type", SampleType.Float32, "Sample Type.", "UInt8, Float32")]
        public SampleType SampleType { get; set; }

        [CommandLine("--sample-rate", 12000, "Downsampled audio sample rate.", "rate")]
        public int SampleRate { get; set; }

        [CommandLine("--padding", 10, "Add padding to the borders. [Only for subtitle Algo]", "seconds")]
        public int Padding { get; set; }

        [CommandLine("--allowed-difference", 0.01f, "Max allowed differences between audio streams. [Only for subtitle Algo]", "seconds")]
        public float AllowedDifference { get; set; }
        [CommandLine("--audio-allowed-difference", 0.05f, "Max allowed differences between audio streams (audios). [Only for audio Algo]", "seconds")]
        public float AudioAllowedDifference { get; set; }

        [CommandLine("--max-group-std", 0.025f, "Max subtitle group standard deviation. [Only for subtitle Algo]", "deviation")]
        public float MaxGroupStd { get; set; }

        [CommandLine("--src-audio", null, "Audio stream index of the source video.", "id")]
        public int? SrcAudio { get; set; }

        [CommandLine("--src-subtitle", null, "Subtitle stream index of the source video", "id", false, false, "--src-script")]
        public int? SrcSubtitle { get; set; }

        [CommandLine("--dst-audio", null, "Audio stream index of the destination video", "id")]
        public int? DstAudio { get; set; }

        [CommandLine("--no-cleanup", false, "Don't delete demuxed streams.")]
        public bool NoCleanup { get; set; }

        [CommandLine("--temp-dir", null, "Specify temporary folder to use when demuxing stream.", "directory")]
        public string TempDir { get; set; }

        [CommandLine("--chapters", null, "XML or OGM chapters to use instead of any found in the source. [Only for subtitle Algo]", "filename")]
        public string Chapters { get; set; }

        [CommandLine("--subtitle", null, "Subtitle/s to use instead (can be used multiple times).", "filenames", false, true,"--script","--subtitles")]
        public string[] Subtitle { get; set; }

        [CommandLine("--dst-keyframes", null, "Destination keyframes file [Only for subtitle Algo]", "filename")]
        public string DstKeyframes { get; set; }

        [CommandLine("--src-keyframes", null, "Source keyframes file [Only for subtitle Algo]", "filename")]
        public string SrcKeyframes { get; set; }

        [CommandLine("--make-dst-keyframes", false, "Make destination keyframes [Only for subtitle Algo]")]
        public bool MakeDstKeyframes { get; set; }

        [CommandLine("--make-src-keyframes", false, "Make Source keyframes file [Only for subtitle Algo]")]
        public bool MakeSrcKeyframes { get; set; }

        [CommandLine("--dst-fps", null, "FPS of the destination video. Must be provided if keyframes are used. [Only for subtitle Algo]", "fps")]
        public float? DstFPS { get; set; }

        [CommandLine("--src-fps", null, "FPS of the source video. Must be provided if keyframes are used. [Only for subtitle Algo]", "fps")]
        public float? SrcFPS { get; set; }

        [CommandLine("--src-timecodes", null, "Timecodes file to use instead of making one from the source (when possible) [Only for subtitle Algo]", "filename")]
        public string SrcTimecodes { get; set; }

        [CommandLine("--dst-timecodes", null, "Timecodes file to use instead of making one from the destination (when possible) [Only for subtitle Algo]", "filename")]
        public string DstTimecodes { get; set; }

        [CommandLine("--src", null, "Source audio/video", "filename")]
        public string Src { get; set; }

        [CommandLine("--dst", null, "Destination audio/video", "filename")]
        public string Dst { get; set; }

        [CommandLine("--output", null, "Output path.", "directory", false, false, "-o")]
        public string Output { get; set; }
        [CommandLine("--use-destinations-names", true, "Use destination source name for output files.")]

        public bool UseDestinationNames { get; set; }
        [CommandLine("--script-file", null, "Script File. [Only for audio Algo]", "filename", false, false, "-s")]
        public string ScriptFile { get; set; }
        [CommandLine("--absolute-times", false, "Script will have absolute times [Only for audio Algo]")]
        public bool AbsoluteTimes { get; set; }


        [CommandLine("--subtitle-streams", null, "Only use this subtitle stream to calculate audio blocks [Only for subtitle Algo]",null, false, false)]
        public string SubtitleStreams { get; set; }
        [CommandLine("--verbose", false, "Enable verbose logging.", null, false, false, "-v")]
        public bool Verbose { get; set; }
        [CommandLine("--verbose-verbose", false, "Enable more verbose logging.", null, false, false, "-vv")]
        public bool VerboseVerbose { get; set; }
        
        [CommandLine("--sub-time",0,"Add/Subtract time to the output subtitles","milliseconds",false,false)]
        public float SubTime { get; set; }
        [CommandLine("--dry-run", false, "Run the program without output.")]
        public bool DryRun { get; set; }

        [CommandLine("--only-extract", false,"Only extract subtitles")]
        public bool OnlyExtract { get; set; }

        [CommandLine("--src-multi-sync", 0, "Re-Sync multiple source audio sources against the main source, seconds to scan","seconds")]
        public int SrcMultiSync { get; set; }
        [CommandLine("--minimal-audio-shift",15, "Minimal Audio shift to execute an audio shifting (only when is only one block)","milliseconds")]
        public float MinimalAudioShift { get; set; }
    }
}
