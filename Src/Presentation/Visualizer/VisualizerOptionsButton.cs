using System.Collections.Generic;
using GodAmp.Presentation.Components;
using Godot;

namespace GodAmp.Presentation.Visualizer;

/// <summary>Presents discovered visualization strategies in a scene-authored menu.</summary>
public partial class VisualizerOptionsButton : MenuButton
{
    private const int VisualizationItemIndex = 0;
    private const int ContextMenuMinimumWidth = 150;
    [Signal] public delegate void VizChangedEventHandler(StringName vizId);

    private readonly List<StringName> _vizIds = [];
    private PopupMenu _popup = null!;
    [Export] private PopupMenu _vizSubmenu = null!;
    private int _currentVizIndex = 0;

    /// <inheritdoc />
    public override void _Ready()
    {
        _popup = GetPopup();
        SkinCursorController.UseSystemCursorFor(_popup);
        _popup.AboutToPopup += OnAboutToPopup;
        _popup.HideOnCheckableItemSelection = false;

        /* MenuButton creates its popup internally; the authored submenu must be its child. */
        _vizSubmenu.Reparent(_popup);
        _popup.SetItemSubmenuNode(VisualizationItemIndex, _vizSubmenu);
        SkinCursorController.UseSystemCursorFor(_vizSubmenu);
    }

    private void OnAboutToPopup()
    {
        _popup.Size = new Vector2I(ContextMenuMinimumWidth, 0);
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

    /// <summary>Populates the authored submenu from the available visualization strategies.</summary>
    /// <param name="strategyTypes">Strategies whose identifiers are emitted on selection.</param>
    public void Initialize(IEnumerable<VisualizerStrategyType> strategyTypes)
    {
        _vizSubmenu.Clear();
        _vizIds.Clear();
        _currentVizIndex = 0;

        var subId = 0;
        foreach (var strategy in strategyTypes)
        {
            _vizSubmenu.AddItem(strategy.DisplayName, subId);
            _vizSubmenu.SetItemAsCheckable(subId, true);
            _vizIds.Add(strategy.Id);
            subId++;
        }

        if (_vizSubmenu.ItemCount > 0)
            _vizSubmenu.SetItemChecked(0, true);
    }

    private void OnVizMenuItemPressed(long index)
    {
        _currentVizIndex = (int)index;
        UpdateCheckedItem();
        EmitSignal(SignalName.VizChanged, _vizIds[_currentVizIndex]);
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        _popup.AboutToPopup -= OnAboutToPopup;
    }
}
