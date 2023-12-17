// <copyright file="ImageOverlaySystem.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace ImageOverlay
{
    using System;
    using System.IO;
    using Colossal.Logging;
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
            InputAction toggleKey = new ("ImageOverlayToggle");
            toggleKey.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/ctrl").With("Button", "<Keyboard>/o");
            toggleKey.performed += ToggleOverlay;
            toggleKey.Enable();

            InputAction upKey = new ("ImageOverlayUp");
            upKey.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/ctrl").With("Button", "<Keyboard>/pageup");
            upKey.performed += (c) => ChangeHeight(5f);
            upKey.Enable();

            InputAction downKey = new ("ImageOverlayDown");
            downKey.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/ctrl").With("Button", "<Keyboard>/pagedown");
            downKey.performed += (c) => ChangeHeight(-5f);
            downKey.Enable();

            InputAction northKey = new ("ImageOverlayNorth");
            northKey.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/ctrl").With("Button", "<Keyboard>/uparrow");
            northKey.performed += (c) => ChangePosition(0f, 1f);
            northKey.Enable();

            InputAction southKey = new ("ImageOverlaySouth");
            southKey.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/ctrl").With("Button", "<Keyboard>/downarrow");
            southKey.performed += (c) => ChangePosition(0f, -1f);
            southKey.Enable();

            InputAction eastKey = new ("ImageOverlayEast");
            eastKey.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/ctrl").With("Button", "<Keyboard>/rightarrow");
            eastKey.performed += (c) => ChangePosition(1f, 0f);
            eastKey.Enable();

            InputAction westKey = new ("ImageOverlayWest");
            westKey.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/ctrl").With("Button", "<Keyboard>/leftarrow");
            westKey.performed += (c) => ChangePosition(-1f, 0f);
            westKey.Enable();

            InputAction northKeyLarge = new ("ImageOverlayNorthLarge");
            northKeyLarge.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/shift").With("Button", "<Keyboard>/uparrow");
            northKeyLarge.performed += (c) => ChangePosition(0f, 10f);
            northKeyLarge.Enable();

            InputAction southKeyLarge = new ("ImageOverlaySouthLarge");
            southKeyLarge.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/shift").With("Button", "<Keyboard>/downarrow");
            southKeyLarge.performed += (c) => ChangePosition(0f, -10f);
            southKeyLarge.Enable();

            InputAction eastKeyLarge = new ("ImageOverlayEastLarge");
            eastKeyLarge.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/shift").With("Button", "<Keyboard>/rightarrow");
            eastKeyLarge.performed += (c) => ChangePosition(10f, 0f);
            eastKeyLarge.Enable();

            InputAction westKeyLarge = new ("ImageOverlayWestLarge");
            westKeyLarge.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/shift").With("Button", "<Keyboard>/leftarrow");
            westKeyLarge.performed += (c) => ChangePosition(-10f, 0f);
            westKeyLarge.Enable();

            InputAction rotateRightKey = new ("ImageOverlayRotateRight");
            rotateRightKey.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/ctrl").With("Button", "<Keyboard>/period");
            rotateRightKey.performed += (c) => Rotate(90f);
            rotateRightKey.Enable();

            InputAction rotateLeftKey = new ("ImageOverlayRotateLeft");
            rotateLeftKey.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/ctrl").With("Button", "<Keyboard>/comma");
            rotateLeftKey.performed += (c) => Rotate(-90f);
            rotateLeftKey.Enable();
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
        /// <param name="context">Callback context.</param>
        private void ToggleOverlay(InputAction.CallbackContext context)
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
            if (!File.Exists(Mod.Instance.ActiveSettings.SelectedOverlay))
            {
                _log.Info("invalid overlay file " + Mod.Instance.ActiveSettings.SelectedOverlay);
                return;
            }

            _log.Info("loading image file " + Mod.Instance.ActiveSettings.SelectedOverlay);

            // Ensure texture instance.
            _overlayTexture ??= new Texture2D(1, 1, TextureFormat.ARGB32, false);

            // Load and apply texture.
            _overlayTexture.LoadImage(File.ReadAllBytes(Mod.Instance.ActiveSettings.SelectedOverlay));
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

                // Plane primitive is 10x10 in size; scale up to cover entire map.
                _overlayObject.transform.localScale = new Vector3(1433.6f, 1f, 1433.6f);

                // Initial rotation to align to map.
                Rotate(180f);

                // Set overlay position to centre of map, 5m above surface level.
                TerrainHeightData terrainHeight = World.GetOrCreateSystemManaged<TerrainSystem>().GetHeightData();
                WaterSurfaceData waterSurface = World.GetOrCreateSystemManaged<WaterSystem>().GetSurfaceData(out _);
                _overlayObject.transform.position = new Vector3(0f, WaterUtils.SampleHeight(ref waterSurface, ref terrainHeight, float3.zero) + 5f, 0f);

                // Attach material to GameObject.
                _overlayObject.GetComponent<Renderer>().material = _overlayMaterial;
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

                // Check that asset bundle exists.
                string assetBundlePath = Path.Combine(Mod.Instance.AssemblyPath, "shaderbundle");
                if (!File.Exists(assetBundlePath))
                {
                    _log.Critical("Image Overlay: unable to find overlay shader asset bundle; aborting operation.");
                }

                // Extract shader from file.
                _overlayShader = AssetBundle.LoadFromFile(assetBundlePath)?.LoadAsset<Shader>("UnlitTransparentAdditive.shader");
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