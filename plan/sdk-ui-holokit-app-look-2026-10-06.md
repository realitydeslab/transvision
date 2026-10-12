# HoloKit SDK UI: match holokit-app's look (font + layout)

Date: 2026-10-06. Request: "for the SDK ... the font should look almost the same looking and layout as ../holokit-app".
Repo changed: `../holokit-unity-sdk` (has uncommitted WIP from session holokit-unity-sdk-8a in the stereo bar files; build on it, never revert).

## Findings
- SDK Stereo bar (`Runtime/UI/HoloKitStereoBar*.cs`): legacy UGUI `Text`, `LegacyRuntime.ttf` (Arial) with synthetic BoldAndItalic, labels 2 mm (= 36 px at 460 dpi).
- SDK Mono UI (`Assets/Prefabs/HoloKit Default UI Canvas.prefab`): white boxes "● REC" / "Stereo", Arial.
- holokit-app Star AR Panel (Star Canvas, ConstantPixelSize 1, authored at 2532x1170): labels MORE/RECORD/EXIT/TRIGGER/BOOST in Object Sans **BoldSlanted** 36; values (60fps, 00:00, 4KHDR) Object Sans **Regular** 36; accent #FF5B1A (same as SDK).
- holokit-app Mono AR Panel (Landscape Canvas, ScaleWithScreenSize 2532x1170, match height): "Enter Stereo Mode" Object Sans **Bold** 48.
- Object Sans: (c) 2018 Alexander Slobzheninov, "All rights reserved", fsType 4. Already committed in public realitydeslab/holokit-app. Bundling in public holokit-unity-sdk is the user's call.

## Steps
- [ ] Render references at 2556x1179 (iPhone 15 Pro): holokit-app Star + Mono panels (prefab copies in a temp folder in transvision), SDK Stereo bar (reflection build) + SDK Mono canvas.
- [ ] SDK: bundle Object Sans BoldSlanted/Bold/Regular TTF; stereo bar uses them (no synthetic styles).
- [ ] SDK: Mono default UI restyled after holokit-app's Mono AR panel.
- [ ] Layout deltas from the side-by-side renders.
- [ ] Re-render, compare; SDK + transvision EditMode tests; device install + screenshot.
- [ ] Remove temp render folder from transvision.

## Results

## Interlude 2026-10-06: TransVision heat (user asked mid-task; UI work paused)
- Cause: Step2 blur was a dense box loop, 39x39 = 1521 reads/px at 960x540, 60 fps, sampling a 60x34 mip. SSAO active on Balanced/HighFidelity renderers for a scene with one unlit plane. Old build reached thermal Critical.
- Done: GaussianBlur.hlsl samples the same window on a 7x7 grid (49 reads, 31x fewer). Offline harness on IMG_1272.jpg: 39-tap mode is bit-identical to the old code; 7x7 differs only by a sub-pixel shift of the mask edge (final image mean |d| 0.84/255).
- Done: SSAO off (both renderers) + test `UrpRenderers_DoNotRunSsao`.
- Done: effect moved from Graphics.Blit in Update to a URP Render Graph unsafe pass (CameraImageProcessor.Pass.cs), enqueued from beginCameraRendering on the first game camera per frame; Step0 RT lost its unused depth buffer (RenderGraph rejects color+depth imports). Test `UrpPass_MatchesTheFormerBlitChain` (max diff <= 1). EditMode 100/100. Device build installed.
- Open: thermal A/B needs a cooled phone (it stayed Critical); device screen read exactly 0 in both old and new builds — likely camera covered, needs the user to point it at a scene.

