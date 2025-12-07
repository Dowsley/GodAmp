using System.Collections.Generic;
using System.IO;
using Godot;
using GodAmp.Autoload;

namespace GodAmp.Components;

public partial class WinampMenuButton : MenuButton
{
	private PopupMenu _popup;
	private PopupMenu _scaleSubmenu;
	private PopupMenu _skinSubmenu;
	private readonly List<string> _skinFilenames = [];

	public override void _Ready()
	{
		_popup = GetPopup();

		
		var id = 0;
		SetupScaleMenu(id++);
		SetupSkinMenu(id);
	}

	private void SetupScaleMenu(int id)
	{
		_scaleSubmenu = new PopupMenu();
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

		_skinSubmenu.IndexPressed += OnSkinMenuItemPressed;

		_popup.AddSubmenuNodeItem("Skins", _skinSubmenu, id);
	}

	private static void OnScaleMenuItemPressed(long index)
	{
		int multiplier = (int)index;
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