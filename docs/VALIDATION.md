# MadarDrop release validation

## 0.4.4 — ordered video joining

22 October 2026: the user requested no application testing. The new joining flow is
reviewed in source and compiled, but neither the app nor installer is executed and no
new media export or UI interaction is claimed as tested. CI is skipped for this release
commit for the same reason. Previous release checks below are historical evidence only.

Packaging checks compare ZIP entries, lengths and SHA-256 hashes against the embedded
payload manifest and ensure the source archive excludes private QA, user media and logs.
The installer still excludes the optional FFmpeg binary. Screenshots remain the existing
0.4.3 fixtures; there are no fabricated screenshots of the unlaunched joining window.

## 0.4.3 — video tools, media metadata and saved preferences

22 October 2026: before the user requested that all further app testing stop, 201 local
application checks completed: 20 new video/media checks, 27 tool/model checks, 17 image crop,
31 editor, 15 workflow, 18 video crop, 22 mute/progress, 11 language/appearance and 40 core checks.
No further app or installer executions are performed for this release after that instruction.
The release is compiled and packaged without launching the app; installer extraction/upgrade
was not re-tested for 0.4.3. The release commit skips CI to respect the same request.

The media suite generated its own three-second video with two audio tracks. It checked Unicode
and escaped metadata, unchanged encoded streams after metadata editing, location removal,
accurate split boundaries and decoded content, timed solid and blurred masks, audio retention,
silent videos, speed/pitch, cancellation during processing, source preservation and cleanup.

Tool/model checks covered all nine video wheel positions, time parsing, split limits,
redaction geometry and intervals, draggable range clamping, cloned parameters, persistent
settings, progress fractions and native English/light and Arabic/dark renders. Screenshots in
this source package were generated before the stop instruction from synthetic fixtures.
They are not screenshots of user media or the original application.

See VIDEO-TOOLS-0.4.3.ar.md for capabilities and remaining differences. Remote CI remains
available for future developer use, but it is not a claimed validation of this release.

## 0.4.2 — PDF page exports and video snapshots

21 October 2026: 134 current local application checks passed: 22 PDF export, 17 snapshot/progress,
40 core conversion, 18 video crop, 22 mute/progress and 15 floating workflow checks.

The full embedded installer passed 11 further checks, including upgrading the actual 0.4.1
executable in place in an isolated directory. Total local checks: 145. All 293 files in the
clean installer payload passed manifest verification.

The new PDF suite generates Unicode-named vector/text PDFs with varied page dimensions,
rotation, crop boxes and custom physical units. It decodes actual 300 DPI PNG/JPG output,
checks colors, dimensions, order and resolution metadata, and verifies split-page text,
rotation and metadata with an independent reader. Existing results and source bytes remain
unchanged. It cancels during a multi-page render (before the worker completes), checks
cleanup, and rejects encrypted, corrupt, empty, oversized and 301-page raster exports.
A large vector page can still split without a huge raster allocation.

The snapshot suite extracts distinct selected video times, compares every decoded PNG pixel
with the full-size source frame, verifies order, frame-rate navigation, source hashes,
no-overwrite naming, invalid time rejection and cancellation during a real export.
It renders the actual WPF window in English/light and Arabic/dark, including selected-frame
thumbnails. Variable-rate video stepping remains a documented limit. Native UI input
automation was not performed. The reference snapshot window was inspected on the official
website; public screenshots contain only our app and locally generated fixtures.

## 0.4.1 — silent video copies and compact conversion progress

21 October 2026: the current local application suites passed 40 core conversion checks,
18 video crop checks, 15 workflow checks, 11 language/appearance checks, and 22 new
mute/progress checks (106 application checks in total).

The media suite generates a Unicode-named clip with two audio tracks. MP4, MOV, MKV,
WebM and M4V outputs have no audio streams and retain every decoded video frame and
recording duration. A separate MKV fixture verifies that subtitles survive muting.
Already-silent input succeeds; source hashes remain unchanged. Pre-cancel and cancellation
during processing leave no published or temporary output. The suite verifies timestamp-based
progress, a compact single-close-control window, saved counts, late-callback protection,
and native Arabic/dark and English/light renders. Regression checks also cover recordings
over 24 hours, missing/out-of-order progress, failed batches, automatic cancellation dismissal,
and persistent errors after a close request.

