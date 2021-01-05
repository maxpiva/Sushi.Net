## v0.9.2 Beta

This work is based on [Sushi](https://github.com/tp7/Sushi) and the command syntax is pretty similar, the only real difference is that the output parameter is a path, not a subtitle. 
I heavy recommend following the original Sushi [Wiki](https://github.com/tp7/Sushi/wiki) before using this program.

## Additionaly Sushi.Net supports:

* Automatic Audio Shifting from from different audio sources with same content.
* Automatic Audio Shifting from different audio languages.
* Multiple Subtitles
* Support Resizing the subtitles (ass,ssa) to the destination video
* Third-party embedding with a standalone library

## Downloads

Windows x86, x64, Ubuntu x64 & OSX x64 binaries can be downloaded from [releases](https://github.com/maxpiva/Sushi.Net/releases).

## Build & Third Party Usage

**Sushi.Net** uses .NET 5 and is a wrapper around **Sushi.Net.Library**.

Binaries builds are published with .NET 5 self-contained and trimmed, any native libraries (Open CV) are included in the build.

**Sushi.Net.Library** can be used in other programs, and it will be uploaded in nuget.org when **Sushi.Net** becomes final.

## How Audio Shifting Works.

Basically, **Sushi.net** will normalize the destination audio stream, then find the silences in the stream, and mark the destination stream into chunks. After that, it will apply a band filter to attenuate the vocals and load both streams into memory. Those chunks will be matched against the source stream. Then the source stream will be reconstructed matching the destination stream chunks positions and saved.

Of course, it can also shift subtitles in the same way original [Sushi](https://github.com/tp7/Sushi) does, and it supports multiple external subtitles as input (space separated) or it can get the subtitles from the --src stream if its a container. (mkv, mp4, etc).

## Example Usage

Being english.mkv and japanese.mkv to different releases from the same show/movie.

```sushi.net --type audio --src english.mkv --dst japanese.mkv```

Will match and English audio stream against a Japanese one, the result will a new English stream that is shifted/synced for the Japanese one.

Being english_tv.mkv and english_dvd.mkv diffrent releases from the same show/movie.

```sushi.net --type audio --src english_tv.mkv --dst english_dvd.mkv```

Will match and English audio tv stream against a dvd one, the result will a new English TV stream that is shifted/synced for the DVD one.

## Requirements

[FFMpeg](http://www.ffmpeg.org/download.html) For Audio Extraction & Manipulation.

## Optional Requirements

[MKVExtract](http://www.bunkus.org/videotools/mkvtoolnix/downloads.html) for timecode extraction.

SCXvid for keyframe creation. [Windows](https://github.com/soyokaze/SCXvid-standalone/releases) Version. [Linux](https://eyalmazuz.github.io/Linux_Keyframes/) Version. [Mac](https://eyalmazuz.github.io/Linux_Keyframes/) Version. (Follow the Linux Guide replacing apt with [brew](https://brew.sh/))

## Sushi.Net internally uses the following third party libraries.

[CliWrap](https://github.com/Tyrrrz/CliWrap) - Amazing and Simple way to spawn and manage executables.

[NAudio](https://github.com/naudio/NAudio) - It manages all our wave needs.

[OpenCvSharp](https://github.com/shimat/opencvsharp) - Gives us the avenue to use Open CV.

[OpenCV](https://opencv.org/) - Matches the audio streams.

[Thinktecture.Logging.Configuration](https://github.com/PawelGerr/Thinktecture.Logging.Configuration) - Enables us to change the loglevel on the fly.

## Notice

I used a custom build native version of OpenCV-OpenCVSharp, removing all the parts we don't use (Open CV compiled only with core and imgproc modules), reducing the executable in near 80 Mbytes. 

There are some retouched scripts and code from the original [OpenCvSharp](https://github.com/shimat/opencvsharp) distribution in the Extras directories, if you want to create the smaller native library version for other linux distributions or maybe the new M1 Macs. Just make sure, you make a pull request ;)

## Future

* Currently the only native dependency is Open CV, and only uses MatchTemplate from it, I'm up to change to a .net replacement in the future.
* Current matcher is pretty accurate for the use case, when the sources have good quality. When the quality degrades, is not that good. So i want to explore some ideas, and see how it goes, like applying FFT to the sources, and audio clipping on the middle ranges, fingerprint analysis, and possible other matchers.
* Better parameters sweet spot, and better vocal filtering.

## History

**v0.9.2**
* Fixed audio best match bug.
* Added the possibility of selecting the matching algorithm from the command line.
* Obliterated more Bugs.

**v0.9.1**
* Squash Bugs.

**v0.9.0**
* First Version