## 2026-10-07: device black screen (pre-existing since the 6.3/ARF 6.4 upgrade)
- Root cause (device log): `Metal: Error creating pipeline state (Unlit/ARKitBackground): depthAttachmentPixelFormat is not valid and shader writes to depth`. Step0 blitted AR Foundation's background material (writes SV_Depth) into a colour-only target: Metal drops the draw, Step0 and everything after it stay black. Same in the old Graphics.Blit code.
- Fix: `Runtime/Resources/TransVisionCameraImage.shader` (Hidden/TransVision/CameraImage, Blit-compatible, ARKit's YCbCr->sRGB->linear math, row-vector display transform). CameraImageProcessor feeds it from `ARCameraManager.frameReceived` (works in Stereo where the SDK disables ARCameraBackground); display transform from the background material (HoloKit SDK's iOS 27 correction), else the frame's.
- Tests: `CameraImageShader_ConvertsYCbCrAndRemapsUvLikeARKit`, `UrpPass_WritesTheCameraFrameIntoStep0`. EditMode 101/101 + new test passing.
- Device: frame registered, shader supported, a direct blit through the material gave avg (83,78,101); at the time of the last check the room was too dark for ARKit (raw Y avg 21/255) and the phone was throttled to 30 fps ("iPhone needs to cool down"). Visual confirmation pending light.

## 2026-10-08: red photo test (offline, shipping URP pass)
- 8 Wikimedia Commons photos (CC0 / CC BY / CC BY-SA, attribution in scratchpad redtest/ATTRIBUTION.md) run through CameraImageProcessor's URP pass via a temp editor harness (Assets/_TempPerf/Editor/RedTestHarness.cs).
- Mask coverage: strawberries 20.5%, red rose 18.0%, red umbrella 6.9%, London bus (night) 1.8%, stop sign 1.4%, brick building ~0.2% (orange-brown brick below the saturation threshold; red text on a sign detected), apple tree ~0% (small apples averaged out by the LOD-4 blur), green forest control 0%.
- Result: saturated reds expand with the liquid distortion as designed; the constant flow-map distortion also ripples non-red areas (original design, unchanged).

## 2026-10-08: liquid B (L3) shipped as the default effect
- Research (cited in the report doc): Hugo Elias 2D wave equation, two-phase flow maps (Valve/Catlike Coding), Quilez domain warping, GPU Gems ch.1 sum of sines (not used), ch.38 stable fluids (too heavy).
- Prototypes L1 (B + domain-warp wobble), L2 (+ wave-equation ripples injected by red), L3 (+ baked bubble domes, rim + highlight); 3 s animations for 5 photos. Whole-frame motion on the no-red control: L3 8.1 vs current 8.7 (mean frame-to-frame change).
- Package: Runtime/Resources/TransVisionLiquid.shader (red field 240x135, blur, wave step 320x180, composite), Runtime/LiquidTextures.cs (runtime-baked noise + bubbles), Runtime/CameraImageProcessor.Liquid.cs (Effect {Liquid, Classic}, Shrink, Bulge, Ripples, Bubbles, Wobble, Red Wobble), Pass branches per effect.
- Tests: LiquidEffect_SpreadsRed_OrShrinksIt (x2); Blit-chain equivalence pinned to Classic. EditMode 104/104. iOS export + xcodebuild unsigned OK. Device: not connected, not run.
- Note: the `unity` CLI refused to run because another session's batch build used `-projectPath .`; ran the Unity binary directly with an absolute -projectPath.

## 2026-10-08/09: on-device test of liquid vs classic (blocked)
- Test build (scratchpad ios-dd-test, TvDeviceTest driver, frame timing on) installed on Holo iPhone 15 A. Every launch denied: "invalid code signature ... not explicitly trusted"; syslog "Profile Needs Network Validation". Retried every 2 min for ~4.5 h (30 min + 60 min + 3 h), no change. Crack B (profile validated earlier) never connected.
- To run: put Phone A on Wi-Fi (or trust the developer in Settings > General > VPN & Device Management), or connect Crack B; then `scratchpad/devtest/run_until_launch3.sh` (auto-switches to Crack B).

