using System.Collections.Generic;
using System.IO;
using Godot;
using GodAmp.Skins;

namespace GodAmp.Presentation.Components;

/// <summary>Routes application menu commands and populates the installed-skin submenu.</summary>
public partial class WinampMenuButton : MenuButton
{
    [Signal] public delegate void ToggleEqualizerRequestedEventHandler();
    [Signal] public delegate void TogglePlaylistRequestedEventHandler();
    [Signal] public delegate void ToggleVisualizerRequestedEventHandler();
    [Signal] public delegate void ZoomModeRequestedEventHandler(int multiplier);
    private const int EqualizerItemId = 100;
    private const int PlaylistItemId = 101;
    private const int VisualizerItemId = 102;
    private const int ScaleItemId = 0;
    private const int SkinsItemId = 1;

    private PopupMenu _popup = null!;
    [Export] private PopupMenu _scaleSubmenu = null!;
    [Export] private PopupMenu _skinSubmenu = null!;
    private readonly List<string> _skinFilenames = [];

    /// <inheritdoc />
    public override void _Ready()
    {
        _popup = GetPopup();
        SkinCursorController.UseSystemCursorFor(_popup);
        _popup.HideOnCheckableItemSelection = false;

        AttachSubmenu(ScaleItemId, _scaleSubmenu);
        AttachSubmenu(SkinsItemId, _skinSubmenu);

        _popup.IdPressed += OnPopupItemPressed;
    }

    public void SetEqualizerChecked(bool value) => _popup.SetItemChecked(_popup.GetItemIndex(EqualizerItemId), value);
    public void SetPlaylistChecked(bool value) => _popup.SetItemChecked(_popup.GetItemIndex(PlaylistItemId), value);
    public void SetVisualizerChecked(bool value) => _popup.SetItemChecked(_popup.GetItemIndex(VisualizerItemId), value);

    private void OnPopupItemPressed(long id)
    {
        switch (id)
        {
            case EqualizerItemId:
                EmitSignal(SignalName.ToggleEqualizerRequested);
                break;
            case PlaylistItemId:
                EmitSignal(SignalName.TogglePlaylistRequested);
                break;
            case VisualizerItemId:
                EmitSignal(SignalName.ToggleVisualizerRequested);
                break;
        }
    }

    /// <summary>Attaches an authored submenu to the popup created internally by MenuButton.</summary>
    /// <param name="id">Scene-authored parent item identifier.</param>
    /// <param name="submenu">Scene-authored submenu with its own static signal connections.</param>
    private void AttachSubmenu(int id, PopupMenu submenu)
    {
        submenu.Reparent(_popup);
        _popup.SetItemSubmenuNode(_popup.GetItemIndex(id), submenu);
        SkinCursorController.UseSystemCursorFor(submenu);
    }

    private void RefreshSkinList()
    {
        _skinSubmenu.Clear();
        _skinFilenames.Clear();

        var subId = 0;
        _skinSubmenu.AddItem("Open Skins directory", subId++);
        _skinSubmenu.AddSeparator();
        subId++;

        _skinSubmenu.AddItem("Default", subId++);

        var availableSkins = SkinLoader.GetAvailableSkins();
        foreach (var skinFile in availableSkins)
        {
            string displayName = Path.GetFileNameWithoutExtension(skinFile);
            _skinSubmenu.AddItem(displayName, subId++);
            _skinFilenames.Add(skinFile);
        }
    }

    private void OnScaleMenuItemPressed(long id)
    {
        int multiplier = (int)id;
        EmitSignal(SignalName.ZoomModeRequested, multiplier);
    }

    private void OnSkinMenuItemPressed(long index)
    {
        switch (index)
        {
            case 0:
                string skinsDir = SkinLoader.GetSkinsDirectory();
                if (!string.IsNullOrEmpty(skinsDir))
                {
                    OS.ShellOpen(skinsDir);
                }
                break;
            case 1:
                break;
            case 2:
                SkinLoader.RestoreOriginalSkin();
                break;
            default:
                int skinIndex = (int)index - 3;
                if (skinIndex >= 0 && skinIndex < _skinFilenames.Count)
                {
                    string skinFile = _skinFilenames[skinIndex];
                    string skinPath = Path.Combine(SkinLoader.GetSkinsDirectory(), skinFile);
                    SkinLoader.Load(skinPath);
                }
                break;
        }
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        _popup.IdPressed -= OnPopupItemPressed;
    }
}
