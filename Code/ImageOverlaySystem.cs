// <copyright file="ImageOverlaySystem.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
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
        private bool _isVisible = false;
        private Shader _overlayShader;

        /// <summary>
        /// Called when the system is created.
        /// </summary>
        protected override void OnCreate()
        {
            base.OnCreate();

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
                _overlayObject?.SetActive(false);
                return;
            }

            // Showing overlay - create overlay if it's not already there.
            if (!_overlayObject || !_overlayMaterial || !_overlayTexture)
            {
                CreateOverlay();
            }

            // Show overlay.
            _overlayObject.SetActive(true);
            _isVisible = true;
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
        /// Creates the overlay object.
        /// </summary>
        private void CreateOverlay()
        {
            _log.Info("creating overlay");

            // Dispose of any existing objects.
            DestroyObjects();

            // Create basic plane.
            _overlayObject = GameObject.CreatePrimitive(PrimitiveType.Plane);

            // Plane primitive is 10x10 in size; scale up to cover entire map.
            _overlayObject.transform.localScale = new Vector3(1433.6f, 1f, 1433.6f);

            // Set overlay position to centre of map, 5m above surface level.
            TerrainHeightData terrainHeight = World.GetOrCreateSystemManaged<TerrainSystem>().GetHeightData();
            WaterSurfaceData waterSurface = World.GetOrCreateSystemManaged<WaterSystem>().GetSurfaceData(out _);
            _overlayObject.transform.position = new Vector3(0f, WaterUtils.SampleHeight(ref waterSurface, ref terrainHeight, float3.zero) + 5f, 0f);

            // Load image texture.
            try
            {
                _log.Info("loading image");

                // Ensure file exists.
                string overlayPath = Path.Combine(Mod.Instance.AssemblyPath, "overlay.png");
                if (!File.Exists(overlayPath))
                {
                    return;
                }

                _log.Info("found image file");

                // Load and apply texture.
                _overlayTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                _overlayTexture.LoadImage(File.ReadAllBytes(overlayPath));
                _overlayTexture.Apply();

                // Attach texture to material.
                _overlayMaterial = new Material(_overlayShader)
                {
                    mainTexture = _overlayTexture,
                };
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
        /// <returns><c>true</c> if the shader was succesfully loaded, <c>false</c> otherwise.</returns>
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