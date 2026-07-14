// <copyright file="OverlayMeshBuilder.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace ImageOverlay
{
    using Unity.Collections;
    using UnityEngine;
    using UnityEngine.Rendering;

    /// <summary>
    /// Generates the overlay's mesh, either as a flat quad or as a grid for the shader to drape over the terrain.
    /// </summary>
    internal static class OverlayMeshBuilder
    {
        /// <summary>
        /// The local size, per side, of all generated meshes, in units.
        /// This matches the Unity <see cref="PrimitiveType.Plane"/> primitive that these meshes replace, so that
        /// the existing scale (size divided by ten) and rotation handling continue to apply unchanged.
        /// </summary>
        internal const float MeshSize = 10f;

        /// <summary>
        /// The number of vertices per side of the mesh used when draping the overlay over the terrain.
        /// The mesh is built once and displaced by the shader, so this costs vertex shading and memory rather than
        /// rebuild time.
        /// </summary>
        internal const int ConformedGridSize = 1536;

        /// <summary>
        /// The number of vertices per side that the shader concentrates on the playable area when the overlay covers
        /// more than that. At the vanilla 14,336m playable area this gives around 14m of vertex spacing whatever the
        /// overlay's size, against a terrain heightmap of around 3.5m per sample; the remaining vertices are left to
        /// cover the backdrop terrain, whose heights are only stored at around 56m anyway.
        /// </summary>
        internal const int PlayableGridSize = 1024;

        // Vertices per side of the flat mesh; a single quad is all that a flat overlay needs.
        private const int FlatGridSize = 2;

        /// <summary>
        /// Builds a flat overlay mesh.
        /// </summary>
        /// <param name="mesh">Mesh to write to.</param>
        internal static void BuildFlat(Mesh mesh)
        {
            Build(mesh, FlatGridSize);
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// Builds the grid mesh that the shader drapes over the terrain.
        /// </summary>
        /// <param name="mesh">Mesh to write to.</param>
        internal static void BuildConformedGrid(Mesh mesh)
        {
            Build(mesh, ConformedGridSize);

            // The shader moves these vertices onto the terrain, which culling knows nothing about; without bounds
            // deep enough to cover any terrain the overlay could be draped over, it would be culled from view while
            // still on screen.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(MeshSize, 100000f, MeshSize));
        }

        /// <summary>
        /// Builds a flat, origin-centred grid lying in the XZ plane.
        /// </summary>
        /// <param name="mesh">Mesh to write to.</param>
        /// <param name="gridSize">Number of vertices per side.</param>
        private static void Build(Mesh mesh, int gridSize)
        {
            int vertexCount = gridSize * gridSize;
            int quadsPerSide = gridSize - 1;
            float step = MeshSize / quadsPerSide;
            float origin = -MeshSize / 2f;

            NativeArray<Vector3> vertices = new (vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeArray<Vector2> uvs = new (vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeArray<int> indices = new (quadsPerSide * quadsPerSide * 6, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            // Texture coordinates run opposite to the vertex axes, matching the layout of the Unity Plane primitive
            // that this mesh replaces (whose uv (0,0) sits at local position (5, 0, 5)).
            for (int j = 0; j < gridSize; ++j)
            {
                for (int i = 0; i < gridSize; ++i)
                {
                    int vertex = (j * gridSize) + i;
                    vertices[vertex] = new Vector3(origin + (i * step), 0f, origin + (j * step));
                    uvs[vertex] = new Vector2(1f - (i / (float)quadsPerSide), 1f - (j / (float)quadsPerSide));
                }
            }

            int index = 0;
            for (int j = 0; j < quadsPerSide; ++j)
            {
                for (int i = 0; i < quadsPerSide; ++i)
                {
                    int a = (j * gridSize) + i;
                    int b = a + 1;
                    int c = a + gridSize;
                    int d = c + 1;

                    indices[index++] = a;
                    indices[index++] = c;
                    indices[index++] = b;
                    indices[index++] = b;
                    indices[index++] = c;
                    indices[index++] = d;
                }
            }

            mesh.Clear();

            // The conformed grid comfortably exceeds the 65,535-vertex limit of the default 16-bit index format.
            mesh.indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);

            vertices.Dispose();
            uvs.Dispose();
            indices.Dispose();
        }
    }
}
