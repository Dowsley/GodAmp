# Theme milestones

## Scope and order

The target is compatibility with Winamp classic `.wsz` skins for the player,
equalizer, playlist, and visualizer window frame. Modern `.wal` skins are outside
this plan.

Work through the milestones in order and verify visuals throughout. Milestones
1-5 form the first theme-completion target. Milestone 6 adds window modes and
resizing. Milestones 7-8 establish production readiness.

EQ preset management, playlist commands, additional visualizer effects, and
distribution signing are separate project work.

## 1. Reliable skin loading

- [x] Resolve missing assets against the built-in skin on every load.
- [x] Use the volume sheet when a custom skin omits `BALANCE.BMP`, matching Winamp.
- [x] Support `NUMS_EX.BMP`, including its precedence over `NUMBERS.BMP`.
- [x] Validate decoded assets before applying them.
- [x] Preserve the active skin when loading fails and report useful errors.
- [x] Restore all default assets and skin state when selecting the default skin.
- [x] Avoid temporary extraction files by reading supported assets in memory.

**Done when:** switching between complete, incomplete, and invalid skins never
mixes assets or corrupts the active skin. Loading a skin produces the same result
regardless of which skin was selected previously.

## 2. Correct text and colors

- [x] Parse `PLEDIT.TXT` normal/current text colors, normal/selected backgrounds,
  and playlist font.
- [x] Provide predictable defaults for omitted settings and unavailable fonts.
- [x] Verify bitmap glyph mapping and layout for supported text and number sheets.
- [x] Render text backgrounds and transparency correctly for colored skins.
- [x] Align clock digits and implement the extended number sheet's required glyphs.
- [x] Update existing playlist entries and text when switching skins.

**Done when:** the compatibility test skins display their intended playlist
styling and readable, correctly aligned text, including after a skin switch.

Milestones 1 and 2 are verified on macOS with Godot Mono 4.7.2. The installed
collection passes loading and live-style checks at 1x and 2x. Fresh-load,
cross-skin switching, and restart captures match; restoring the default skin
also reproduces its initial captures. See [Theme testing](THEME_TESTING.md) for
commands, coverage, and limitations. Cross-platform and packaged-build checks
remain in milestones 7 and 8.

## 3. Pixel-accurate controls

- [x] Use fixed reference coordinates for non-resizable main-panel and EQ controls.
- [x] Audit atlas rectangles against Winamp, including EQ on/auto sprite boundaries.
- [x] Match slider track sizes, thumb positions, frame selection, and endpoints.
- [x] Implement separate on/off and mouse-pressed states for toggle controls.
- [x] Verify normal, pressed, hover, and disabled rendering where applicable.
- [x] Switch main, EQ, playlist, and generic titlebar artwork with window activation.
- [x] Verify alignment and hit targets at 1x and 2x scaling.

**Done when:** controls align with the skin artwork at both scales, including
slider endpoints and every supported interaction state.

The control suite checks real scene geometry, EQ audio connections, slider
endpoint input, toggle states, and reversible titlebar activation at 1x and 2x.
Input tests use scene controls in an isolated viewport to avoid OS pointer
interference. Native captures cover the installed collection on macOS.
Auto-EQ and presets remain disabled; windowshade behavior and its extended
artwork remain in milestone 6. Generic title lettering remains in milestone 4.

## 4. Complete skinned displays

- [ ] Draw the EQ response curve and preamp line using the skin's graph assets.
- [ ] Implement the main-panel spectrum analyzer and oscilloscope.
- [ ] Parse and apply the `VISCOLOR.TXT` palette, with defaults for missing data.
- [ ] Render playback status from `PLAYPAUS.BMP`.
- [ ] Render mono/stereo status from `MONOSTER.BMP`.
- [ ] Render generic-window title lettering from `GEN.BMP`, including variable
  glyph widths and active/inactive states.

**Done when:** displays respond to playback and settings and use the selected
skin's artwork and colors. The analyzer palette applies to the classic
main-panel display; custom visualizer effects retain their own styling.

## 5. Window shapes and cursors

- [ ] Parse supported `REGION.TXT` polygon sections.
- [ ] Apply window rendering masks and matching mouse hit regions.
- [ ] Scale masks consistently with the UI.
- [ ] Decode skin cursors and preserve their hotspots.
- [ ] Map cursors to the appropriate windows and controls.
- [ ] Provide fallback cursors and document animated-cursor support boundaries.
- [ ] Verify shaped windows and cursors across supported platforms.

**Done when:** shaped skins have correct cutouts and mouse behavior, and controls
use the intended skin cursors without hotspot offsets.

## 6. Windowshade and resizing

