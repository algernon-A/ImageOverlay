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
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.Experimental.Rendering;
    using static ActionNames;

    /// <summary>
    /// The historical start mod system.
    /// </summary>
    internal sealed partial class ImageOverlaySystem : GameSystemBase
    {
        // Default elevation bias above the surface when the overlay is draped over the terrain, in metres.
        private const float DefaultConformBias = 0.5f;

        // Maximum number of times the terrain heightmap is halved when building the overlay's max height map.
        private const int MaxHeightMapReductions = 6;

        // Share of the overlay mesh, per side, that the shader concentrates on the playable area.
        private const float PlayableVertexFraction = (float)OverlayMeshBuilder.PlayableGridSize / OverlayMeshBuilder.ConformedGridSize;

        // Input actions.
        private readonly List<KeyValuePair<ProxyAction, Action>> _actions = new ();

        // Terrain max height maps; one covering the playable area, one the backdrop terrain around it.
        private readonly RenderTexture[] _reductionTargets = new RenderTexture[MaxHeightMapReductions];
        private readonly RenderTexture[] _backdropReductionTargets = new RenderTexture[MaxHeightMapReductions];

        // References.
        private ILog _log;

        // Texture.
        private GameObject _overlayObject;
        private Mesh _overlayMesh;
        private Material _overlayMaterial;
        private Texture2D _overlayTexture;
        private Shader _overlayShader;
        private bool _isVisible = false;

        // Terrain max height map reduction.
        private Shader _maxReduceShader;
        private Material _maxReduceMaterial;

        // Status flags.
        private bool _shaderInitialized = false;
        private bool _meshDirty = false;

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
        /// Sets whether the overlay is draped over the terrain surface or drawn as a flat plane.
        /// </summary>
        /// <param name="conformToTerrain"><c>true</c> to drape the overlay over the terrain, <c>false</c> to draw it flat.</param>
        internal void SetConformToTerrain(bool conformToTerrain)
        {
            _log.Info($"setting terrain conforming to {conformToTerrain}");

            // Switching modes changes the mesh in either direction, so always rebuild.
            _meshDirty = true;

            // Elevation is a height above the terrain when draped over it, and a height above the map base when flat.
            // The two aren't interchangeable, so carrying one over as the other would leave the overlay buried or
            // floating; re-derive it for the mode being switched to instead.
            ResetElevation();
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
                // Overlay mesh is 10m wide, so divide input size accordingly.
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
        /// When the overlay is draped over the terrain this is a bias above the surface, not an absolute height.
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
        /// Fits the overlay to the maximum playable area, or, if it's already fitted to that, to the map extents.
        /// </summary>
        internal void ToggleExtents()
        {
            ModSettings activeSettings = Mod.Instance.ActiveSettings;
            TerrainSystem terrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();

            // The maximum playable area; the map itself, which map tiles are laid out across.
            float2 playableCentre = terrainSystem.playableOffset + (terrainSystem.playableArea * 0.5f);
            float playableSize = math.cmax(terrainSystem.playableArea);

            // The map extents; the playable area plus the backdrop terrain surrounding it.
            float2 mapCentre = terrainSystem.worldOffset + (terrainSystem.worldSize * 0.5f);
            float mapSize = math.cmax(terrainSystem.worldSize);

            // Maps without backdrop terrain, as in the editor, have nothing beyond their playable area to fit to.
            if (mapSize <= playableSize)
            {
                _log.Info($"map has no backdrop; fitting overlay to the whole map ({playableSize}m)");
                FitOverlay(playableCentre, playableSize);
                return;
            }

            // Switch to whichever of the two the overlay isn't already fitted to.
            bool fittedToPlayableArea = math.abs(activeSettings.OverlaySize - playableSize) < math.abs(activeSettings.OverlaySize - mapSize);
            if (fittedToPlayableArea)
            {
                _log.Info($"fitting overlay to map extents ({mapSize}m)");
                FitOverlay(mapCentre, mapSize);
            }
            else
            {
                _log.Info($"fitting overlay to maximum playable area ({playableSize}m)");
                FitOverlay(playableCentre, playableSize);
            }
        }

        /// <summary>
        /// Resets the overlay elevation to 5m above the surface level at the exact centre of the map, or, if the
        /// overlay is draped over the terrain, to the default bias above the surface.
        /// </summary>
        internal void ResetElevation()
        {
            ModSettings activeSettings = Mod.Instance.ActiveSettings;

            // When draped over the terrain, elevation is a bias above the surface rather than an absolute height.
            if (activeSettings.ConformToTerrain)
            {
                activeSettings.OverlayPosY = DefaultConformBias;
                return;
            }

            if (!TryGetSurfaceData(out TerrainHeightData terrainHeight, out WaterSurfaceData<SurfaceWater> waterSurface))
            {
                _log.Info("surface data unavailable; unable to reset elevation");
                return;
            }

            float surfaceHeight = WaterUtils.SampleHeight(ref waterSurface, ref terrainHeight, float3.zero);
            _log.Info($"terrain height is {surfaceHeight}");
            activeSettings.OverlayPosY = surfaceHeight + 5f;
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
            _actions.Add(new (activeSettings.GetAction(ToggleExtentsAction), ToggleExtents));

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

            // Rebuild the mesh if the overlay has been switched between flat and draped.
            // A hidden overlay is rebuilt when it's next shown instead.
            if (_meshDirty && _isVisible && _overlayObject)
            {
                BuildMesh();
            }

            // The shader reads the terrain heightmap directly, so terraforming, moving, resizing, and rotating the
            // overlay all follow along on their own; only the values the shader reads need keeping current.
            if (_isVisible && _overlayObject)
            {
                UpdateConformProperties();
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
                // Pick up any changes made while the overlay was hidden.
                if (_meshDirty)
                {
                    BuildMesh();
                }

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

                // Create the overlay object with a generated mesh, so that it can be draped over the terrain.
                _overlayObject = new GameObject("ImageOverlay");
                _overlayMesh = new Mesh
                {
                    name = "ImageOverlayMesh",
                };

                // The conformed mesh is regenerated whenever the overlay is moved, sized, or rotated.
                _overlayMesh.MarkDynamic();
                _overlayObject.AddComponent<MeshFilter>().sharedMesh = _overlayMesh;
                _overlayObject.AddComponent<MeshRenderer>();

                // Apply scale.
                SetSize(Mod.Instance.ActiveSettings.OverlaySize);

                // Set overlay elevation.
                ResetElevation();
                _overlayObject.transform.position = new Vector3(Mod.Instance.ActiveSettings.OverlayPosX, Mod.Instance.ActiveSettings.OverlayPosY, Mod.Instance.ActiveSettings.OverlayPosZ);

                // Apply rotation.
                UpdateRotation();

                // Attach material to GameObject.
                _overlayObject.GetComponent<Renderer>().material = _overlayMaterial;
                SetAlpha(Mod.Instance.ActiveSettings.Alpha);

                // Build the mesh last, once the object's transform is final; a conformed mesh samples through it.
                BuildMesh();
            }
            catch (Exception e)
            {
                _log.Error(e, "exception loading image overlay file");
            }
        }

        /// <summary>
        /// Centres the overlay on the given position and sets it to the given size.
        /// </summary>
        /// <param name="centre">Position to centre the overlay on.</param>
        /// <param name="size">Size to set, per side, in metres.</param>
        private void FitOverlay(float2 centre, float size)
        {
            ModSettings activeSettings = Mod.Instance.ActiveSettings;
            activeSettings.OverlaySize = size;
            activeSettings.OverlayPosX = centre.x;
            activeSettings.OverlayPosZ = centre.y;
        }

        /// <summary>
        /// Rebuilds the overlay mesh to match current settings.
        /// </summary>
        private void BuildMesh()
        {
            if (!_overlayMesh || !_overlayObject)
            {
                return;
            }

            _meshDirty = false;

            if (Mod.Instance.ActiveSettings.ConformToTerrain)
            {
                OverlayMeshBuilder.BuildConformedGrid(_overlayMesh);
            }
            else
            {
                OverlayMeshBuilder.BuildFlat(_overlayMesh);
            }
        }

        /// <summary>
        /// Updates the values that the overlay shader uses to drape itself over the terrain.
        /// </summary>
        private void UpdateConformProperties()
        {
            if (_overlayObject.GetComponent<Renderer>()?.material is not Material overlayMaterial)
            {
                return;
            }

            // The shader keeps displacing until told otherwise, so this has to be cleared, not just left unset.
            if (!Mod.Instance.ActiveSettings.ConformToTerrain)
            {
                overlayMaterial.SetFloat("_Conform", 0f);
                return;
            }

            TerrainSystem terrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            if (terrainSystem.heightmap is not Texture heightmap)
            {
                // No terrain to conform to (yet); stay flat rather than collapsing to zero height.
                overlayMaterial.SetFloat("_Conform", 0f);
                return;
            }

            ModSettings activeSettings = Mod.Instance.ActiveSettings;
            float playableSize = math.cmax(terrainSystem.playableArea);
            float overlaySize = activeSettings.OverlaySize;

            // The playable area keeps a fixed share of the mesh whenever the overlay covers more than it, so its
            // detail doesn't drop away as the overlay grows.
            bool concentrateOnPlayableArea = overlaySize > playableSize + 1f;
            float warpFraction = concentrateOnPlayableArea ? PlayableVertexFraction : 1f;
            float warpExtent = concentrateOnPlayableArea ? OverlayMeshBuilder.MeshSize * 0.5f * (playableSize / overlaySize) : OverlayMeshBuilder.MeshSize * 0.5f;

            // Cell sizes either side of that split, which set how far each height map has to be reduced.
            float playableCellSize = (concentrateOnPlayableArea ? playableSize : overlaySize) / (OverlayMeshBuilder.PlayableGridSize - 1);
            float backdropCellSize = concentrateOnPlayableArea
                ? (overlaySize - playableSize) / (OverlayMeshBuilder.ConformedGridSize - OverlayMeshBuilder.PlayableGridSize)
                : playableCellSize;

            if (BuildMaxHeightMap(heightmap, playableSize, playableCellSize, _reductionTargets) is not Texture maxHeightMap)
            {
                overlayMaterial.SetFloat("_Conform", 0f);
                return;
            }

            // Beyond the playable area only the coarser backdrop terrain is available. Without one, the overlay has
            // nothing out there to conform to, so the playable area's own height map stands in.
            Texture maxBackdropMap = maxHeightMap;
            Vector4 backdropOffsetScale = terrainSystem.mapOffsetScale;
            if (concentrateOnPlayableArea && terrainSystem.downscaledHeightmap is Texture backdrop)
            {
                float2 worldSize = terrainSystem.worldSize;
                if (BuildMaxHeightMap(backdrop, math.cmax(worldSize), backdropCellSize, _backdropReductionTargets) is Texture reducedBackdrop)
                {
                    maxBackdropMap = reducedBackdrop;
                    backdropOffsetScale = new Vector4(-terrainSystem.worldOffset.x, -terrainSystem.worldOffset.y, 1f / worldSize.x, 1f / worldSize.y);
                }
            }

            float2 heightScaleOffset = terrainSystem.heightScaleOffset;
            overlayMaterial.SetTexture("_HeightMap", maxHeightMap);
            overlayMaterial.SetVector("_MapOffsetScale", terrainSystem.mapOffsetScale);
            overlayMaterial.SetTexture("_BackdropMap", maxBackdropMap);
            overlayMaterial.SetVector("_BackdropOffsetScale", backdropOffsetScale);
            overlayMaterial.SetVector("_HeightScaleOffset", new Vector4(heightScaleOffset.x, heightScaleOffset.y, 0f, 0f));
            overlayMaterial.SetFloat("_WarpFraction", warpFraction);
            overlayMaterial.SetFloat("_WarpExtent", warpExtent);
            overlayMaterial.SetFloat("_Bias", activeSettings.OverlayPosY);
            overlayMaterial.SetFloat("_Conform", 1f);
        }

        /// <summary>
        /// Halves the terrain heightmap until each of its texels covers at least twice the overlay's grid spacing,
        /// keeping the highest terrain in each texel. Taking the maximum, rather than sampling the height directly
        /// below each vertex, is what stops terrain poking through a grid far coarser than the heightmap.
        /// </summary>
        /// <param name="heightmap">Terrain height map to reduce.</param>
        /// <param name="coveredSize">Size of the area covered by the height map, per side, in metres.</param>
        /// <param name="cellSize">Size of the grid cells that will sample the reduced map, in metres.</param>
        /// <param name="targets">Reduction targets to render through.</param>
        /// <returns>Reduced height map, the height map itself if no reduction is needed, or <c>null</c> on failure.</returns>
        private Texture BuildMaxHeightMap(Texture heightmap, float coveredSize, float cellSize, RenderTexture[] targets)
        {
            // Each grid cell must be covered by the four texels nearest to it, which reach half a texel out in every
            // direction; texels therefore need to be twice the cell size. The tolerance stops the usual case (a
            // map-sized overlay over a 4,096 sample heightmap) from tipping just over a halving and doubling how far
            // ridges are inflated in exchange for a fraction of a percent of coverage.
            float terrainTexelSize = coveredSize / heightmap.width;
            float requiredRatio = 2f * cellSize / terrainTexelSize * 0.99f;
            int reductions = Mathf.CeilToInt(Mathf.Log(math.max(requiredRatio, 1f), 2f));
            reductions = Mathf.Clamp(reductions, 0, MaxHeightMapReductions);

            if (reductions == 0)
            {
                // Heightmap texels already cover a whole cell, so it can be sampled as-is.
                return heightmap;
            }

            if (!_maxReduceMaterial)
            {
                if (!_maxReduceShader)
                {
                    return null;
                }

                _maxReduceMaterial = new Material(_maxReduceShader);
            }

            Texture source = heightmap;
            for (int i = 0; i < reductions; ++i)
            {
                int width = math.max(heightmap.width >> (i + 1), 1);
                int height = math.max(heightmap.height >> (i + 1), 1);
                RenderTexture target = targets[i];

                if (!target || target.width != width || target.height != height)
                {
                    ReleaseReductionTarget(targets, i);
                    target = new RenderTexture(width, height, 0, GraphicsFormat.R16_UNorm)
                    {
                        name = $"ImageOverlayMaxHeight{i}",

                        // Every sample lands exactly on a texel centre, so filtering would only cost time.
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp,
                    };

                    target.Create();
                    targets[i] = target;
                }

                Graphics.Blit(source, target, _maxReduceMaterial);
                source = target;
            }

            return source;
        }

        /// <summary>
        /// Releases the given height map reduction target, if it exists.
        /// </summary>
        /// <param name="targets">Reduction targets.</param>
        /// <param name="index">Reduction target index.</param>
        private void ReleaseReductionTarget(RenderTexture[] targets, int index)
        {
            if (targets?[index] is RenderTexture target)
            {
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                targets[index] = null;
            }
        }

        /// <summary>
        /// Gets the current terrain and water surface data, completing any pending writes to them.
        /// </summary>
        /// <param name="terrainData">Terrain height data.</param>
        /// <param name="waterData">Water surface data.</param>
        /// <returns><c>true</c> if both sets of data are valid to sample, <c>false</c> otherwise.</returns>
        private bool TryGetSurfaceData(out TerrainHeightData terrainData, out WaterSurfaceData<SurfaceWater> waterData)
        {
            TerrainSystem terrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            waterData = World.GetOrCreateSystemManaged<WaterSystem>().GetSurfaceData(out JobHandle deps);
            terrainSystem.AddCPUHeightReader(deps);
            terrainData = terrainSystem.GetHeightData();
            deps.Complete();

            // Height sampling indexes these arrays directly without any bounds checking of its own. Terrain height
            // data also reports a placeholder 2x2 resolution until the heightmap is ready, which would produce
            // nonsense heights.
            return terrainData.isCreated && waterData.isCreated && terrainData.resolution.x > 2;
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
                    // Extract shaders from file; both come from the one bundle, which can only be loaded once.
                    AssetBundle shaderBundle = AssetBundle.LoadFromStream(reader.BaseStream);
                    _overlayShader = shaderBundle?.LoadAsset<Shader>("Assets/UnlitTransparentAdditive.shader");
                    _maxReduceShader = shaderBundle?.LoadAsset<Shader>("Assets/TerrainMaxReduce.shader");

                    if (_overlayShader is not null)
                    {
                        // The overlay can still be drawn flat without this one, so it isn't fatal.
                        if (_maxReduceShader is null)
                        {
                            _log.Error("Image Overlay: unable to load terrain height reduction shader from asset bundle; terrain conforming will be unavailable.");
                        }

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

            // Height map reduction targets.
            for (int i = 0; i < MaxHeightMapReductions; ++i)
            {
                ReleaseReductionTarget(_reductionTargets, i);
                ReleaseReductionTarget(_backdropReductionTargets, i);
            }

            // Height map reduction material.
            if (_maxReduceMaterial)
            {
                UnityEngine.Object.DestroyImmediate(_maxReduceMaterial);
                _maxReduceMaterial = null;
            }

            // Overlay mesh.
            if (_overlayMesh)
            {
                _log.Info("destroying existing overlay mesh");
                UnityEngine.Object.DestroyImmediate(_overlayMesh);
                _overlayMesh = null;
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