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
    using Game.Simulation;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.InputSystem;

    /// <summary>
    /// The historical start mod system.
    /// </summary>
    internal sealed partial class ImageOverlaySystem : GameSystemBase
    {
        // References.
        private ILog _log;

        // Texture.
        private GameObject _overlayObject;
        private Material _overlayMaterial;
        private Texture2D _overlayTexture;
        private Shader _overlayShader;
        private bool _isVisible = false;

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

            // Set up hotkeys.
            InputBindingsManager.Ensure();
            List<string> shiftKey = new () { "<Keyboard>/shift" };
            List<string> controlKey = new () { "<Keyboard>/ctrl" };

            InputBindingsManager.Instance.AddAction("ImageOverlayToggle", "<Keyboard>/o", controlKey, ToggleOverlay);
            InputBindingsManager.Instance.AddAction("ImageOverlayUp", "<Keyboard>/pageup", controlKey, () => ChangeHeight(5f));
            InputBindingsManager.Instance.AddAction("ImageOverlayDown", "<Keyboard>/pagedown", controlKey, () => ChangeHeight(-5f));

            InputBindingsManager.Instance.AddAction("ImageOverlayNorth", "<Keyboard>/uparrow", controlKey, () => ChangePosition(0f, 1f));
            InputBindingsManager.Instance.AddAction("ImageOverlaySouth", "<Keyboard>/downarrow", controlKey, () => ChangePosition(0f, -1f));
            InputBindingsManager.Instance.AddAction("ImageOverlayEast", "<Keyboard>/rightarrow", controlKey, () => ChangePosition(1f, 0f));
            InputBindingsManager.Instance.AddAction("ImageOverlayWest", "<Keyboard>/leftarrow", controlKey, () => ChangePosition(-1f, 0f));

            InputBindingsManager.Instance.AddAction("ImageOverlayNorthLarge", "<Keyboard>/uparrow", shiftKey, () => ChangePosition(0f, 10f));
            InputBindingsManager.Instance.AddAction("ImageOverlaySouthLarge", "<Keyboard>/downarrow", shiftKey, () => ChangePosition(0f, -10f));
            InputBindingsManager.Instance.AddAction("ImageOverlayEastLarge", "<Keyboard>/rightarrow", shiftKey, () => ChangePosition(10f, 0f));
            InputBindingsManager.Instance.AddAction("ImageOverlayWestLarge", "<Keyboard>/leftarrow", shiftKey, () => ChangePosition(-10f, 0f));

            InputBindingsManager.Instance.AddAction("ImageOverlayRotateRight", "<Keyboard>/period", controlKey, () => Rotate(90f));
            InputBindingsManager.Instance.AddAction("ImageOverlayRotateLeft", "<Keyboard>/comma", controlKey, () => Rotate(-90f));

            InputBindingsManager.Instance.AddAction("ImageOverlaySizeUp", "<Keyboard>/equals", controlKey, () => Mod.Instance.ActiveSettings.OverlaySize += 10f);
            InputBindingsManager.Instance.AddAction("ImageOverlaySizeDown", "<Keyboard>/minus", controlKey, () => Mod.Instance.ActiveSettings.OverlaySize -= 10f);
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
                InputBindingsManager.Instance.EnableActions();
            }
            else
            {
                InputBindingsManager.Instance.DisableActions();
            }
        }

        /// <summary>
        /// Called every update.
        /// </summary>
        protected override void OnUpdate()
        {
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
        }

        /// <summary>
        /// Changes the overlay height by the given adjustment.
        /// </summary>
        /// <param name="adjustment">Height adjustment.</param>
        private void ChangeHeight(float adjustment)
        {
            // Null check.
            if (_overlayObject)
            {
                _overlayObject.transform.position += new Vector3(0f, adjustment, 0f);
            }
        }

        /// <summary>
        /// Changes the overlay position by the given adjustment.
        /// </summary>
        /// <param name="xAdjustment">X-position adjustment.</param>
        /// <param name="zAdjustment">Z-position adjustment.</param>
        private void ChangePosition(float xAdjustment, float zAdjustment)
        {
            // Null check.
            if (_overlayObject)
            {
                _overlayObject.transform.position += new Vector3(xAdjustment, 0f, zAdjustment);
            }
        }

        /// <summary>
        /// Rotates the overlay around the centre (y-axis) by the given amount in degrees.
        /// </summary>
        /// <param name="rotation">Rotation in degrees.</param>
        private void Rotate(float rotation)
        {
            // Null check.
            if (_overlayObject)
            {
                _overlayObject.transform.Rotate(new Vector3(0f, rotation, 0f), Space.Self);
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
            _log.Info("creating overlay");

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

                // Initial rotation to align to map.
                Rotate(180f);

                // Set overlay position to centre of map, 5m above surface level.
                TerrainHeightData terrainHeight = World.GetOrCreateSystemManaged<TerrainSystem>().GetHeightData();
                WaterSurfaceData waterSurface = World.GetOrCreateSystemManaged<WaterSystem>().GetSurfaceData(out _);
                _overlayObject.transform.position = new Vector3(0f, WaterUtils.SampleHeight(ref waterSurface, ref terrainHeight, float3.zero) + 5f, 0f);

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
                using StreamReader reader = new (Assembly.GetExecutingAssembly().GetManifestResourceStream("ImageOverlayLite.Shader.shaderbundle"));
                {
                    // Extract shader from file.
                    _overlayShader = AssetBundle.LoadFromStream(reader.BaseStream)?.LoadAsset<Shader>("UnlitTransparentAdditive.shader");
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