## 2026-10-12: on-device test done (Holo iPhone 15 A, A17 Pro, 2556x1179, SDK 5a16342)
- Profile validated; TvDeviceTest ran all phases. 60.0 fps in every phase; thermal stayed Nominal.
- GPU ms mean (p50/p95), 25 s per phase on the strawberry still: Off 4.51/4.50 (4.70/5.06); Liquid 6.73/6.13 (7.22/7.45); Classic 10.79/10.76 (10.77/10.98). Shrink 5.53, Umbrella 6.71, Stereo 6.52.
- Effect cost over Off: Liquid about +1.6 to +2.2 ms, Classic about +6.3 ms (Liquid about 1/3 of Classic).
- Live camera phases were black (camera covered or dark; no Metal errors in the log); still-image phases render the effect.

## 2026-10-12: EchoVision recording + SDK Mono buttons
- SDK pin 7ef2463 -> 5a16342 (same commit as EchoVision; SDK working-tree changes not included).
- Scene (via temp editor script): canvas keeps StepPreview/LogText; Record, Recording 4K (HD60/4K30) and Switch Render Mode buttons taken from the SDK prefab, CanvasScaler copied from it; recorder: AudioInput HoloKitMicrophoneCapture, includeMicrophone, Voice processing, HD60 default (as EchoVision). Stereo bar left at the SDK default.
- Test: ProjectIntegrityTests.Recorder_AndMonoButtons_MatchEchoVision.
- TvDeviceTest parked in scratchpad so the hand-test build does not run it.

## 2026-10-12: AR background orientation wrong on iOS 27 (fixed)
- Symptom (user): live camera background in the wrong orientation.
- Cause: CameraImageProcessor read `_UnityDisplayTransform` from the AR background material in `frameReceived`. On iOS 27 the HoloKit SDK (ARKitCameraCompatibility) replaces that material and corrects the transform in Update/onBeforeRender, but AR Foundation writes the frame's own (wrong) transform into it on every frameReceived, so TransVision read the uncorrected one.
- Device evidence (probe, old build, LandscapeLeft): ours `[0 -1; -0.346 0 | 0.673 1]` vs SDK `[1 0; 0 -0.615 | 0 0.808]`, MISMATCH every check; screenshot comparison RED (TransVision image rotated vs SDK background).
- Fix: frameReceived only stores the frame's matrix; `UpdateDisplayTransform()` reads the replaced material's matrix in beginCameraRendering (after the SDK correction); falls back to the frame's matrix when unset. No HasProperty (matrix is not a declared property).
- Tests: CameraImage_UsesTheBackgroundTransformCorrectedBeforeRendering (red with the old timing: 106/107; green with the fix), CameraImage_FallsBackToTheFrameTransform. EditMode 108/108.
- iOS minimum 15.0 -> 27.0 (user request); test Ios_MinimumVersionIs27.

## 2026-10-12: stereo bar off, plane fit, liquid motion only in red
- Stereo: m_StereoBarItems = 0 (as EchoVision); test Stereo_ShowsNoSdkStereoBar.
- Mono plane: sized every frame from the Mono camera's projection (ScaleToFillView(projection), CenterOfView) instead of fieldOfView once at start (was ~1.4x zoomed: fieldOfView stayed 60 while ARKit/the SDK set the real projection). Test PlaneFromAnOffCentreProjection_FillsTheViewportCornerToCorner. Not yet verified on device; user reported a thin line once and a frame at the top in portrait (landscape fine) - open.
- Liquid (user request): dynamic distortion only inside the red, small static distortion elsewhere. Composite: still warp = Noise without time * _Global (m_Wobble 0.006 -> 0.003); flowing warp (3x faster), ripples, bubbles, rim and highlight all multiplied by the red mask (m_RedWobble 0.012 -> 0.02). Test LiquidComposite_MovesInsideTheRed_StaysStillOutside: red with the old shader (outside max diff 81), green now. EditMode 111/111.
- Probe now gated by env TVPROBE=1 (DEVICECTL_CHILD_TVPROBE=1); launch args do not reach Unity on iOS. Builds now incremental (TV_IOS_APPEND, one DerivedData folder).

