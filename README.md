## v0.9 Beta

This work is based on [Sushi](https://github.com/tp7/Sushi) and the command syntax is pretty similar, the only real difference is that the output parameter is a path, not a subtitle. 
I heavy recommend following the original Sushi [Wiki](https://github.com/tp7/Sushi/wiki) before using this program.

## Additionaly Sushi.Net supports:

* Automatic Audio Shifting from different audio languages.
* Multiple Subtitles
* Support Resizing the subtitles (ass,ssa) to the destination video
* Third-party embedding with a standalone library

## Downloads

Windows x86, x64, Ubuntu x64 & OSX x64 binaries can be downloaded from releases.

## Build & Third Party Usage

**Sushi.Net** uses .NET 5 and is a wrapper around **Sushi.Net.Library**.

Binaries builds are published with .NET 5 self-contained and trimmed, any native libraries (Open CV) are included in the build.

**Sushi.Net.Library** can be used in other programs, and it will be uploaded in nuget.org when **Sushi.Net** becomes final.

## How Audio Shifting Works.

Basically, **Sushi.net** will normalize the destination audio stream, then find the silences in the stream, and mark the destination stream into chunks. After that, it will apply a band filter to attenuate the vocals and load both streams into memory. Those chunks will be matched against the source stream. Then the source stream will be reconstructed matching the destination stream chunks positions and saved.

Of course, it can also shift subtitles in the same way original [Sushi](https://github.com/tp7/Sushi) does, and it supports multiple external subtitles as input (space separated) or it can get the subtitles from the --src stream if its a container. (mkv, mp4, etc).

## Example Usage

Beign english.mkv and japanese.mkv to different releases from the same show/movie.

```sushi.net --type audio --src english.mkv --dst japanese.mkv```

Will match and english audio stream against a japanese one, the result will a new english stream that is shifted/synced for the japanese one.

## Requirements

[FFMpeg](http://www.ffmpeg.org/download.html) For Audio Extraction & Manipulation.

## Optional Requirements

[MKVExtract](http://www.bunkus.org/videotools/mkvtoolnix/downloads.html) for timecode extraction.

SCXvid for keyframe creation. [Windows](https://github.com/soyokaze/SCXvid-standalone/releases) Version. [Linux](https://eyalmazuz.github.io/Linux_Keyframes/) Version. [Mac](https://eyalmazuz.github.io/Linux_Keyframes/) Version. (Follow the Linux Guide replacing apt with [brew](https://brew.sh/))

## Sushi.Net internally uses the following third party libraries.

[CliWrap](https://github.com/Tyrrrz/CliWrap) - Amazing and Simple way to spawn and manage executables.

[NAudio](https://github.com/naudio/NAudio) - It manages all our wave needs.

[NumPyDotNet](https://github.com/Quansight-Labs/numpy.net) - Is in charge of doing the calculations.

[OpenCvSharp](https://github.com/shimat/opencvsharp) - Gives us the avenue to use Open CV.

[OpenCV](https://opencv.org/) - Matches the audio streams.

[Thinktecture.Logging.Configuration](https://github.com/PawelGerr/Thinktecture.Logging.Configuration) - Enables us to change the loglevel on the fly.

## Notice

We used a custom build native version of OpenCV-OpenCVSharp, removing all the parts we don't use (Open CV compiled only with core and imgproc modules), reducing the executable in near 80 Mbytes. 

There are some retouched scripts and code from the original [OpenCvSharp](https://github.com/shimat/opencvsharp) distribution in the Extras directories, if you want to create the smaller native library version for other linux distributions or maybe the new M1 Macs. Just make sure, you make a pull request ;)

## Future

* Currently the only native dependency is Open CV, and we only use MatchTemplate from it, I'm up to change to a .net replacement in the future.
* Remove NumPy dependency, and internalize required math methods.
* Revamp the whole array management using ArrayPools and Spans.
* Better matcher, better parameters sweet spot, and better vocal filtering.

