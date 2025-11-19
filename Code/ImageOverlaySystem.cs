// <copyright file="ImageOverlaySystem.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace ImageOverlay
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using Colossal.Logging;
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Input;
    using Game.Simulation;
    using Unity.Mathematics;
    using UnityEngine;
    using static ActionNames;

    /// <summary>
    /// The historical start mod system.
    /// </summary>
    internal sealed partial class ImageOverlaySystem : GameSystemBase
    {
        // Input actions.
        private readonly List<KeyValuePair<ProxyAction, Action>> _actions = new ();

        // References.
        private ILog _log;

        // Texture.
        private GameObject _overlayObject;
        private Material _overlayMaterial;
        private Texture2D _overlayTexture;
        private Shader _overlayShader;
        private bool _isVisible = false;

        // Status flag.
        private bool _shaderInitialized = false;

        /// <summary>
        /// Gets the active instance.
        /// </summary>
        internal static ImageOverlaySystem Instance { get; private set; }

        /// <summary>
        /// Triggers a refresh of the current overlay (if any).
        /// </summary>
        internal void UpdateOverlay()
        {
            // Only refresh if there's an existing overlay object.
            if (_overlayObject)
            {
                UpdateOverlayTexture();
            }
        }

        /// <summary>
        /// Sets whether the overlay will be displayed through terrain.
        /// </summary>
        /// <param name="showThroughTerrain"><c>true</c> to have the image still appear through terrain, <c>false</c> to respect terrain opacity.</param>
        internal void ShowThroughTerrain(bool showThroughTerrain)
        {
            if (_overlayObject?.GetComponent<Renderer>()?.material is Material overlayMaterial)
            {
                overlayMaterial.SetFloat("_ZTest", showThroughTerrain ? 8f : 4f);
                _shaderInitialized = true;
            }
            else
            {
                _log.Info("Unable set ZTest: overlay material shader not yet ready.");
            }
        }

        /// <summary>
        /// Sets the overlay's alpha value.
        /// </summary>
        /// <param name="alpha">Alpha value to set (0f - 1f).</param>
        internal void SetAlpha(float alpha)
        {
            // Only update if there's an existing overlay object.
            if (_overlayObject)
            {
                // Invert alpha.
                _overlayObject.GetComponent<Renderer>().material.color = new Color(1f, 1f, 1f, 1f - alpha);
            }
        }

        /// <summary>
        /// Sets the overlay size.
        /// </summary>
        /// <param name="size">Size per size, in metres.</param>
        internal void SetSize(float size)
        {
            // Only refresh if there's an existing overlay object.
            if (_overlayObject)
            {
                // Plane primitive is 10m wide, so divide input size accordingly.
                float scaledSize = size / 10f;
                _overlayObject.transform.localScale = new Vector3(scaledSize, 1f, scaledSize);
            }
        }

        /// <summary>
        /// Sets the overlay's X-position.
        /// </summary>
        /// <param name="posX">X position, in metres.</param>
        internal void SetPositionX(float posX)
        {
            // Only refresh if there's an existing overlay object.
            if (_overlayObject)
            {
                Vector3 newPos = _overlayObject.transform.position;
                newPos.x = posX;
                _overlayObject.transform.position = newPos;
            }
        }

        /// <summary>
        /// Sets the overlay's elevation.
        /// </summary>
        /// <param name="elevation">Elevation, in metres.</param>
        internal void SetPositionY(float elevation)
        {
            // Only refresh if there's an existing overlay object.
            if (_overlayObject)
            {
                Vector3 newPos = _overlayObject.transform.position;
                newPos.y = elevation;
                _overlayObject.transform.position = newPos;
            }
        }

        /// <summary>
        /// Sets the overlay's Z-position.
        /// </summary>
        /// <param name="posZ">Z position, in metres.</param>
        internal void SetPositionZ(float posZ)
        {
            // Only refresh if there's an existing overlay object.
            if (_overlayObject)
            {
                Vector3 newPos = _overlayObject.transform.position;
                newPos.z = posZ;
                _overlayObject.transform.position = newPos;
            }
        }

        /// <summary>
        /// Resets the overlay elevation to 5m above the surface level at the exact centre of the map.
        /// </summary>
        internal void ResetElevation()
        {
            TerrainHeightData terrainHeight = World.GetOrCreateSystemManaged<TerrainSystem>().GetHeightData();
            WaterSurfaceData<SurfaceWater> waterSurface = World.GetOrCreateSystemManaged<WaterSystem>().GetSurfaceData(out _);
            Mod.Instance.ActiveSettings.OverlayPosY = WaterUtils.SampleHeight(ref waterSurface, ref terrainHeight, float3.zero) + 5f;
        }

        /// <summary>
        /// Updates the overlay's rotation to match current settings.
        /// </summary>
        internal void UpdateRotation()
        {
            // Only refresh if there's an existing overlay object.
            if (_overlayObject)
            {
                _overlayObject.transform.rotation = Quaternion.Euler(0f, Mod.Instance.ActiveSettings.OverlayRotation + 180, 0f);
            }
        }

        /// <summary>
        /// Called when the system is created.
        /// </summary>
        protected override void OnCreate()
        {
            base.OnCreate();

            // Set instance.
            Instance = this;

            // Set log.
            _log = Mod.Instance.Log;
            _log.Info("OnCreate");

            // Try to load shader.
            if (!LoadShader())
            {
                // Shader loading error; abort operation.
                return;
            }

            // Get input actions from settings.
            ModSettings activeSettings = Mod.Instance.ActiveSettings;

            // Assign input actions.
            _actions.Add(new (activeSettings.GetAction(ToggleAction), ToggleOverlay));
            _actions.Add(new (activeSettings.GetAction(MoveUpAction), () => { activeSettings.OverlayPosY += 1f; }));
            _actions.Add(new (activeSettings.GetAction(MoveDownAction), () => { activeSettings.OverlayPosY -= 1f; }));
            _actions.Add(new (activeSettings.GetAction(MoveUpLargeAction), () => { activeSettings.OverlayPosY += 10f; }));
            _actions.Add(new (activeSettings.GetAction(MoveDownLargeAction), () => { activeSettings.OverlayPosY -= 10f; }));
            _actions.Add(new (activeSettings.GetAction(MoveNorthAction), () => { activeSettings.OverlayPosZ += 1f; }));
            _actions.Add(new (activeSettings.GetAction(MoveSouthAction), () => { activeSettings.OverlayPosZ -= 1f; }));
            _actions.Add(new (activeSettings.GetAction(MoveEastAction), () => { activeSettings.OverlayPosX += 1f; }));
            _actions.Add(new (activeSettings.GetAction(MoveWestAction), () => { activeSettings.OverlayPosX -= 1f; }));
            _actions.Add(new (activeSettings.GetAction(MoveNorthLargeAction), () => { activeSettings.OverlayPosZ += 10f; }));
            _actions.Add(new (activeSettings.GetAction(MoveSouthLargeAction), () => { activeSettings.OverlayPosZ -= 10f; }));
            _actions.Add(new (activeSettings.GetAction(MoveEastLargeAction), () => { activeSettings.OverlayPosX += 10f; }));
            _actions.Add(new (activeSettings.GetAction(MoveWestLargeAction), () => { activeSettings.OverlayPosX -= 10f; }));
            _actions.Add(new (activeSettings.GetAction(RotateLeftAction), () => { activeSettings.OverlayRotation -= 1f; }));
            _actions.Add(new (activeSettings.GetAction(RotateRightAction), () => { activeSettings.OverlayRotation += 1f; }));
            _actions.Add(new (activeSettings.GetAction(RotateLeftLargeAction), () => { activeSettings.OverlayRotation -= 90f; }));
            _actions.Add(new (activeSettings.GetAction(RotateRightLargeAction), () => { activeSettings.OverlayRotation += 90f; }));
            _actions.Add(new (activeSettings.GetAction(IncreaseSizeAction), () => { activeSettings.OverlaySize += 10f; }));
            _actions.Add(new (activeSettings.GetAction(DecreaseSizeAction), () => { activeSettings.OverlaySize -= 10f; }));
            _actions.Add(new (activeSettings.GetAction(IncreaseSizeLargeAction), () => { activeSettings.OverlaySize += 100f; }));
            _actions.Add(new (activeSettings.GetAction(DecreaseSizeLargeAction), () => { activeSettings.OverlaySize -= 100f; }));

            _log.Info("Finished OnCreate");
        }

        /// <summary>
        /// Called when loading is complete.
        /// </summary>
        /// <param name="purpose">Loading purpose.</param>
        /// <param name="mode">Current game mode.</param>
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            if ((mode & GameMode.GameOrEditor) != GameMode.None)
            {
                foreach (KeyValuePair<ProxyAction, Action> entry in _actions)
                {
                    entry.Key.shouldBeEnabled = true;
                }
            }
            else
            {
                foreach (KeyValuePair<ProxyAction, Action> entry in _actions)
                {
                    entry.Key.shouldBeEnabled = false;
                }
            }
        }

        /// <summary>
        /// Called every update.
        /// </summary>
        protected override void OnUpdate()
        {
            ModSettings activeSettings = Mod.Instance.ActiveSettings;

            foreach (KeyValuePair<ProxyAction, Action> entry in _actions)
            {
                if (entry.Key.WasPerformedThisFrame())
                {
                    _log.Info($"Performing action {entry.Key.name}");
                    entry.Value();
                }
            }
        }

        /// <summary>
        /// Called when the system is destroyed.
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();

            DestroyObjects();
        }

        /// <summary>
        /// Toggles the overlay (called by hotkey action).
        /// </summary>
        private void ToggleOverlay()
        {
            _log.Info("Toggling overlay");

            // Hide overlay if it's currently visible.
            if (_isVisible)
            {
                _isVisible = false;
                if (_overlayObject)
                {
                    _overlayObject.SetActive(false);
                }

                return;
            }

            // Showing overlay - create overlay if it's not already there, or if the file we used has been deleted.
            if (!_overlayObject || !_overlayMaterial || !_overlayTexture)
            {
                CreateOverlay();
            }

            // Show overlay if one was successfully loaded.
            if (_overlayObject)
            {
                _overlayObject.SetActive(true);
                _isVisible = true;
            }
            else
            {
                _log.Info("Overlay object wasn't created");
            }

            // Ensure initial shader initialization if needed.
            if (!_shaderInitialized)
            {
                ShowThroughTerrain(Mod.Instance.ActiveSettings.ShowThroughTerrain);
            }
        }

        /// <summary>
        /// Updates the overlay texture.
        /// </summary>
        private void UpdateOverlayTexture()
        {
            // Ensure file exists.
            string selectedOverlay = Mod.Instance.ActiveSettings.SelectedOverlay;
            if (string.IsNullOrEmpty(selectedOverlay))
            {
                _log.Info($"no overlay file set");
                return;
            }

            if (!File.Exists(selectedOverlay))
            {
                _log.Info($"invalid overlay file {selectedOverlay}");
                return;
            }

            _log.Info($"loading image file {selectedOverlay}");

            // Ensure texture instance.
            _overlayTexture ??= new Texture2D(1, 1, TextureFormat.ARGB32, false);

            // Load and apply texture.
            _overlayTexture.LoadImage(File.ReadAllBytes(selectedOverlay));
            _overlayTexture.Apply();

            // Create material.
            _overlayMaterial ??= new Material(_overlayShader)
            {
                mainTexture = _overlayTexture,
            };
        }

        /// <summary>
        /// Creates the overlay object.
        /// </summary>
        private void CreateOverlay()
        {
            // Dispose of any existing objects.
            DestroyObjects();

            // Load image texture.
            try
            {
                // Load texture.
                UpdateOverlayTexture();

                // Create basic plane.
                _overlayObject = GameObject.CreatePrimitive(PrimitiveType.Plane);

                // Apply scale.
                SetSize(Mod.Instance.ActiveSettings.OverlaySize);

                // Set overlay elevation.
                ResetElevation();
                TerrainHeightData terrainHeight = World.GetOrCreateSystemManaged<TerrainSystem>().GetHeightData();
                WaterSurfaceData<SurfaceWater> waterSurface = World.GetOrCreateSystemManaged<WaterSystem>().GetSurfaceData(out _);
                _log.Info($"terrain height is {WaterUtils.SampleHeight(ref waterSurface, ref terrainHeight, float3.zero)}");
                _overlayObject.transform.position = new Vector3(Mod.Instance.ActiveSettings.OverlayPosX, Mod.Instance.ActiveSettings.OverlayPosY, Mod.Instance.ActiveSettings.OverlayPosZ);

                // Apply rotation.
                UpdateRotation();

                // Attach material to GameObject.
                _overlayObject.GetComponent<Renderer>().material = _overlayMaterial;
                SetAlpha(Mod.Instance.ActiveSettings.Alpha);
            }
            catch (Exception e)
            {
                _log.Error(e, "exception loading image overlay file");
            }
        }

        /// <summary>
        /// Loads the custom shader from file.
        /// </summary>
        /// <returns><c>true</c> if the shader was successfully loaded, <c>false</c> otherwise.</returns>
        private bool LoadShader()
        {
            try
            {
                _log.Info("loading overlay shader");
                using StreamReader reader = new (Assembly.GetExecutingAssembly().GetManifestResourceStream("ImageOverlay.Shader.shaderbundle"));
                {
                    // Extract shader from file.
                    _overlayShader = AssetBundle.LoadFromStream(reader.BaseStream)?.LoadAsset<Shader>("Assets/UnlitTransparentAdditive.shader");
                    if (_overlayShader is not null)
                    {
                        // Shader loaded - all good!
                        return true;
                    }
                    else
                    {
                        _log.Critical("Image Overlay: unable to load overlay shader from asset bundle; aborting operation.");
                    }
                }
            }
            catch (Exception e)
            {
                _log.Critical(e, "Image Overlay: exception loading overlay shader; aborting operation.");
            }

            // If we got here, something went wrong.
            return false;
        }

        /// <summary>
        /// Destroys any existing texture and GameObject.
        /// </summary>
        private void DestroyObjects()
        {
            // Overlay texture.
            if (_overlayTexture)
            {
                _log.Info("destroying existing overlay texture");
                UnityEngine.Object.DestroyImmediate(_overlayTexture);
                _overlayTexture = null;
            }

            // Overlay material.
            if (_overlayMaterial)
            {
                _log.Info("destroying existing overlay material");
                UnityEngine.Object.DestroyImmediate(_overlayMaterial);
                _overlayMaterial = null;
            }

            // GameObject.
            if (_overlayObject)
            {
                _log.Info("destroying existing overlay object");
                UnityEngine.Object.DestroyImmediate(_overlayObject);
                _overlayObject = null;
            }
        }
    }
}