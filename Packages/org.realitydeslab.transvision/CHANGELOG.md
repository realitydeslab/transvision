# Changelog

## [0.1.0] - 2026-10-12

App Store version 0.1.0 (build 1).

### Changed
- Unity 6000.3 (Unity 6.3 LTS), URP 17.3, AR Foundation 6.4.3, HoloKit Unity SDK 0.6.0 (tag `0.6.0-preview.1`).
- Minimum iOS version is 27.0.
- The app moved from `Assets/` into this embedded package; `Assets/` keeps only project configuration.
- The red effect runs as a URP Render Graph pass on the AR camera.
- New default effect, Liquid: red regions spread (nocebo) or shrink (placebo) and flow with ripples, bubbles and a rim; everything outside the red keeps only a small still warp. The previous effect stays available as Classic.
- Mono shows the result full screen.
- Stereo shows the whole camera image on a plane 1 m in front of the eyes, matched to the camera's intrinsics and the HoloKit eye offset.
- Mono has the HoloKit SDK's Record, HD60/4K30 and render-mode buttons with microphone audio, as in EchoVision; Stereo hides the SDK's button bar.

### Fixed
- The camera picture was shown in the wrong orientation on iOS 27: the effect now uses the display transform the AR background is drawn with in the same frame.
- Recordings match what the screen shows, in Mono and Stereo.

### Performance
- The mask blur samples at most 7 x 7 taps instead of every texel of its window.
- SSAO is off in the URP renderers.
