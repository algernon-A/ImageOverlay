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
    using System.Threading.Tasks;
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
        // Map size.
        private const float MapSize = 14336f;

        // Input actions.
        private readonly List<KeyValuePair<ProxyAction, Action>> _actions = new ();

        // References.
        private ILog _log;

        // Mesh.
        private bool _terrainConforming = false;
        private Vector3[] _vertices;
        private int _meshResolution = 2048;

        // Texture.
        private GameObject _overlayObject;
        private Mesh _overlayMesh;
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
        /// Sets whether the overlay will be projected onto a terrain-conforming mesh or a flat plane.
        /// </summary>
        /// <param name="terrainConforming"><c>true</c> to use a terrain-conforming mesh, <c>false</c> to use a flat plane.</param>
        internal void SetTerrainConforming(bool terrainConforming)
        {
            // Don't do anything if no change.
            if (_terrainConforming != terrainConforming)
            {
                _terrainConforming = terrainConforming;
                if (_overlayMesh)
                {
                    GenerateMesh();
                    UpdateMeshHeights();

                    // Reset height to zero if we're using a terrain-conforming mesh.
                    if (_terrainConforming)
                    {
                        Vector3 overlayPosition = _overlayObject.transform.position;
                        overlayPosition.y = 0f;
                        _overlayObject.transform.position = overlayPosition;
                    }
                }
            }
        }

        /// <summary>
        /// Sets the mesh resolution for terrain-conforming meshes.
        /// </summary>
        /// <param name="resolution">The new mesh resolution.</param>
        internal void SetMeshResolution(int resolution)
        {
            // Don't do anything if no change.
            if (_meshResolution != resolution)
            {
                _meshResolution = resolution;

                // Don't regenerate mesh if we're not using a terrain-conforming mesh.
                if (_overlayMesh && _terrainConforming)
                {
                    // Regenerate mesh completely.
                    CreateOverlay();
                    GenerateMesh();
                    UpdateMeshHeights();
                }
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
                // Divide input size relative to mesh size.
                float scaledSize = size / MapSize;
                _overlayObject.transform.localScale = new Vector3(scaledSize, 1f, scaledSize);
                UpdateMeshHeights();
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
                UpdateMeshHeights();
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
                UpdateMeshHeights();
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
                UpdateMeshHeights();
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

            // Showing overlay - ensure terrain conforming setting is current.
            SetTerrainConforming(Mod.Instance.ActiveSettings.TerrainConforming);
            SetMeshResolution(Mod.Instance.ActiveSettings.MeshResolution);

            // Create overlay if it's not already there, or if the file we used has been deleted.
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

                // Create dynamic mesh object.
                _overlayMesh = new Mesh
                {
                    name = "ImageOverlayMesh",
                    indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                };
                _overlayMesh.MarkDynamic();

                // Create overlay game object.
                _overlayObject = new GameObject("ImageOverlay");
                _overlayObject.AddComponent<MeshRenderer>();

                // Generate initial overlay mesh.
                GenerateMesh();
                _overlayObject.AddComponent<MeshFilter>().mesh = _overlayMesh;

                // Apply scale.
                SetSize(Mod.Instance.ActiveSettings.OverlaySize);

                // Set overlay elevation.
                ResetElevation();
                TerrainHeightData terrainHeight = World.GetOrCreateSystemManaged<TerrainSystem>().GetHeightData();
                WaterSurfaceData<SurfaceWater> waterSurface = World.GetOrCreateSystemManaged<WaterSystem>().GetSurfaceData(out _);
                Vector3 overlayPosition = _overlayObject.transform.position;
                if (_terrainConforming)
                {
                    overlayPosition.y = 0f;
                }
                else
                {
                    overlayPosition.y = Mod.Instance.ActiveSettings.OverlayPosY;
                }

                _overlayObject.transform.position = overlayPosition;

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
        /// Regenerates the overlay mesh according to current settings.
        /// </summary>
        private void GenerateMesh()
        {
            if (!_overlayMesh)
            {
                _log.Info("GenerateMesh called with no overlay mesh");
                return;
            }

            // Generate mesh vertices and UVs.
            int meshSteps = _terrainConforming ? _meshResolution : 2;
            int maxMeshIndex = meshSteps - 1;
            int numVertices = meshSteps * meshSteps;
            _vertices = new Vector3[numVertices];
            Vector2[] uv = new Vector2[numVertices];
            const float startX = MapSize * -0.5f;
            const float startY = MapSize * -0.5f;
            float xStep = MapSize / (meshSteps - 1f);
            float zStep = MapSize / (meshSteps - 1f);
            for (int zIndex = 0; zIndex < meshSteps; ++zIndex)
            {
                // N is inverted compared to Z.
                float v = 1f - (zIndex / (float)maxMeshIndex);

                for (int xIndex = 0; xIndex < meshSteps; ++xIndex)
                {
                    int vertexIndex = (zIndex * meshSteps) + xIndex;
                    float x = startX + (xStep * xIndex);
                    float z = startY + (zStep * zIndex);
                    _vertices[vertexIndex] = new Vector3(x, 0f, z);

                    // U is inverted compared to X.
                    float u = 1f - (xIndex / (float)maxMeshIndex);
                    uv[vertexIndex] = new Vector2(u, v);
                }
            }

            // Generate mesh triangles.
            int[] tris = new int[maxMeshIndex * maxMeshIndex * 6];
            int triIndex = 0;
            for (int zIndex = 1; zIndex < meshSteps; ++zIndex)
            {
                for (int xIndex = 1; xIndex < meshSteps; ++xIndex)
                {
                    int topLeft = ((zIndex - 1) * meshSteps) + (xIndex - 1);
                    int topRight = topLeft + 1;
                    int bottomLeft = topLeft + meshSteps;
                    int bottomRight = bottomLeft + 1;

                    tris[triIndex++] = bottomLeft;
                    tris[triIndex++] = topLeft;
                    tris[triIndex++] = topRight;
                    tris[triIndex++] = topRight;
                    tris[triIndex++] = bottomRight;
                    tris[triIndex++] = bottomLeft;
                }
            }

            // Apply the new data.
            _overlayMesh.SetVertices(_vertices);
            _overlayMesh.SetUVs(0, uv);
            _overlayMesh.SetTriangles(tris, 0);
        }

        /// <summary>
        /// Updates the vertex elevations of the overlay mesh to conform to terrain and water surface heights.
        /// </summary>
        private void UpdateMeshHeights()
        {
            // Don't do anything if we're not using a terrain-conforming mesh.
            if (!_terrainConforming)
            {
                return;
            }

            TerrainHeightData terrainHeight = World.GetOrCreateSystemManaged<TerrainSystem>().GetHeightData();
            WaterSurfaceData<SurfaceWater> waterSurface = World.GetOrCreateSystemManaged<WaterSystem>().GetSurfaceData(out _);

            // Transform the vertex positions to world space. This is the only Unity transform work done outside the parallel worker.
            Vector3[] worldVertices = new Vector3[_vertices.Length];
            for (int i = 0; i < _vertices.Length; ++i)
            {
                worldVertices[i] = _overlayObject.transform.TransformPoint(_vertices[i]);
            }

            // Parallel height sampling worker, per-row (each row is independent).
            Parallel.For(0, _meshResolution, zIndex =>
            {
                for (int xIndex = 0; xIndex < _meshResolution; ++xIndex)
                {
                    int vertexIndex = (zIndex * _meshResolution) + xIndex;
                    float3 location = worldVertices[vertexIndex];

                    // Sample the terrain/water height for this vertex.
                    _vertices[vertexIndex].y = WaterUtils.SampleHeight(ref waterSurface, ref terrainHeight, location);
                }
            });

            // Apply the new data.
            _overlayMesh.SetVertices(_vertices);
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
                    if (_overlayShader)
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