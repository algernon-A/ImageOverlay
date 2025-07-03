// <copyright file="ModSettings.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace ImageOverlay
{
    using System.IO;
    using System.Linq;
    using Colossal.IO.AssetDatabase;
    using Colossal.Logging;
    using Game.Input;
    using Game.Modding;
    using Game.Settings;
    using Game.UI;
    using Game.UI.Widgets;
    using UnityEngine;
    using static ActionNames;

    /// <summary>
    /// The mod's settings.
    /// </summary>
    [FileLocation(Mod.ModName)]
    [SettingsUISection(OverlayTab, KeysTab)]
    [SettingsUITabOrder(OverlayTab, KeysTab)]
    [SettingsUIGroupOrder(FileSelectionSection, AlphaSection, ToggleSection, ElevationSection, PositionSection, RotationSection, SizeSection)]
    [SettingsUIShowGroupName(ElevationSection, PositionSection, RotationSection, SizeSection)]
    public class ModSettings : ModSetting
    {
        // Layout constants.
        private const string OverlayTab = "Overlay";
        private const string KeysTab = "Keys";
        private const string FileSelectionSection = "FileSelection";
        private const string AlphaSection = "FileSelection";
        private const string ToggleSection = "Toggle";
        private const string ElevationSection = "OverlayElevation";
        private const string PositionSection = "OverlayPosition";
        private const string RotationSection = "OverlayRotation";
        private const string SizeSection = "OverlaySize";

        // Control constants.
        private const string NoOverlayText = "None";
        private const float VanillaMapSize = 14336f;
        private const float ExpandedMapSize = VanillaMapSize * 4f;

        // References.
        private readonly ILog _log;
        private readonly string _directoryPath;

        // Overlay file selection.
        private string[] _fileList;
        private string _selectedOverlay = string.Empty;
        private int _fileListVersion = 0;

        // Overlay attributes.
        private bool _showThroughTerrain = true;
        private float _overlaySize = VanillaMapSize;
        private float _overlayPosX = 0f;
        private float _overlayPosZ = 0f;
        private float _overlayElevation = 0f;
        private float _overlayRotation = 0f;
        private float _alpha = 0f;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModSettings"/> class.
        /// </summary>
        /// <param name="mod"><see cref="IMod"/> instance.</param>
        public ModSettings(IMod mod)
            : base(mod)
        {
            _log = Mod.Instance.Log;
            _directoryPath = Path.Combine(Application.persistentDataPath, "Overlays");
            UpdateFileList();
        }

        /// <summary>
        /// Gets or sets the selected overlay file.
        /// </summary>
        [SettingsUIDropdown(typeof(ModSettings), nameof(GenerateFileSelectionItems))]
        [SettingsUIValueVersion(typeof(ModSettings), nameof(GetListVersion))]
        [SettingsUISection(OverlayTab, FileSelectionSection)]
        public string SelectedOverlay
        {
            get
            {
                // Return default no overlay text if there's no overlays.
                if (_fileList is null || _fileList.Length == 0)
                {
                    return string.Empty;
                }

                // Check for validity of currently selected filename.
                if (string.IsNullOrEmpty(_selectedOverlay) || !_fileList.Contains(_selectedOverlay))
                {
                    // Invalid overlay selected; reset it to the first file on the list.
                    _selectedOverlay = _fileList[0];
                }

                return _selectedOverlay;
            }

            set
            {
                if (_selectedOverlay != value)
                {
                    Mod.Instance.Log.Info($"Updating selected overlay to ${value}");
                    _selectedOverlay = value;
                    ImageOverlaySystem.Instance?.UpdateOverlay();
                }
            }
        }

        /// <summary>
        /// Sets a value indicating whether the list of overlay files should be refreshed.
        /// </summary>
        [SettingsUIButton]
        [SettingsUISection(OverlayTab, FileSelectionSection)]
        public bool RefreshFileList
        {
            set
            {
                UpdateFileList();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the overlay should be confined to a flat plane.
        /// </summary>
        [SettingsUISection(OverlayTab, AlphaSection)]
        public bool ShowThroughTerrain
        {
            get => _showThroughTerrain;

            set
            {
                _showThroughTerrain = value;
                ImageOverlaySystem.Instance?.ShowThroughTerrain(value);
            }
        }

        /// <summary>
        /// Gets or sets the overlay alpha.
        /// </summary>
        [SettingsUISlider(min = 0f, max = 95f, step = 5f, scalarMultiplier = 100f, unit = Unit.kPercentage)]
        [SettingsUISection(OverlayTab, AlphaSection)]
        public float Alpha
        {
            get => _alpha;
            set
            {
                if (_alpha != value)
                {
                    _alpha = value;
                    ImageOverlaySystem.Instance?.SetAlpha(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the overlay size.
        /// </summary>
        [SettingsUISlider(min = 100f, max = ExpandedMapSize, step = 1f, scalarMultiplier = 1f)]
        [SettingsUISection(OverlayTab, SizeSection)]
        public float OverlaySize
        {
            get => _overlaySize;
            set
            {
                if (_overlaySize != value)
                {
                    _overlaySize = value;
                    ImageOverlaySystem.Instance?.SetSize(value);
                }
            }
        }

        /// <summary>
        /// Sets a value indicating whether the overlay size should be reset to default.
        /// </summary>
        [SettingsUIButton]
        [SettingsUISection(OverlayTab, SizeSection)]
        public bool ResetToVanilla
        {
            set => OverlaySize = VanillaMapSize;
        }

        /// <summary>
        /// Gets or sets the overlay Y-position (actually Z in Unity-speak, but let's not confuse the users too much).
        /// </summary>
        [SettingsUISlider(min = -ExpandedMapSize / 2f, max = ExpandedMapSize / 2f, step = 1f, scalarMultiplier = 1f)]
        [SettingsUISection(OverlayTab, PositionSection)]
        public float OverlayPosX
        {
            get => _overlayPosX;
            set
            {
                if (_overlayPosX != value)
                {
                    _overlayPosX = value;
                    ImageOverlaySystem.Instance?.SetPositionX(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the overlay Z-position.
        /// </summary>
        [SettingsUISlider(min = -ExpandedMapSize / 2f, max = ExpandedMapSize / 2f, step = 1f, scalarMultiplier = 1f)]
        [SettingsUISection(OverlayTab, PositionSection)]
        public float OverlayPosZ
        {
            get => _overlayPosZ;
            set
            {
                if (_overlayPosZ != value)
                {
                    _overlayPosZ = value;
                    ImageOverlaySystem.Instance?.SetPositionZ(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the overlay's elevation.
        /// </summary>
        [SettingsUISlider(min = -1000, max = 4000, step = 1f, scalarMultiplier = 1f)]
        [SettingsUISection(OverlayTab, PositionSection)]
        public float OverlayPosY
        {
            get => _overlayElevation;
            set
            {
                _overlayElevation = value;
                ImageOverlaySystem.Instance?.SetPositionY(value);
            }
        }

        /// <summary>
        /// Sets a value indicating whether the overlay position should be reset to default.
        /// </summary>
        [SettingsUIButton]
        [SettingsUISection(OverlayTab, PositionSection)]
        public bool ResetPosition
        {
            set
            {
                OverlayPosX = 0f;
                OverlayPosZ = 0f;

                // Reset elevation to 5m above the surface level at the exact centre of the map.
                if (ImageOverlaySystem.Instance is ImageOverlaySystem imageOverlaySystem)
                {
                    imageOverlaySystem.ResetElevation();
                }
                else
                {
                    OverlayPosY = 5f;
                }
            }
        }

        /// <summary>
        /// Gets or sets the overlay rotation.
        /// </summary>
        [SettingsUISlider(min = -180f, max = 180f, step = 1f, scalarMultiplier = 1f)]
        [SettingsUISection(OverlayTab, RotationSection)]
        public float OverlayRotation
        {
            get => _overlayRotation;
            set
            {
                if (_overlayRotation != value)
                {
                    _overlayRotation = value;

                    // Bounds check.
                    if (_overlayRotation < -180f)
                    {
                        _overlayRotation += 360f;
                    }

                    if (_overlayRotation > 180f)
                    {
                        _overlayRotation -= 360f;
                    }

                    // Update any existing overlay.
                    ImageOverlaySystem.Instance?.UpdateRotation();
                }
            }
        }

        /// <summary>
        /// Sets a value indicating whether the overlay rotation should be reset to default.
        /// </summary>
        [SettingsUIButton]
        [SettingsUISection(OverlayTab, RotationSection)]
        public bool ResetRotation
        {
            set
            {
                OverlayRotation = 0f;
            }
        }

        /// <summary>
        /// Gets or sets the 'show overlay' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.O, ToggleAction, ctrl: true)]
        [SettingsUISection(KeysTab, ToggleSection)]
        public ProxyBinding ToggleBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'move north' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.PageUp, MoveUpAction, ctrl: true)]
        [SettingsUISection(KeysTab, ElevationSection)]
        public ProxyBinding MoveUpBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'move south' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.PageDown, MoveDownAction, ctrl: true)]
        [SettingsUISection(KeysTab, ElevationSection)]
        public ProxyBinding MoveDownBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'move north large' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.PageUp, MoveUpLargeAction, ctrl: true, shift: true)]
        [SettingsUISection(KeysTab, ElevationSection)]
        public ProxyBinding MoveUpLargeBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'move south large' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.PageDown, MoveDownLargeAction, ctrl: true, shift: true)]
        [SettingsUISection(KeysTab, ElevationSection)]
        public ProxyBinding MoveDownLargeBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'move north' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.UpArrow, MoveNorthAction, ctrl: true)]
        [SettingsUISection(KeysTab, PositionSection)]
        public ProxyBinding MoveNorthBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'move south' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.DownArrow, MoveSouthAction, ctrl: true)]
        [SettingsUISection(KeysTab, PositionSection)]
        public ProxyBinding MoveSouthBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'move east' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.RightArrow, MoveEastAction, ctrl: true)]
        [SettingsUISection(KeysTab, PositionSection)]
        public ProxyBinding MoveEastBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'move west' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.LeftArrow, MoveWestAction, ctrl: true)]
        [SettingsUISection(KeysTab, PositionSection)]
        public ProxyBinding MoveWestBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'move north large' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.UpArrow, MoveNorthLargeAction, ctrl: true, shift: true)]
        [SettingsUISection(KeysTab, PositionSection)]
        public ProxyBinding MoveNorthLargeBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'move south large' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.DownArrow, MoveSouthLargeAction, ctrl: true, shift: true)]
        [SettingsUISection(KeysTab, PositionSection)]
        public ProxyBinding MoveSouthLargeBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'move east large' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.RightArrow, MoveEastLargeAction, ctrl: true, shift: true)]
        [SettingsUISection(KeysTab, PositionSection)]
        public ProxyBinding MoveEastLargeBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'move east large' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.LeftArrow, MoveWestLargeAction, ctrl: true, shift: true)]
        [SettingsUISection(KeysTab, PositionSection)]
        public ProxyBinding MoveWestLargeBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'rotate left' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.Comma, RotateLeftAction, ctrl: true)]
        [SettingsUISection(KeysTab, RotationSection)]
        public ProxyBinding RotateLeftBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'rotate right' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.Period, RotateRightAction, ctrl: true)]
        [SettingsUISection(KeysTab, RotationSection)]
        public ProxyBinding RotateRightBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'rotate left large' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.Comma, RotateLeftLargeAction, ctrl: true, shift: true)]
        [SettingsUISection(KeysTab, RotationSection)]
        public ProxyBinding RotateLeftLargeBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'rotate right large' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.Period, RotateRightLargeAction, ctrl: true, shift: true)]
        [SettingsUISection(KeysTab, RotationSection)]
        public ProxyBinding RotateRightLargeBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'increase size' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.Equals, IncreaseSizeAction, ctrl: true)]
        [SettingsUISection(KeysTab, SizeSection)]
        public ProxyBinding IncreaseSizeBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'decrease size' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.Minus, DecreaseSizeAction, ctrl: true)]
        [SettingsUISection(KeysTab, SizeSection)]
        public ProxyBinding DecreaseSizeBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'increase size large' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.Equals, IncreaseSizeLargeAction, ctrl: true, shift: true)]
        [SettingsUISection(KeysTab, SizeSection)]
        public ProxyBinding IncreaseSizeLargeBinding { get; set; }

        /// <summary>
        /// Gets or sets the 'decrease size large' hotkey.
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.Minus, DecreaseSizeLargeAction, ctrl: true, shift: true)]
        [SettingsUISection(KeysTab, SizeSection)]
        public ProxyBinding DecreaseSizeLargeBinding { get; set; }

        /// <summary>
        /// Generates the overlay file selection dropdown menu item list.
        /// </summary>
        /// <returns>List of file selection dropdown menu items with trimmed filenames as the display value.</returns>
        public DropdownItem<string>[] GenerateFileSelectionItems()
        {
            // If no files, just return a single "None" entry.
            if (_fileList is null || _fileList.Length == 0)
            {
                return new DropdownItem<string>[]
                {
                    new ()
                    {
                        value = string.Empty,
                        displayName = NoOverlayText,
                    },
                };
            }

            // Generate menu list of files (with trimmed names as visible menu items).
            DropdownItem<string>[] items = new DropdownItem<string>[_fileList.Length];
            for (int i = 0; i < _fileList.Length; ++i)
            {
                items[i] = new DropdownItem<string>
                {
                    value = _fileList[i],
                    displayName = Path.GetFileNameWithoutExtension(_fileList[i]),
                };
            }

            return items;
        }

        /// <summary>
        /// Gets the current version of the file list.
        /// </summary>
        /// <returns>Current file list version.</returns>
        public int GetListVersion() => _fileListVersion;

        /// <summary>
        /// Restores mod settings to default.
        /// </summary>
        public override void SetDefaults()
        {
            _selectedOverlay = string.Empty;
            OverlaySize = VanillaMapSize;
            Alpha = 0f;
        }

        /// <summary>
        /// Updates the list of available overlay files.
        /// </summary>
        internal void UpdateFileList()
        {
            // Create overlay directory if it doesn't already exist.
            if (!Directory.Exists(_directoryPath))
            {
                Directory.CreateDirectory(_directoryPath);
            }

            _log.Info("refreshing overlay file list using directory " + _directoryPath);
            _fileList = Directory.GetFiles(_directoryPath, "*.png", SearchOption.TopDirectoryOnly);
            foreach (string file in _fileList)
            {
                _log.Info($"    Found file: ${file})");
            }

            ++_fileListVersion;
        }
    }
}