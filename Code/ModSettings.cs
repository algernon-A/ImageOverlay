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
    using Game.Modding;
    using Game.Settings;
    using Game.UI;
    using Game.UI.Widgets;
    using UnityEngine;

    /// <summary>
    /// The mod's settings.
    /// </summary>
    [FileLocation(Mod.ModName)]
    public class ModSettings : ModSetting
    {
        private const string NoOverlayText = "None";
        private const float VanillaMapSize = 14336f;

        // References.
        private readonly ILog _log;
        private readonly string _directoryPath;

        // Overlay file selection.
        private string[] _fileList;
        private string _selectedOverlay = string.Empty;
        private int _fileListVersion = 0;

        // Overlay attributes.
        private float _overlaySize = VanillaMapSize;
        private float _overlayPosX = 0f;
        private float _overlayPosZ = 0f;
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
        [SettingsUISection("FileSelection")]
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
                    _selectedOverlay = value;
                    ImageOverlaySystem.Instance?.UpdateOverlay();
                }
            }
        }

        /// <summary>
        /// Sets a value indicating whether the list of overlay files should be refreshed.
        /// </summary>
        [SettingsUIButton]
        [SettingsUISection("FileSelection")]
        public bool RefreshFileList
        {
            set
            {
                UpdateFileList();
            }
        }

        /// <summary>
        /// Gets or sets the overlay size.
        /// </summary>
        [SettingsUISlider(min = VanillaMapSize / 4f, max = VanillaMapSize * 4f, step = 1f, scalarMultiplier = 1f)]
        [SettingsUISection("OverlaySize")]
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
        [SettingsUISection("OverlaySize")]
        public bool ResetToVanilla
        {
            set => OverlaySize = VanillaMapSize;
        }

        /// <summary>
        /// Gets or sets the overlay Y-position (actually Z in Unity-speak, but let's not confuse the users too much).
        /// </summary>
        [SettingsUISlider(min = -VanillaMapSize / 2f, max = VanillaMapSize / 2f, step = 1f, scalarMultiplier = 1f)]
        [SettingsUISection("OverlayPosition")]
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
        [SettingsUISlider(min = -VanillaMapSize / 2f, max = VanillaMapSize / 2f, step = 1f, scalarMultiplier = 1f)]
        [SettingsUISection("OverlayPosition")]
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
        /// Sets a value indicating whether the overlay position should be reset to default.
        /// </summary>
        [SettingsUIButton]
        [SettingsUISection("OverlayPosition")]
        public bool ResetPosition
        {
            set
            {
                OverlayPosX = 0f;
                OverlayPosZ = 0f;
            }
        }

        /// <summary>
        /// Gets or sets the overlay alpha.
        /// </summary>
        [SettingsUISlider(min =0f, max = 95f, step = 5f, scalarMultiplier = 100f, unit = Unit.kPercentage)]
        [SettingsUISection("Alpha")]
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
        private void UpdateFileList()
        {
            // Create overlay directory if it doesn't already exist.
            if (!Directory.Exists(_directoryPath))
            {
                Directory.CreateDirectory(_directoryPath);
            }

            _log.Info("refreshing overlay file list using directory " + _directoryPath);
            _fileList = Directory.GetFiles(_directoryPath, "*.png", SearchOption.TopDirectoryOnly);
            _log.Debug(_fileList.Length);
            ++_fileListVersion;
        }
    }
}