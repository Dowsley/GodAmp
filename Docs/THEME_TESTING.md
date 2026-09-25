# Theme testing

## Isolated data

Set `GODAMP_DATA_DIR` to a disposable directory for verification. Settings and the
`Skins` directory are resolved beneath it. Without this variable, GodAmp uses its
normal platform data directory.

Use the Godot Mono executable matching `GodAmp.csproj`. The examples below use
the macOS installation; substitute the executable path on other platforms.

```sh
godamp_godot="/Applications/Godot Mono 4.7.2.app/Contents/MacOS/Godot"
godamp_test_data="$(mktemp -d /tmp/godamp-themes.XXXXXX)"
dotnet build --no-restore -p:UseSharedCompilation=false
```

Run `dotnet restore` first if dependencies have not been restored.

## Regression tests

```sh
GODAMP_DATA_DIR="$godamp_test_data" \
  "$godamp_godot" --headless --path . \
  --log-file "$godamp_test_data/regression.log" \
  res://Tests/ThemeTests.tscn
```

The runner returns a nonzero exit code on failure and prints
`PASS: theme regression tests` on success. Fixtures are generated beneath the
isolated data directory; no third-party skins are required.

Coverage includes:

- Case-insensitive playlist metadata, per-field defaults, and font selection.
- Fixed bitmap glyph coordinates for oversized sheets, punctuation aliases,
  extended number glyphs, and preserved image colors.
- Nested archive paths, missing assets, balance fallback, cropped optional artwork,
  and extended number-sheet precedence.
- Failed-load preservation of textures, fonts, selected skin, persisted settings,
  and skin-change notifications.
- Corrupt, empty, missing, and undersized inputs; default restoration and switching
  independent of the preceding skin.

Corrupt-image fixtures intentionally produce decoder diagnostics and loader
warnings. The final result and exit code distinguish expected rejection from a
test failure.

Set `GODAMP_TEST_SKINS` to a directory of `.wsz` files to include real-skin loading
checks. The source directory is read-only to the runner.

## Control geometry and interaction

```sh
GODAMP_DATA_DIR="$godamp_test_data" \
  "$godamp_godot" --path . --rendering-method gl_compatibility \
  --audio-driver Dummy --log-file "$godamp_test_data/controls.log" \
  res://Tests/ControlTests.tscn
```

This requires a graphical desktop. It checks the actual application scene
rectangles and EQ audio bindings. Copies of scene controls receive pointer
events through an isolated viewport at 1x and 2x, keeping native OS mouse
movement from interfering with held-state assertions. Coverage includes slider
endpoints and track frames, disabled input, toggle on/off and held states,
hovering, release outside the hitbox, completed clicks, and titlebar focus-signal
transitions. Supplied skins are read from the isolated `Skins` directory.

The runner prints `PASS: classic control regression tests` and returns zero on
success. Native rendering is checked separately with the capture runner.

## Rendering and live updates

Copy the desired `.wsz` files into `$godamp_test_data/Skins` after running the
regression suite, then run:

```sh
GODAMP_DATA_DIR="$godamp_test_data" \
  "$godamp_godot" --path . --rendering-method gl_compatibility \
  --audio-driver Dummy --log-file "$godamp_test_data/rendering.log" \
  res://Tests/ThemeCapture.tscn
```

This requires a graphical desktop. It opens the real application scenes, checks
clock coordinates and live playlist styling, and saves main/EQ/playlist captures
under `$godamp_test_data/Captures` at 1x, 2x, and 4x. The capture sequence includes the
default skin, each installed skin, and default restoration. Captures keep the
marquee and clock blink stable for comparison; one playlist row is selected.

For a fresh-load and restart check, use an isolated directory without an existing
`godamp.ini`, with the target skin copied into its `Skins` directory:

```sh
GODAMP_DATA_DIR="$godamp_test_data" GODAMP_CAPTURE_SKIN="XBOX_MasterAmp.wsz" \
  "$godamp_godot" --path . --rendering-method gl_compatibility \
  --audio-driver Dummy --log-file "$godamp_test_data/fresh.log" \
  res://Tests/ThemeCapture.tscn

GODAMP_DATA_DIR="$godamp_test_data" GODAMP_EXPECT_SKIN="XBOX_MasterAmp.wsz" \
  "$godamp_godot" --path . --rendering-method gl_compatibility \
  --audio-driver Dummy --log-file "$godamp_test_data/restart.log" \
  res://Tests/ThemeCapture.tscn
```

The second invocation asserts that the autoload restores the selected skin before
the application scene is created. Compare the regular and `restart-` captures
at each scale. Run the full switching sequence again and compare its output to
the restart captures to detect dependence on the previously selected skin.

## Verified scope and remaining checks

Milestones 1 and 2 are verified with the installed classic-skin collection on
macOS using Godot Mono 4.7.2 and the Compatibility renderer. The inspected
captures cover default restoration, colored playlist backgrounds and text,
system-font selection, oversized text sheets, and extended digits. Fresh-load,
switching, and restart PNGs match byte-for-byte at 1x and 2x, as do the initial
and restored default captures.

Present but cropped image sheets preserve their supplied pixels and pad omitted
areas transparently. Entirely absent sheets resolve to defaults, with the
classic balance-to-volume fallback. Unsupported bitmap characters do not gain
system-font substitutions; playlist text uses the selected system font with
font fallback enabled.

Playlist system fonts use grayscale antialiasing and rasterize at the current UI
zoom, with linear filtering on playlist labels. Bitmap display fonts and skin
artwork retain their pixel rendering. Font rendering is visually checked at 1x,
2x, and 4x with the default, Playstation-Amp, and Unreal skins.

Milestone 3 checks cover fixed main/EQ coordinates, slider endpoints and track
frames, toggle interaction states, menu and close-button atlas regions, and
active/inactive titlebar frames. The installed collection passes the control
suite at 1x and 2x. Native captures retain the playlist font smoothing.

These checks do not establish Windows/Linux behavior, packaged-build behavior,
window-region/cursor support, or analyzer/EQ graph support. Windowshade,
auto-EQ, and preset management are not interactive features. Native Winamp
screenshot comparisons and broader platform checks remain in later milestones.
