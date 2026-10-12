# Release TransVision 0.1.0 (build 1)

Date: 2026-10-12. Request: push this version to TestFlight and the App Store, clear GitHub history, new version tag.
Decisions (user): 0.1.0 build 1, tag v0.1.0; TestFlight + submit for review (show the What's New text first);
grouped Conventional Commits pushed to main; HoloKit SDK from GitHub tag 0.6.0-preview.1.

App Store Connect: TransVision, app id 6736925900, live 0.0.5 (READY_FOR_SALE); last build 0.0.5 (0), all expired.
Signing team KR9H35SQQ9.

## Steps
- [x] 1. Remove temp files: Assets/_TempUiRef, Assets/_TempPerf except Editor/TempIosBuild.cs (kept untracked until the release build, then deleted).
- [x] 2. manifest: io.holokit.unity-sdk -> https://github.com/holokit/holokit-unity-sdk.git#0.6.0-preview.1; re-resolve (packages-lock).
- [x] 3. Version: ProjectSettings bundleVersion 0.1.0, iPhone buildNumber 1; package.json 0.1.0.
- [x] 4. EditMode tests green.
- [x] 5. Commits ee4ab61..0e07795 (8, Conventional Commits). Push main + tag v0.1.0 after the upload succeeds.
- [x] 6. Release build: Unity export -> xcodebuild archive (Release) -> exportArchive (app-store-connect, upload).
- [x] 7. ASC: build b18aa81d VALID (minOS 27, no encryption), TestFlight Internal IN_BETA_TESTING; version de99720c 0.1.0 created, What's New set, build attached. Submitted 2026-10-12 13:13 UTC (submission a0b78155), WAITING_FOR_REVIEW; age rating: 7 new questions answered No/NONE (4+).
- [x] 8. Delete Assets/_TempPerf; final git status clean except plan/.

## Notes
- plan/ committed at the user's request (API key details removed).
- Temp files parked in scratchpad/parked (_TempUiRef kept for the paused SDK UI font task).
- Build commit originally carried the PostProcessing_EchoVision.asset deletion; rewritten locally (unpushed) so the move commit holds the rename.
- Pushed main 36cd6d5..0e07795, tag v0.1.0 (b97dea6), GitHub release https://github.com/realitydeslab/transvision/releases/tag/v0.1.0
- Upload: exportArchive with the API key fails (Cloud signing permission error); without -authenticationKey* flags (Xcode account) it works.
- Old versions tagged (user request): v0.0.1=52d9628, v0.0.2=5d49f1c, v0.0.4=0ef7033, v0.0.5=36cd6d5 (0.0.3 never shipped, not tagged).
- App Store: copyright 2024-2026 Reality Design Lab (user).
- Release binary on device: development re-sign of the same archive (UUIDs E8B3262A / AC25A648 and Data hash match) ran 28 s on Holo iPhone 15 A, Release build, no exceptions or Metal errors; opened in Stereo (last used mode) with the effect visible. Recording and Mono not exercised (need taps).
- TestFlight External (user): Beta App Description = App Store description, privacy policy https://docs.holokit.io/legal/privacy-policy; build added to group External (public link); beta review submitted, WAITING_FOR_BETA_REVIEW.
