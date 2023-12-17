// <copyright file="Mod.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace ImageOverlay
{
    using System.IO;
    using System.Reflection;
    using Colossal.IO.AssetDatabase;
    using Colossal.Logging;
    using Game;
    using Game.Modding;

    /// <summary>
    /// The base mod class for instantiation by the game.
    /// </summary>
    public sealed class Mod : IMod
    {
        /// <summary>
        /// The mod's default name.
        /// </summary>
        public const string ModName = "Image Overlay";

        // Mod assembly path cache.
        private string s_assemblyPath = null;

        /// <summary>
        /// Gets the active instance reference.
        /// </summary>
        public static Mod Instance { get; private set; }

        /// <summary>
        /// Gets the mod directory file path of the currently executing mod assembly.
        /// </summary>
        public string AssemblyPath
        {
            get
            {
                // Update cached path if the existing one is invalid.
                if (string.IsNullOrWhiteSpace(s_assemblyPath))
                {
                    // No path cached - find current executable asset.
                    string assemblyName = Assembly.GetExecutingAssembly().FullName;
                    ExecutableAsset modAsset = AssetDatabase.global.GetAsset(SearchFilter<ExecutableAsset>.ByCondition(x => x.definition?.FullName == assemblyName));
                    if (modAsset is null)
                    {
                        Log.Error("mod executable asset not found");
                        return null;
                    }

                    // Update cached path.
                    s_assemblyPath = Path.GetDirectoryName(modAsset.GetMeta().path);
                }

                // Return cached path.
                return s_assemblyPath;
            }
        }

        /// <summary>
        /// Gets the mod's active log.
        /// </summary>
        internal ILog Log { get; private set; }

        /// <summary>
        /// Called by the game when the mod is loaded.
        /// </summary>
        public void OnLoad()
        {
            // Set instance reference.
            Instance = this;

            // Initialize logger.
            Log = LogManager.GetLogger(ModName);
#if DEBUG
            Log.Info("setting logging level to Debug");
            Log.effectivenessLevel = Level.Debug;
#endif
            Log.Info("loading");
        }

        /// <summary>
        /// Called by the game when the game world is created.
        /// </summary>
        /// <param name="updateSystem">Game update system.</param>
        public void OnCreateWorld(UpdateSystem updateSystem)
        {
            Log.Info("starting OnCreateWorld");
            updateSystem.UpdateAt<ImageOverlaySystem>(SystemUpdatePhase.ToolUpdate);
        }

        /// <summary>
        /// Called by the game when the mod is disposed of.
        /// </summary>
        public void OnDispose()
        {
            Log.Info("disposing");
        }
    }
}
