# GodAmp

A reimplementation of Winamp 2.9 in Godot, with full cross-platform support.

I made this as a [submission](https://neptune-skies.itch.io/godamp) for the [Tool Jam 5](https://itch.io/jam/the-tool-jam-5), since I've always wanted to make a music player with visualizers.

There is a plan to eventually turn this into its own fully customizable player that brings the old internet feel, but built with modern tools and able to run on any platform.

![readme](https://github.com/user-attachments/assets/daf7fb55-8723-4bc4-af56-cc560df16008)

*DISCLAIMER: This is a free, non-commercial project made for fun and will remain being one.  All rights to the original Winamp skins, themes, assets, and sample songs belong to their respective owners. This project does not claim ownership over them.*

### Credits
App Icon by my [beloved](https://www.instagram.com/stars.in.a.box/).  

Songs by:
1. [Leader - Step Down](https://www.youtube.com/channel/UCDm2LMZIJjy63w-mHMely1Q)
2. [Oceans Divide - Lipstick Lies](http://oceansdivide.com/)
3. [Fall of Envy - Wondering](https://www.reverbnation.com/fallofenvy) 
4. [AMRPHIS - Metalcore](https://youtu.be/WhU461oCmD8?si=nSzPaObakaQ9qYIr)

As far as I know, they are royalty/copyright-free, but please do let me know if not, and I'll take them down.


## Features
### Music player
1. Basic functionalities: Play, Pause, Stop, Previous & Next track.
2. Playlist functionality:
   - Toggle Shuffle & Repeat (playlists)
   - Adjust volume or ear balancing on top sliders 
   - Click the "Load" (up arrow) button to load your own tracks (supports mp3, wav, ogg). It will load into a new playlist.
3. Double click any track on the playlist to start playing it.
4. **Click "2x" to scale the UI**

### Equalizer
1. Amplifier (PreAmp)
2. Fully working EQ10, just like in Winamp. Can be disabled by the toggle.

### Visualizer
1. **Waveform Lines** - Rotates in the direction of the dominant frequency. Try songs with lots of different beats.
2. **Racing Squares** - The stronger the frequencies, the stronger they are knocked. Some other attributes will cause special warping effects.
3. More to be added...

## Roadmap
- [X] Add support for other file types.
  - MP3, OGG, WAV
- [X] Actual window
- [X] Functional playlists
- [X] Multiple windows
- [ ] Full theme support
- [ ] Working frequency spline panels
- [ ] More visualizers (and flexibility for them)
- [ ] Integration with special keys
