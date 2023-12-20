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
    using Game.UI.Widgets;
    using UnityEngine;

    /// <summary>
    /// The mod's settings.
    /// </summary>
    [FileLocation(Mod.ModName)]
    public class ModSettings : ModSetting
    {
        private const string NoOverlayText = "None";

        private readonly ILog _log;
        private readonly string _directoryPath;

        // Overlay file selection.
        private string[] _fileList;
        private string _selectedOverlay = string.Empty;
        private int _fileListVersion = 0;

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
                _selectedOverlay = value;

                ImageOverlaySystem.Instance?.UpdateOverlay();
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