- [ ] Implement collapsed main-panel, EQ, and playlist modes.
- [ ] Support `EQ_EX.BMP` and the compact controls used by windowshade modes.
- [ ] Apply the corresponding titlebar states, cursors, and region masks.
- [ ] Make playlist and visualizer frames resizable with correct tiling and
  minimum sizes.
- [ ] Preserve docking and scaling behavior through mode and size changes.
- [ ] Persist and restore supported window sizes and modes.

**Done when:** each supported window mode renders and behaves correctly through
resizing, scaling, docking, and restoration.

## 7. Compatibility hardening

- [ ] Handle archive filename casing, nested paths, and path-separator variations.
- [ ] Define deterministic behavior for duplicate assets and image extensions.
- [ ] Verify supported bitmap formats and dimensions; reject or fall back cleanly
  for invalid assets.
- [ ] Handle malformed or incomplete metadata without partial skin application.
- [ ] Verify fallback fonts on supported platforms.
- [ ] Verify startup restoration, missing saved skins, and repeated skin switching.
- [ ] Verify resource discovery and loading in packaged builds.
- [ ] Record unsupported cases with actionable diagnostics.

**Done when:** the documented skin test set loads predictably in development and
packaged builds, and broken or unsupported files produce understandable results.

## 8. Verification and release readiness

- [ ] Maintain screenshot comparisons against classic Winamp, using Webamp as a
  supplementary reference.
- [ ] Add focused regression tests for asset resolution, metadata parsing, glyph
  mapping, and skin-state transitions.
- [ ] Run the visual and interaction checklist on supported platforms.
- [ ] Document supported skin features and remaining compatibility exceptions.
- [ ] Update the README and TODO list to reflect verified support.

**Done when:** the compatibility checklist passes and remaining exceptions are
explicitly recorded. Tests cover GodAmp's own logic and meaningful failure cases.

## Compatibility test set

Start with the installed skins and add targeted fixtures for cases they do not
cover. Installed skins live outside the repository and are not test dependencies
unless explicitly supplied.

| Skin or fixture | Coverage |
| --- | --- |
| XBOX_MasterAmp and Zelda_Amp_3 | Extended number sheets without `NUMBERS.BMP`; custom cursors |
| Winamp3_Classified_v5.5 | Both number-sheet variants; extended-sheet precedence |
| Playstation-Amp | Gray playlist backgrounds and black current-track text |
| Unreal_Skin_Of_The_Same_Game | Red playlist styling, Tahoma font, custom cursors |
| MountainDew | Window-region polygons, including the EQ cutout |
| SoundLair_v1_21 | Custom cursors and an omitted generic-window sheet |
| CHROMEngine_Indigo_v5 followed by ASTROCYT | Switching from a skin with `GEN.BMP` to one without it |
| Complete installed collection | General artwork, text, controls, and switching checks |
| Targeted incomplete/invalid archives | Missing balance sheet, malformed metadata, duplicate names, invalid images, and load failure |
| Targeted window-mode skins | Windowshade artwork, resizing, masks, and cursor states |

For each applicable milestone, check a fresh load, a switch from another skin,
restoration of the default skin, and restart with the skin selected. Compare
visuals at 1x and 2x and exercise the relevant control states.

## Implementation references

GodAmp entry points:

- `Src/Autoload/SkinLoader.cs`: skin-state preparation, atlas replacement,
  bitmap fonts, and default restoration.
- `Src/Utils/SkinArchive.cs`: in-memory archive decoding and missing-sheet resolution.
- `Src/Controls/Playlist/PlaylistTrackEntry.cs` and `.tscn`: playlist text and
  selection styling.
- `Src/Controls/MasterPanel/` and `Src/Controls/Equalizer/`: control layout,
  sliders, and display behavior.
- `Data/SkinResources/`: atlas rectangles.
- `Src/Utils/SkinBitmapFont.cs`: colored bitmap glyph mapping and clock fonts.
- `Src/Core/WindowManager.cs`: window focus, positioning, and docking.

The local Winamp checkout is at `~/Code/winamp`. Reference its classic renderer:

- `Src/Winamp/draw.cpp`: bitmap fallbacks, number-sheet precedence, visualization
  palette, and generic-window font extraction.
- `Src/Winamp/Skins.cpp`: playlist metadata, regions, and cursor mapping.
- `Src/Winamp/draw_main.cpp`: main-panel coordinates, control states, and indicators.
- `Src/Winamp/draw_eq.cpp`: EQ sprite boundaries, sliders, and response graph.
- `Src/Winamp/draw_pe.cpp`: playlist rendering.
- `Src/Winamp/draw_embed.cpp`: generic-window frames and title lettering.

The supplementary Webamp checkout is at `~/Code/webamp`; classic skin parsing and
sprite definitions are under `packages/webamp/js/`.
