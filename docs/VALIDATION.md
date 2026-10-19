# MadarDrop 0.3.0 validation

Local Windows x64 release checks, 19 October 2026: **141 checks passed**.

| Suite | Passed |
| --- | ---: |
| Conversions, Unicode, output decoding and source preservation | 40 |
| Image editing, redaction, PDF and metadata | 31 |
| Focused floating tools without revealing the main window | 5 |
| Crop handles, aspect ratios, export and source preservation | 17 |
| Arabic/English, light/dark and preference persistence | 11 |
| Update versions, optional media package integrity and notices | 9 |
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
