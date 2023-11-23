Shader "Instanced/InstancedIndirectColor" {
    Properties{
        _Color("Color (RGBA)", Color) = (1.0, 1.0, 1.0, 1.0)
    }
    SubShader {
        Tags { "RenderType" = "Opaque" }

        Pass {
            CGPROGRAM
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex   : POSITION;
            };

            struct v2f {
                float4 vertex   : SV_POSITION;
            };

            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                StructuredBuffer<float4x4> matrixBuffer;
            #endif

            void setup() { }

            v2f vert(appdata_t i, uint instanceID: SV_InstanceID) {
                v2f o;
                
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    // float3 objectOrigin = float3(unity_ObjectToWorld[0][3], unity_ObjectToWorld[1][3], unity_ObjectToWorld[2][3]);
                    // float4x4 TRS = matrixBuffer[instanceID];
                    // TRS[0][3] -= objectOrigin.x;
                    // TRS[1][3] -= objectOrigin.y;
                    // TRS[2][3] -= objectOrigin.z;
                    
                    float3 worldOrigin = unity_ObjectToWorld._14_24_34;
                    float4x4 TRS = matrixBuffer[instanceID];
                    TRS._14_24_34 -= worldOrigin;

                    unity_ObjectToWorld = mul(unity_ObjectToWorld, TRS);
                #endif
                
                o.vertex = UnityObjectToClipPos(i.vertex);

                return o;
            }

            fixed4 _Color;

            fixed4 frag(v2f i) : SV_Target {
                return _Color;
            }

            ENDCG
        }
    }
}