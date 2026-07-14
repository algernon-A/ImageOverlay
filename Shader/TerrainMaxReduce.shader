// Halves a terrain height map, keeping the highest of each 2x2 block of source texels.
// Applied repeatedly, this produces a map whose texels each hold the highest terrain across themselves, which the
// overlay's vertex shader uses to drape itself over the terrain without the terrain poking through.


Shader "Hidden/Terrain Max Reduce"
{
	Properties
	{
		_MainTex ("Source", 2D) = "black" {}
	}

	SubShader
	{
		Pass
		{
			ZTest Always
			Cull Off
			ZWrite Off
			Fog { Mode Off }

			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_TexelSize;

			float frag (v2f_img IN) : SV_Target
			{
				// The destination is exactly half the size of the source, so offsetting by half a source texel from
				// each destination texel centre lands exactly on the four source texel centres it covers.
				float2 offset = _MainTex_TexelSize.xy * 0.5;

				float height0 = tex2D(_MainTex, IN.uv + float2(-offset.x, -offset.y)).r;
				float height1 = tex2D(_MainTex, IN.uv + float2(offset.x, -offset.y)).r;
				float height2 = tex2D(_MainTex, IN.uv + float2(-offset.x, offset.y)).r;
				float height3 = tex2D(_MainTex, IN.uv + float2(offset.x, offset.y)).r;

				return max(max(height0, height1), max(height2, height3));
			}
			ENDCG
		}
	}
}
