# Theme code review

## Scope

This review covers the theme implementation, font correction, regression and
capture runners, documentation, and generated namespace analyzer configuration.
The preimplementation checkpoint distinguishes these edits from existing Godot
resource serialization and engine-version changes.

## Reviewed files

| File | Review outcome |
| --- | --- |
| `Src/Autoload/SkinLoader.cs` | State preparation, fallback sources, atlas rollback, font scaling, signal lifecycle, and startup restoration reviewed; XML API contracts added. |
| `Src/Autoload/SettingsManager.cs` | Data-root override reads the environment once per lookup and documents its behavior. |
| `Src/Utils/SkinArchive.cs` | Image dictionary exposed through a read-only interface; asset limit named; unnecessary full-size image copies avoided; precedence and fallback tested. |
| `Src/Utils/SkinBitmapFont.cs` | Shared cell dimensions named; glyph and baseline contracts documented. Sprite-specific coordinates remain literal format data. |
| `Src/Data/PlaylistSkinStyle.cs` | Section-state naming clarified; parsing and font-fallback contracts documented; Roslyn collection suggestion applied. |
| `Src/Components/BitmapLabel.cs` | Number-font selection is an exported scene setting, independent of imported resource filenames. |
| `Src/Controls/MasterPanel/MasterPanel.tscn` | All four clock labels explicitly select the number font; positions and node references verified through rendering. |
| `Src/Controls/MasterPanel/MarqueeLabel.cs` | Base initialization, inherited signal cleanup, and empty-value handling reviewed; unused import removed and changed routines documented. |
| `Src/Controls/Playlist/Playlist.cs` | Background styling and subscription cleanup reviewed; Roslyn collection suggestions applied. |
| `Src/Controls/Playlist/PlaylistTrackEntry.cs` | Playing-track state and font size named; setup contract and styling documented; font antialiasing preserved. |
| `Tests/ThemeTests.cs` | Fixture and assertion contracts documented; duplicate-path precedence and image-resolution checks added. |
| `Tests/ThemeCapture.cs` | Repeated scale loops consolidated; layout-settling delay named; capture and assertion contracts documented. |
| `Tests/ThemeTests.tscn`, `Tests/ThemeCapture.tscn`, new script UID files | Scene references and UID files checked; runners execute successfully. |
| `GodAmp.csproj` | Generated namespace-root configuration is portable and included in design-time analysis. |
| `Docs/THEME_MILESTONES.md`, `Docs/THEME_TESTING.md` | Implementation references and verified scale descriptions corrected. |

## Verification

- Roslyn style and analyzer checks include informational suggestions on edited C# files.
- Build and XML documentation generation validate compilation and documentation syntax.
- Generated-fixture regression checks pass, including rejected-load preservation.
- Default and restored-default captures pass at 1x, 2x, and 4x.
- The default 4x PNG matches the font-correction baseline byte-for-byte.

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
- The existing `MasterPanel._Process` nullable warning and window-focus teardown
  diagnostic remain outside these edits. Unrelated Roslyn suggestions remain in
  untouched files.
- This review does not establish packaged-build or cross-platform compatibility;
  those checks remain in the theme milestones.
