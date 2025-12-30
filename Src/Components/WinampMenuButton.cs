using System.Collections.Generic;
using System.IO;
using Godot;
using GodAmp.Autoload;

namespace GodAmp.Components;

public partial class WinampMenuButton : MenuButton
{
    private const int EqualizerItemId = 100;
    private const int PlaylistItemId = 101;
    private const int VisualizerItemId = 102;

    private PopupMenu _popup = null!;
    private PopupMenu _scaleSubmenu = null!;
    private PopupMenu _skinSubmenu = null!;
    private readonly List<string> _skinFilenames = [];

    public override void _Ready()
    {
        _popup = GetPopup();
        _popup.HideOnCheckableItemSelection = false;

        var id = 0;
        SetupScaleMenu(id++);
        SetupSkinMenu(id++);
        SetupWindowToggles();

        _popup.IdPressed += OnPopupItemPressed;
    }

    public void SetEqualizerChecked(bool value) => _popup.SetItemChecked(_popup.GetItemIndex(EqualizerItemId), value);
    public void SetPlaylistChecked(bool value) => _popup.SetItemChecked(_popup.GetItemIndex(PlaylistItemId), value);
    public void SetVisualizerChecked(bool value) => _popup.SetItemChecked(_popup.GetItemIndex(VisualizerItemId), value);

    private void SetupWindowToggles()
    {
        _popup.AddSeparator();
        _popup.AddCheckItem("Equalizer", EqualizerItemId);
        _popup.AddCheckItem("Playlist", PlaylistItemId);
        _popup.AddCheckItem("Visualizer", VisualizerItemId);

        _popup.SetItemChecked(_popup.GetItemIndex(EqualizerItemId), true);
        _popup.SetItemChecked(_popup.GetItemIndex(PlaylistItemId), true);
        _popup.SetItemChecked(_popup.GetItemIndex(VisualizerItemId), true);
    }

    private static void OnPopupItemPressed(long id)
    {
        switch (id)
        {
            case EqualizerItemId:
                SignalBus.Instance.EmitSignal(SignalBus.SignalName.ToggleEqualizerRequested);
                break;
            case PlaylistItemId:
                SignalBus.Instance.EmitSignal(SignalBus.SignalName.TogglePlaylistRequested);
                break;
            case VisualizerItemId:
                SignalBus.Instance.EmitSignal(SignalBus.SignalName.ToggleVisualizerRequested);
                break;
        }
    }

    private void SetupScaleMenu(int id)
    {
        _scaleSubmenu = new PopupMenu();
        _scaleSubmenu.HideOnCheckableItemSelection = false;
        _scaleSubmenu.Name = "ScaleSubmenu";

        _scaleSubmenu.AddItem("1x", 1);
        _scaleSubmenu.AddItem("2x", 2);
        _scaleSubmenu.AddItem("3x", 3);
        _scaleSubmenu.AddItem("4x", 4);

        _scaleSubmenu.IdPressed += OnScaleMenuItemPressed;

        _popup.AddSubmenuNodeItem("Scale UI", _scaleSubmenu, id);
    }

    private void SetupSkinMenu(int id)
    {
        _skinSubmenu = new PopupMenu();
        _skinSubmenu.Name = "SkinSubmenu";
        _skinSubmenu.AboutToPopup += RefreshSkinList;
        _skinSubmenu.IndexPressed += OnSkinMenuItemPressed;

        _popup.AddSubmenuNodeItem("Skins", _skinSubmenu, id);
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

    private static void OnScaleMenuItemPressed(long id)
    {
        int multiplier = (int)id;
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.ZoomModeRequested, multiplier);
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
}