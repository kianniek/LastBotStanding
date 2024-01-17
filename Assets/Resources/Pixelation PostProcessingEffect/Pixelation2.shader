Shader "Custom/PixelationShader"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_PixelSize("Pixel Size", Float) = 10.0
		_Intensity("Intensity", Float) = 0
		_Shift("Shift", Vector) = (0,0,0,0)
	}
		SubShader
		{
			// Render after all opaque geometry
			Tags { "RenderType" = "Opaque" }

			// Define the Stencil buffer operations
			Stencil
			{
				Ref 1 // The reference value used to compare against the stencil buffer
				Comp Equal // The comparison function which needs to pass for the pixel to be rendered
				Pass Keep // What to do with the stencil buffer if the stencil test passes
			}

			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				#include "UnityCG.cginc"

				struct appdata
				{
					float4 vertex : POSITION;
					float2 uv : TEXCOORD0;
				};

				struct v2f
				{
					float2 uv : TEXCOORD0;
					float4 vertex : SV_POSITION;
				};

				sampler2D _MainTex;
				float4 _MainTex_ST;
				float _PixelSize;
				float _Intensity;
				float4 _Shift;

				v2f vert(appdata v)
				{
					v2f o;
					o.vertex = UnityObjectToClipPos(v.vertex);
					o.uv = TRANSFORM_TEX(v.uv, _MainTex);
					return o;
				}

				fixed4 frag(v2f i) : SV_Target
				{
					_PixelSize = _PixelSize * 2;
					float2 centeredUv = i.uv;
					// Calculate the centered pixelated UV coordinates
					if (_PixelSize != 0)
					{
						float2 screenCenter = _PixelSize * 0.5 * _ScreenParams.xy;
						centeredUv = (i.uv * _ScreenParams.xy - screenCenter);
						_PixelSize *= _Intensity;
						centeredUv = floor(centeredUv / _PixelSize) * _PixelSize;
						centeredUv = (centeredUv + screenCenter) / _ScreenParams.xy;
					}

					// Sample the texture at the centered pixelated UV coords
					fixed4 col = tex2D(_MainTex, centeredUv);
					return col;
				}
				ENDCG
			}
		}
			FallBack "Diffuse"
}
