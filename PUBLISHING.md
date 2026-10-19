# Publishing MadarDrop

Publish only the independent application, installer, tests and build files. Release assets are the
setup, curated source ZIP and SHA-256 checksums. Do not publish the development workspace, account
credentials, user files, or preview backups. Screenshots under docs/ are from our own application.

Keep the independent name/orbit mark and non-affiliation statement. Do not describe this as an official
Tangerine release or use that product's logo, screenshots or assets. Copyright rules can distinguish
functionality from protectable code and screen expression; trademarks can raise origin-confusion
issues. Renaming a close interface reproduction does not settle every legal question. This is not
a legal clearance opinion; consult a lawyer for the relevant jurisdictions before a commercial launch.

Primary references:
- https://www.copyright.gov/circs/circ61.pdf
- https://www.uspto.gov/trademarks/search/likelihood-confusion
- https://ffmpeg.org/legal.html

MIT covers our source, not dependencies. The public installer retains Python/PDF/image licenses and
excludes FFmpeg. It can acquire FFmpeg directly from its provider on the user's request. If you choose
to redistribute a FFmpeg binary, provide its EXACT corresponding sources, build information and the
notices required by that binary and all its libraries. Publishing our source is not a substitute.

For a new version, update the application/installer assembly versions, SetupSupport.Version,
builder filenames and docs. Build in a clean output folder, run release checks, generate checksums
and publish a versioned release. The in-app check compares the latest stable GitHub tag and opens
the release page; it never installs silently. Obtain your own signing certificate for signed builds.
