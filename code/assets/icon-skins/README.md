# Application icon skin samples

These two optional visual packs are displayed inside QiDesk. Selecting a pack
does not modify Windows shortcuts, application binaries or system resources.

- Fluent: https://github.com/vinceliuice/Fluent-icon-theme
  - Revision: c8a244f571ef8fb47812bdc9dbce76546cedce69
- Papirus: https://github.com/PapirusDevelopmentTeam/papirus-icon-theme
  - Revision: bf539287ef5dc18529424a02cccee76175920a6f

Both icon themes are distributed under GPL-3.0. Each directory contains its
upstream license, original selected SVGs, derived transparent 128px PNGs and
SOURCE.json with upstream paths, resolved symlinks and content hashes.
Application names/logos belong to their respective owners. The application
matching table is maintained separately in pack.json.

PNG derivatives were rendered from the SVG originals using sharp. The prepared
PNGs, manifests, source SVGs and licenses are included here; no conversion tool
is required to build the application. code/build.ps1 copies the runtime packs
and licenses into dist.

Seelen UI and Shortcut Icon Changer were reference material only. No code or executable from either project is bundled
with QiDesk. The JSON format here is not a Seelen YAML importer.
