# Theme code review

## Scope

This review covers skin loading, text, classic controls, regression and capture
runners, documentation, and generated namespace analyzer configuration.

## Reviewed files

| File | Review outcome |
| --- | --- |
| `Src/Autoload/SkinLoader.cs` | State preparation, fallback sources, atlas rollback, font scaling, signal lifecycle, and startup restoration reviewed; XML API contracts added. |
| `Src/Autoload/SettingsManager.cs` | Data-root override reads the environment once per lookup and documents its behavior. |
| `Src/Utils/SkinArchive.cs` | Image dictionary exposed through a read-only interface; asset limit named; unnecessary full-size image copies avoided; precedence and fallback tested. |
| `Src/Utils/SkinBitmapFont.cs` | Shared cell dimensions named; glyph and baseline contracts documented. Sprite-specific coordinates remain literal format data. |
| `Src/Data/PlaylistSkinStyle.cs` | Section-state naming clarified; parsing and font-fallback contracts documented; Roslyn collection suggestion applied. |
| `Src/Components/BitmapLabel.cs` | Number-font selection is an exported scene setting, independent of imported resource filenames. |
| `Src/Controls/MasterPanel/MasterPanel.cs` and `.tscn` | Fixed reference rectangles, custom slider signals, menu states, nullable stream access, and retained clock/font settings reviewed. |
| `Src/Components/SkinSlider.cs` | Rendering and hit testing share thumb geometry; dragging, disabled input, focus cancellation, and skin subscriptions reviewed. Sheet coordinates are literal format data; travel and frame constants are named. |
| `Src/Components/SkinToggleButton.cs` | Separate latched/held state, cancelled presses, disabled controls, focus loss, and programmatic state restoration reviewed. Display atlases are private to each control. |
| `Src/Components/WindowPanelContainer.cs` | Titlebar focus subscriptions have matching cleanup; active regions are restored on exit; titlebar buttons cannot start a window drag. |
| `Src/Core/WindowManager.cs` and `Src/Main.tscn` | Each native window owns its focus; fixed docking signals are serialized in the application scene. |
| `Src/Components/SkinSlider.tscn` and `SkinToggleButton.tscn` | Fixed self-signals are serialized in reusable scenes and inherited by control instances. Code subscriptions are limited to the runtime hosting window and external skin autoload. |
| `Src/Controls/Equalizer/Equalizer.cs`, `.tscn`, and `EqualizerBand.tscn` | Shared band scene, fixed control coordinates, and bound band indices avoid duplicated slider implementations and handlers. Audio connections are verified against the effects. |
| `Data/SkinResources/` control atlases | EQ on/auto boundaries, seek thumb, menu artwork, and playlist/generic close states checked against the local Winamp renderer. Disabled controls use released artwork. |
| `Src/Controls/Playlist/Playlist.tscn` and `Src/Visualizer/Visualizer.tscn` | Active/inactive titlebar segments and close-button geometry reviewed. Playlist cleanup calls the inherited focus cleanup. |
| `Src/Controls/MasterPanel/MarqueeLabel.cs` | Base initialization, inherited signal cleanup, and empty-value handling reviewed; unused import removed and changed routines documented. |
| `Src/Controls/Playlist/Playlist.cs` | Background styling and subscription cleanup reviewed; Roslyn collection suggestions applied. |
| `Src/Controls/Playlist/PlaylistTrackEntry.cs` | Playing-track state and font size named; setup contract and styling documented; font antialiasing preserved. |
| `Tests/ThemeTests.cs` | Fixture and assertion contracts documented; duplicate-path precedence and image-resolution checks added. |
| `Tests/ThemeCapture.cs` | Shared scale loop, named layout delay, explicit viewport redraw for covered native windows, and capture contracts reviewed. |
| `Tests/ControlTests.cs` and `.tscn` | Real-scene geometry and audio wiring assertions complement viewport input tests for endpoints, toggle states, cancellation, disabled controls, and focus signals. |
| `Tests/ThemeTests.tscn`, `Tests/ThemeCapture.tscn`, new script UID files | Scene references and UID files checked; runners execute successfully. |
| `GodAmp.csproj` | Generated namespace-root configuration is portable and included in design-time analysis. |
| `Docs/THEME_MILESTONES.md`, `Docs/THEME_TESTING.md` | Implementation references and verified scale descriptions corrected. |

## Verification

- Roslyn style and analyzer checks include informational suggestions on edited C# files.
- Build and XML documentation generation validate compilation and documentation syntax.
- Generated-fixture regression checks pass, including rejected-load preservation.
- Default and restored-default captures pass at 1x, 2x, and 4x.
- The installed skin collection passes control geometry and interaction checks at 1x and 2x.
- Obsolete horizontal/vertical slider scripts, their unused scenes, and the unused color helper have no remaining callers.

## Remaining findings

- `SettingsManager.SaveAllSettings` logs write failures without returning a result.
  A skin can apply successfully without persisting its selection. Handling this
  consistently requires an explicit settings-save error contract.
- The archive limit bounds each uncompressed entry's bytes, not its decoded image
  dimensions or aggregate allocation. Decoded-image limits belong in compatibility
  hardening before treating arbitrary downloaded skins as resource-bounded input.
- The namespace workaround overrides the analyzer-visible `ProjectDir` globally.
  IDE0130 skips files outside `Src`, including `Tests`; other analyzers consuming
  that property see the same override. MSBuild's actual project directory is unchanged.
- Unrelated Roslyn suggestions remain in untouched files.
- This review does not establish packaged-build or cross-platform compatibility;
  those checks remain in the theme milestones.
