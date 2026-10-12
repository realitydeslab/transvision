# TransVision: Unity 6.3 LTS + local HoloKit SDK + package move + tests

Date: 2026-10-04. Precedent: `../echovision` (commits f5cb0fe, c4b7a6b, 5a3e572).

## Goal
1. Editor 6000.1.1f1 (working tree) -> 6000.3.24f1 (Unity 6.3 LTS, installed).
2. `io.holokit.unity-sdk` git URL -> local `../holokit-unity-sdk` pinned by commit, as echovision does.
3. App content `Assets/{Scripts,Scenes,ShaderGraph,...}` -> embedded package
   `Packages/org.realitydeslab.transvision`, keeping every `.meta` so GUIDs hold.
4. EditMode tests for the pure logic in the package; listed in `testables`.

## Starting state
- Uncommitted user work: 6000.1.1f1 upgrade + `PlanePlacer` ParentConstraint change. Kept, built on.
- No `Library/` in transvision: first open is a full import.

## Steps
- [x] P0 Resolve every GUID referenced by the scene/prefab (project, SDK, packages). All resolve; b7f0e674 is Unity's built-in LightingSettings.
- [x] P0 Manifest: Unity 6.3 package versions (URP/VFX 17.3, ARF/ARKit 6.4.3, test-framework 1.6), SDK `git+file` pin 7ef2463, testables.
- [x] P0 `git mv` content into the package; asmdef `TransVision` (runtime), `TransVision.Editor.Tests`.
- [x] P0 Fix hard-coded `Assets/...` paths (EditorBuildSettings, SimulationEnvironmentAssetsManager, `NewPostProcessEffectRendererFeature`).
- [x] P0 URP 6.3 cleanup (user scope: "URP 6.3 cleanup only"): template renderer feature is RenderGraph-only; `displayName` -> `[DisplayInfo]`.
- [x] P0 SDK iOS build rule: `adjustIOSFPSUsingThermalState: 0` (6.3 default is on; the SDK fails iOS builds).
- [x] P1 Extract testable math (plane scale, HSV conversion, ParentConstraint setup) into static code; tests.
- [x] P0 Headless open in 6000.3.24f1, compile clean, run EditMode tests.
- [x] P1 iOS build export smoke check.
- [x] P1 README "How to run" update.

## Decisions
- Project config (URP assets, XR settings, TextMesh Pro, ProjectSettings) stays in `Assets/` (echovision rule).

## Results
- Import in 6000.3.24f1: 0 `error CS`; one 6.3 deprecation warning in our code, fixed.
- EditMode: 48/48 passed on the macOS target (TransVision 25, HoloKit SDK 23); 98/98 after switching to the iOS target (TransVision 25, HoloKit SDK 73).
- Negative check: `adjustIOSFPSUsingThermalState: 1` makes `IosBuild_PassesEveryHoloKitErrorRule` fail (exit 8) with the SDK thermal message.
- iOS export (BuildPipeline.BuildPlayer via a throwaway Assets/Editor script, since removed): Succeeded, 0 errors, 49 warnings; the only shader warnings are VFX Graph's own `Sort` compute (threadgroup memory on pre-A11). `xcodebuild` Release, iphoneos, unsigned: BUILD SUCCEEDED, 0 errors. Device run not done.
- Resolved: URP 17.3.0 (builtin), ARF/ARKit 6.4.3, test-framework 1.6.0, Input System 1.20.0, XR Management 4.6.1, io.holokit.unity-sdk git+file 7ef2463.

## Open
- `defaultScreenOrientation` 4 -> 3 (LandscapeLeft) is in the working tree; not set by this migration (no step writes it). Confirm it is intended.
- SDK is pinned by absolute `git+file:///Users/amber/...` path (same as echovision); other machines/CI need the same clone path.
- `Force IOS Speakers When Recording` is 0: SDK warning-level rule (not build-blocking). echovision sets it to 1; left unchanged here.
- On-device check of Mono/Stereo red effect after the URP 17.3 bump.

## Update 2026-10-12 (0.1.0 release)
- SDK comes from the GitHub tag 0.6.0-preview.1 (commit 5a16342), no longer the local git+file path.
- defaultScreenOrientation 3 (LandscapeLeft) kept; shipped in 0.1.0.
- On-device check of Mono/Stereo done (see sdk-ui-holokit-app-look-2026-10-06.md).
