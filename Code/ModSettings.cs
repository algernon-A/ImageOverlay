// <copyright file="ModSettings.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace ImageOverlay
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Colossal.IO.AssetDatabase;
    using Game.Modding;
    using Game.Settings;
    using Game.UI.Widgets;

    /// <summary>
    /// The mod's settings.
    /// </summary>
    [FileLocation(Mod.ModName)]
    public class ModSettings : ModSetting
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModSettings"/> class.
        /// </summary>
        /// <param name="mod"><see cref="IMod"/> instance.</param>
        public ModSettings(IMod mod)
            : base(mod)
        {
        }

        [SettingsUIDropdown(typeof(ModSettings), nameof(GetStringDropdownItems))]
        [SettingsUISection("OverlayFile")]
        public string StringDropdown { get; set; } = "None";


        public DropdownItem<string>[] GetStringDropdownItems()
        {
            Mod.Instance.Log.Info(Mod.Instance.AssemblyPath);

            string[] files = Directory.GetFiles(Mod.Instance.AssemblyPath, "*.png", SearchOption.TopDirectoryOnly);
            Mod.Instance.Log.Info(files.Length);

            if (files.Length == 0)
            {
                return new DropdownItem<string>[]
                {
                    new DropdownItem<string>
                    {
                        value = string.Empty,
                        displayName = "None",
                    },
                };
            }
            else
            {

            }
            DropdownItem<string>[] items = new DropdownItem<string>[files.Length];
            for (int i = 0; i < files.Length; ++i)
            {
                items[i] = new DropdownItem<string>
                {
                    value = files[i],
                    displayName = Path.GetFileNameWithoutExtension(files[i]),
                };
            }

            return items;
        }

        /// <summary>
        /// Restores mod settings to default.
        /// </summary>
        public override void SetDefaults()
        {
        }
    }
}