The full embedded installer passed 11 additional checks, including upgrading the actual 0.4.0
executable in an isolated folder. The clean payload contains 293 files, all verified by its manifest.

The three user-supplied reference videos and additional official pages were reviewed.
Evidence, timestamps and remaining differences are recorded in VIDEO-REVIEW-2026-10-21.ar.md.
Reference media are excluded from the public source package. Native app input automation
remains unperformed under the user's preference. Media tests use local FFmpeg; the public
installer obtains that optional component separately through Settings.

## 0.4.0 — visual video crop and floating tools

20 October 2026: local application suites passed: 40 conversion, 31 editor, 8 workflow,
17 image crop, 11 language/appearance, 10 release and 18 video/workflow checks.
The new media suite creates its own Unicode-named video with sound and a silent variant.
It decodes the crop to verify dimensions and region content within lossy-encoding tolerance,
checks audio/duration and source hashes, tests cancellation during processing and temporary-file
cleanup, and renders the compact tool and progress windows in both appearances/languages.
No ffprobe installation is required. Media tests use the existing local FFmpeg, which is not
included in the public package. CI also runs the non-media progress and compact-tool checks.

Installer/update suites passed 11 full-package and 17 rollback/long-path checks, including upgrading
the actual 0.3.1 executable in an isolated folder. Together the local suites passed 163 checks.

The official 22.5-second demo was watched with frame seeking; observations and differences are
recorded in REFERENCE.ar.md. Screenshots here are self-rendered from our WPF windows, not copied
from the original. Live Explorer drag/drop automation remains unperformed under the user's
preference to keep the existing test approach. The original product was not run on macOS.

## 0.3.1 — lemon mark and orange appearance

19 October 2026: the existing 11 language/appearance checks and 11 full installer checks passed,
including upgrading the actual 0.3.0 executable in place. The original vector mark is used in
the UI/tray and compiled into the app, installer and uninstaller executables. The ICO includes
16, 24, 32, 48, 64, 128 and 256 pixel frames. Light Arabic/English and dark screenshots were
rendered from the application. Existing conversion/editor behavior was not changed by this update.

Reference research is recorded in `REFERENCE.ar.md`: 22 tool-window image links, press screenshots,
observed interaction and remaining functional differences. Source screenshots are linked rather
than copied into the package. Native app UI automation was not performed.

## 0.3.0 baseline

Local Windows x64 release checks, 19 October 2026: **142 checks passed**.

| Suite | Passed |
| --- | ---: |
| Conversions, Unicode, output decoding and source preservation | 40 |
| Image editing, redaction, PDF and metadata | 31 |
| Focused floating tools without revealing the main window | 5 |
| Crop handles, aspect ratios, export and source preservation | 17 |
| Arabic/English, light/dark and preference persistence | 11 |
| Update versions, optional media package integrity and notices | 10 |
| Update/rollback, unsafe paths and installation beyond 260 characters | 17 |
| Full embedded setup, repair, legacy upgrade and managed uninstall | 11 |

The full installer test installed its actual embedded package into an isolated deep directory,
repaired damage, created a Windows shortcut, upgraded a legacy executable in place, and checked that
uninstall and upgrades preserved unrelated files. It did not modify the user's real installation,
registry entry, preferences or shortcuts. Rollback was fault-injected and verified with file hashes.

Screenshots were rendered from the actual application and installer windows, including both languages
and appearances. A user previously confirmed the Shift wheel on their desktop. Interactive computer
control was not performed after the user declined it; live Explorer drag/drop, installation prompts,
registry registration and removal still benefit from manual testing on clean Windows machines.

The media archive tests use an isolated fixture to test extraction, checksum rejection and retained
license notices. Existing FFmpeg was used for the conversion suite. Public payloads exclude FFmpeg.
GitHub Actions provides a separate clean Windows build and runs the checked-in regression suites.