## 2026-10-12: Mono full screen, Stereo plane = AR camera frustum
- Mono: CameraImageProcessor.DisplayPass (AfterRenderingTransparents, AddBlitPass) copies the result over the AR camera's picture; ResultPlane hidden. Test MonoCamera_ShowsTheResultFullScreen. Recorder re-renders the AR camera per frame, so the pass also feeds Mono and Stereo movies (device: both movies upright with the effect; 2544x1168 and 2560x1440).
- Open: movies are ~30 fps (113 frames / 3.99 s, 120 / 4.01 s) although HD60 is selected.
- Stereo misalignment (user): plane followed the centre eye and was sized for 60 deg fov x screen aspect: 25 x 11.5 m at 10 m vs the AR camera's 13.9 x 6.4 m (ARKit m00 1.441, m11 3.125 -> 69.5 x 35.5 deg). Modelled error for the left eye: 17-21 deg at the picture edge at any depth.
- Fix: plane is a child of the AR camera, FitToCameraView(projection, D): scale 2D/m00 x 2D/m11, centre D*m02/m00, D*m12/m11; D = 2 m. Camera to centre eye = CameraOffset + MrOffset = (0.040, -0.062, -0.078) m; modelled error at D = 2 m: 0 at 2 m, <= 1.5 deg from 1 to 10 m, ~4-5 deg at 0.5 m (parallax). Tests: FitToCameraView_* (corners, each texel on its camera pixel, any pose).
- Device check of the camera side: ARKit intrinsics fx = fy = 1424.0, c = (960.4, 729.6) at 1920x1440 give m00 = 1.4834, m11 = 3.2159 (visible height 1440 x 0.615), m12 = 0.0216; the projection in use was 1.4834 / 3.2158 / 0.0227. No camera-side error.
- User test (HoloKit X, objects within 1 m): plane at 2 m showed the picture low and large, as the parallax model predicts for near points. Distance sweep 0.4/0.6/1.0/2.0/4.0 m: 0.4 m closest. User chose a fixed 0.4 m (no LiDAR, no adaptive depth). PlaneDistance = 0.4.

## 2026-10-12: Stereo = LiDAR depth grid on the whole camera image
- Fixed 0.4 m: near aligned, a door at ~4 m looked ~15% small and ~6.5 deg high (user). No fixed D works for both: camera 7.8 cm in front of and 6.2 cm above the eyes. User agreed to LiDAR.
- Whole image in Stereo: CameraImageProcessor.WholeCameraImage uncrops the SDK display transform (WholeImageTransform; crop (1, 0.615) on iPhone 15 Pro: 34.5 -> 53.6 deg vertical); ScreenCrop drives the full-screen pass's region (recordings stay the screen's view) and PlanePlacer.WholeImageProjection. Bottom band without picture at 0.4 m: 13.3 -> 4.4 deg.
- Depth grid: PlanePlacer builds a 97x73 grid under the AR camera; Hidden/TransVision/StereoPicture places vertex uv at d * ((2u-1+m02)/m00, (2v-1+m12)/m11, 1) with d = ARKit environment depth sampled through _TVPictureTransform (global from CameraImageProcessor). AROcclusionManager added at runtime, NoOcclusion, Fastest + temporal smoothing, enabled only in Stereo. Fallback flat at 1 m.
- Test DepthGrid_SeenFromAnEye_LooksLikeTheScene: tilted wall 0.6-1.85 m, left eye at the SDK offset: mean difference 0.40/255 at depth vs 11.48/255 flat. WholeImageTransform_UncropsTheScreenTransform (landscape + portrait). EditMode 110/110.
- Device: LiDAR depth worked in Stereo (256x192 DepthFloat32, centre 2.4-2.9 m), but the user dropped LiDAR as too costly. Reverted: no AROcclusionManager, no depth grid shader, no ARSubsystems reference.
- Final Stereo (user's choice): flat plane, whole camera image, PlaneDistance 1 m, PictureZoom 1.0 (1.1 tried; the user chose 1.0). Tests: FitToCameraView_* (incl. ZoomMagnifiesAboutTheCentre), WholeImageTransform_*. EditMode 114/114.
