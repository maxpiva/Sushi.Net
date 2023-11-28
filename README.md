## v1.0.0 Still Beta :O

This work is based on [Sushi](https://github.com/tp7/Sushi) and the command syntax is pretty similar, the only real difference is that the output parameter is a path, not a subtitle. 
I highly recommend following the original Sushi [Wiki](https://github.com/tp7/Sushi/wiki) before using this program.

## Additionally Sushi.Net supports:

* Automatic Audio Shifting from different audio sources with the same content.
* Automatic Audio Shifting from different audio languages.
* Multiple Subtitles
* Support Resizing the subtitles (ass,ssa) to the destination video
* Third-party embedding with a standalone library

## Downloads

Windows x64, Ubuntu x64 & OSX x64 binaries can be downloaded from [releases](https://github.com/maxpiva/Sushi.Net/releases).

## Build & Third Party Usage

**Sushi.Net** uses .NET 8 and is a wrapper around **Sushi.Net.Library**.

Binaries builds are published with .NET 8 self-contained and trimmed, any native libraries (Open CV) are included in the build.

**Sushi.Net.Library** can be used in other programs, and it will be uploaded to nuget.org when **Sushi.Net** becomes final.

## How Audio Shifting Works.

Basically, **Sushi.net** will normalize the destination audio stream, then find the silences in the stream, and mark the destination stream into chunks. After that, it will apply a band filter to attenuate the vocals and load both streams into memory. Those chunks will be matched against the source stream. Then the source stream will be reconstructed matching the destination stream chunks positions and saved.

Of course, it can also shift subtitles in the same way original [Sushi](https://github.com/tp7/Sushi) does, and it supports multiple external subtitles as input (space separated) or it can get the subtitles from the --src stream if its a container. (mkv, mp4, etc).

## Example Usage

Being english.mkv and japanese.mkv to different releases from the same show/movie.

```sushi.net --type audio --src english.mkv --dst japanese.mkv```

It will match an English audio stream against a Japanese one, and the result will be a new English stream that is shifted/synced for the Japanese one.

english_tv.mkv and english_dvd.mkv different releases from the same show/movie.

```sushi.net --type audio --src english_tv.mkv --dst english_dvd.mkv```

It will match an English audio TV stream against a DVD one, and the result will be a new English TV stream that is shifted/synced for the DVD one.

## Scripting [NEW for v1.0]

By default, sushi will **shift** the audio and or subtitles. But an action **-a** or **-action** new command line exist
* shift [Shift Audio]
* export [Save the Shift movements, into a script file that can be edited in plain text, command usage inside the script file.]
* script [Execute the script, instead analyze the sources]

The script file created shows the inserts and the cuts required to convert the source file into the destination file.
This is useful when you have a bunch of files, where the cuts/inserts are the same, or to fine-tune a conversion.


## Requirements

[FFMpeg](http://www.ffmpeg.org/download.html) & FFProbe For Information, Audio Extraction & Manipulation.

## Optional Requirements

[MKVExtract](http://www.bunkus.org/videotools/mkvtoolnix/downloads.html) for timecode extraction.

SCXvid for keyframe creation. [Windows](https://github.com/soyokaze/SCXvid-standalone/releases) Version. [Linux](https://eyalmazuz.github.io/Linux_Keyframes/) Version. [Mac](https://eyalmazuz.github.io/Linux_Keyframes/) Version. (Follow the Linux Guide replacing apt with [brew](https://brew.sh/))

## Sushi.Net internally uses the following third-party libraries.

[CliWrap](https://github.com/Tyrrrz/CliWrap) - Amazing and Simple way to spawn and manage executables.

[NAudio](https://github.com/naudio/NAudio) - It manages all our wave needs.

[OpenCvSharp](https://github.com/shimat/opencvsharp) - Gives us the avenue to use Open CV.

[OpenCV](https://opencv.org/) - Matches the audio streams.

[Thinktecture.Logging.Configuration](https://github.com/PawelGerr/Thinktecture.Logging.Configuration) - This enables us to change the log level on the fly.

## Notice

I used a custom build native version of OpenCV-OpenCVSharp, removing all the parts we don't use (Open CV compiled only with core and imgproc modules), reducing the executable in nearly 80 Mbytes. 

There are some retouched scripts and code from the original [OpenCvSharp](https://github.com/shimat/opencvsharp) distribution in the Extras directories, if you want to create the smaller native library version for other Linux distributions or maybe the new M1 Macs. Just make sure, you make a pull request ;)

## Future

* Currently the only native dependency is Open CV, and only uses MatchTemplate from it, I'm up to change to a .Net replacement in the future.
* Current matcher is pretty accurate for the use case when the sources have good quality. When the quality degrades, is not that good. So I want to explore some ideas and see how it goes, like applying FFT to the sources, and audio clipping on the middle ranges, fingerprint analysis, and possibly other matchers.
* Better parameters sweet spot, and better vocal filtering.

## TV Sources Tidbits

* If your source is TV-based, some TV channels heavily chop endings, beginnings, credits, scene changes, and scenes with silence, so they can put more advertising per show, they can chop up to 1-3 minutes, for an hour show. Because of that, it's recommended you mute the advertising (not removing it), and increase the window so matching can be better.

## History

**v1.0.0.**
* Too many changes to keep track of them :O

**v0.9.2**
* Fixed audio best match bug.
* Added the possibility of selecting the matching algorithm from the command line.
* Obliterated more Bugs.

**v0.9.1**
* Squash Bugs.

**v0.9.0**
* First Version

