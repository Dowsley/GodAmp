using System.Collections.Generic;
using GodAmp.Skins.Fonts;
using Godot;

using GodAmp.Skins.Regions;
using GodAmp.Skins.Cursors;

namespace GodAmp.Skins;

/// <summary>Complete prepared artwork and presentation resources for one skin application.</summary>
internal sealed record SkinState(IReadOnlyDictionary<string, Texture2D> Textures, FontFile TextFont,
    FontFile NumberFont, PlaylistSkinStyle PlaylistStyle, Font PlaylistFont,
    VisualizationPalette Palette, GenericTitleFont TitleFont, SkinRegions Regions,
    IReadOnlyDictionary<SkinCursorRole, SkinCursor> Cursors, bool EqualizerWindowshadeAvailable);
