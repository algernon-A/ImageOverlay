// Cribbed from various places and guides around the internet.
// Therefore, the good bits are somebody else's, and the bad bits are mine.


Shader "Unlit/Transparent Colored Additive"
{
	Properties
	{
		_MainTex ("Base (RGB), Alpha (A)", 2D) = "black" {}
        _Color ("Color (RGBA)", Color) = (1, 1, 1, 1)

		// ZTest property; 4 is 'LEqual' (default), 8 sets to 'Always' (enum is Unity CompareFunction).
		[Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4.0

		// Terrain conforming.  When _Conform is set, vertices are displaced onto the terrain surface using
		// _HeightMap, which holds the highest terrain within each of its texels.
		_HeightMap ("Terrain max height map", 2D) = "black" {}
		_MapOffsetScale ("Map offset (XY) and scale (ZW)", Vector) = (0, 0, 1, 1)
		_HeightScaleOffset ("Terrain height scale (X) and offset (Y)", Vector) = (1, 0, 0, 0)
		_Bias ("Height bias above terrain", Float) = 0.5
		_Conform ("Conform to terrain", Float) = 0.0

		// Backdrop terrain, used where the overlay reaches beyond the playable area that _HeightMap covers.
		_BackdropMap ("Backdrop max height map", 2D) = "black" {}
		_BackdropOffsetScale ("Backdrop offset (XY) and scale (ZW)", Vector) = (0, 0, 1, 1)

		// Vertex density.  _WarpFraction of the mesh, per side, is spread across _WarpExtent of it, concentrating
		// detail on the playable area when the overlay covers more than that.
		_WarpFraction ("Warp vertex fraction", Float) = 1.0
		_WarpExtent ("Warp local extent", Float) = 5.0
	}

	SubShader
	{
		LOD 200

		Tags
		{
			"Queue" = "Transparent"
			"RenderType" = "Transparent"
		}

		Pass
		{
			Cull Off
			Lighting Off
			ZTest [_ZTest]
			ZWrite Off
			Fog { Mode Off }
			Offset -1, -1
			Blend SrcAlpha One

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			// Sampling a texture in the vertex shader requires shader model 3.0.
			#pragma target 3.0
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_ST;
            float4 _Color;

			sampler2D _HeightMap;
			float4 _HeightMap_TexelSize;
			float4 _MapOffsetScale;
			float4 _HeightScaleOffset;
			float _Bias;
			float _Conform;

			sampler2D _BackdropMap;
			float4 _BackdropMap_TexelSize;
			float4 _BackdropOffsetScale;

			float _WarpFraction;
			float _WarpExtent;

			struct appdata_t
			{
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
				fixed4 color : COLOR;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				half2 texcoord : TEXCOORD0;
				fixed4 color : COLOR;
			};

			// Returns the highest terrain around the given map position.
			// Each height map texel already holds the maximum terrain height across itself, and taking the maximum of
			// the four texels nearest the position extends that to at least half a texel in every direction; the
			// height map is reduced far enough that this covers a whole grid cell, so terrain cannot poke through.
			float SampleMaxHeight(sampler2D heightMap, float4 mapTexelSize, float2 mapPosition)
			{
				float2 texelPosition = (mapPosition * mapTexelSize.zw) - 0.5;
				float2 firstTexel = (floor(texelPosition) + 0.5) * mapTexelSize.xy;
				float2 texelSize = mapTexelSize.xy;

				float height0 = tex2Dlod(heightMap, float4(firstTexel, 0, 0)).r;
				float height1 = tex2Dlod(heightMap, float4(firstTexel + float2(texelSize.x, 0), 0, 0)).r;
				float height2 = tex2Dlod(heightMap, float4(firstTexel + float2(0, texelSize.y), 0, 0)).r;
				float height3 = tex2Dlod(heightMap, float4(firstTexel + texelSize, 0, 0)).r;

				return max(max(height0, height1), max(height2, height3));
			}

			// Spreads _WarpFraction of the mesh, per side, across _WarpExtent of it, and the remainder across what's
			// left, concentrating vertices on the playable area. Both are 1 and 5 respectively when the overlay is
			// within the playable area, which leaves the mesh evenly spread.
			float Warp(float coordinate)
			{
				float inner = _WarpFraction * 5.0;
				float distance = abs(coordinate);

				if (distance <= inner)
				{
					return coordinate * (_WarpExtent / inner);
				}

				float outer = lerp(_WarpExtent, 5.0, (distance - inner) / (5.0 - inner));
				return sign(coordinate) * outer;
			}

			v2f vert (appdata_t v)
			{
				v2f output;

				// The mesh is evenly spread; warping it here rather than baking it in keeps it usable at any size.
				float2 local = float2(Warp(v.vertex.x), Warp(v.vertex.z));
				float3 worldPos = mul(unity_ObjectToWorld, float4(local.x, v.vertex.y, local.y, 1.0)).xyz;

				if (_Conform > 0.5)
				{
					// Height maps cover fixed areas, so the overlay's own position doesn't apply here.
					float2 mapPosition = (worldPos.xz + _MapOffsetScale.xy) * _MapOffsetScale.zw;
					float height;

					if (all(mapPosition >= 0.0) && all(mapPosition <= 1.0))
					{
						height = SampleMaxHeight(_HeightMap, _HeightMap_TexelSize, mapPosition);
					}
					else
					{
						// Beyond the playable area, only the coarser backdrop terrain is available.
						float2 backdropPosition = (worldPos.xz + _BackdropOffsetScale.xy) * _BackdropOffsetScale.zw;
						height = SampleMaxHeight(_BackdropMap, _BackdropMap_TexelSize, backdropPosition);
					}

					worldPos.y = (height * _HeightScaleOffset.x) + _HeightScaleOffset.y + _Bias;
				}

				output.vertex = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));

				// Texture coordinates follow the warped position, so that the image stays undistorted.
				output.texcoord = 0.5 - (local / 10.0);
				output.color = v.color;
				return output;
			}

			fixed4 frag (v2f IN) : COLOR
			{
				return tex2D(_MainTex, IN.texcoord) * IN.color * _Color;
			}
			ENDCG
		}
	}
}
