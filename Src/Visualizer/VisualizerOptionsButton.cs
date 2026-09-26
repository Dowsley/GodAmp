using System.Collections.Generic;
using GodAmp.Components;
using GodAmp.Data;
using Godot;

namespace GodAmp.Visualizer;

public partial class VisualizerOptionsButton : MenuButton
{
    [Signal] public delegate void VizChangedEventHandler(StringName vizId);

    private readonly List<StringName> _vizIds = [];
    private PopupMenu _popup = null!;
    private PopupMenu _vizSubmenu = null!;
    private int _currentVizIndex = 0;

    /// <summary>Builds the runtime visualization menus and connects their selection handlers.</summary>
    /// <param name="strategyTypeMapRef">Available strategies in display order.</param>
    public void Initialize(List<VisualizerStrategyType> strategyTypeMapRef)
    {
        _popup = GetPopup();
        SkinCursorController.UseSystemCursorFor(_popup);
        _popup.AboutToPopup += OnAboutToPopup;
        _popup.HideOnCheckableItemSelection = false;

        SetupVizMenu(strategyTypeMapRef);
    }

    private void OnAboutToPopup()
    {
        _popup.Size = new Vector2I(150, 0);
        Vector2I mousePos = DisplayServer.MouseGetPosition();
        _popup.Position = mousePos;

        UpdateCheckedItem();
    }

    private void UpdateCheckedItem()
    {
        for (int i = 0; i < _vizSubmenu.ItemCount; i++)
        {
            _vizSubmenu.SetItemChecked(i, i == _currentVizIndex);
        }
    }

    /// <summary>Creates a checkable submenu from the available visualization strategies.</summary>
    /// <param name="strategyTypes">Strategies whose identifiers are emitted on selection.</param>
    private void SetupVizMenu(List<VisualizerStrategyType> strategyTypes)
    {
        _vizSubmenu = new PopupMenu();
        SkinCursorController.UseSystemCursorFor(_vizSubmenu);
        _vizSubmenu.HideOnCheckableItemSelection = false;
        _vizSubmenu.Name = "VizSubmenu";

        var subId = 0;
        foreach (var strategy in strategyTypes)
        {
            _vizSubmenu.AddItem(strategy.DisplayName, subId);
            _vizSubmenu.SetItemAsCheckable(subId, true);
            _vizIds.Add(strategy.Id);
            subId++;
        }

        _vizSubmenu.SetItemChecked(0, true);
        _vizSubmenu.IndexPressed += OnVizMenuItemPressed;
        _popup.AddSubmenuNodeItem("Visualizations", _vizSubmenu);
    }

    private void OnVizMenuItemPressed(long index)
    {
        _currentVizIndex = (int)index;
        UpdateCheckedItem();
        EmitSignal(SignalName.VizChanged, _vizIds[_currentVizIndex]);
    }
}
