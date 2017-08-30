# TransparentPaint

[Telestrator][1] (video marker) designed for [OBS][2]. 

TransparentPaint accepts stylus(pen)/touch/mouse drawing on a transparent canvas, snapped to a particular window (e.g. a OBS source projector). 
The drawing output is available for HTTP streaming and can be included by OBS BrowserSource. The output can also be displayed on a chroma-keyed window.

# Installation

Just copy, unzip and run.

.NET Framework 4.6.1 must be installed first.

# Usage

## Test

1. Launch `TransparentPaint`
   * You don't have to allow for firewall access, because we are accessing it locally
3. Open http://127.0.0.1:8010 with Chrome (Only tested on Chrome)
4. Use mouse/stylus/touch to draw something on `TransparentPaint`
5. Some strokes should appear on the chrome.

## Use with OBS

1. Launch `OBS`
2. Stream the strokes to OBS
   1. Create a new BrowserSource
      * Untick local file, URL: http://127.0.0.1:8010
3. To overlay the `TransparentPaint` over the video
   1. Right click on your video source, then select `Windowed Projector`
   2. In the `TransparentPaint`, in the text box near the padlock icon, type `Projector`
   3. Check the padlock, or F6
   4. The `TransparentPaint` should snap to the your source projector.
4. Now you can draw with mouse/stylus/touch

# Others

* Hotkeys
   * F1: Clear canvas
   * F2: Undo
   * F6: Snap toggle
* The `Zoom` button creates a cloned window with Green chroma key, which might be useful in some sistuation.
* Config and logs are saved at `%LOCALAPPDATA%\Hellosam.Net.TransparentPaint`
* Tested with Windows 10 + OBS 20.0.1

# License

The source code is released under the MIT License.

## Distribution

The binaries include the following libraries, and their licensing terms are:

* [Microsoft.Tpl.Dataflow](https://www.nuget.org/packages/Microsoft.Tpl.Dataflow/), 
  [Microsoft .Net License](https://www.microsoft.com/net/dotnet_library_license.htm)
* [log4net](https://logging.apache.org/log4net/), [Apache License Version 2](https://logging.apache.org/log4net/license.html)
* [MvvmLight](https://mvvmlight.codeplex.com/), [MIT](https://mvvmlight.codeplex.com/license)


# Known Bugs

* The HTTP streamed output does not include ink currently drawing by stylus/touch.
  
  I have described this in the [Stackoverflow][10], if someone knows the answer please let me know.
  * The ink will appear only after the stylus or finger is up.
  * mouse is fine though. 

[1]: https://en.wikipedia.org/wiki/Telestrator
[2]: https://obsproject.com/
[10]: https://stackoverflow.com/questions/45963928/creating-bitmap-of-inkcanvas-that-includes-stroke-from-dynamicrenderer-drawn-by
