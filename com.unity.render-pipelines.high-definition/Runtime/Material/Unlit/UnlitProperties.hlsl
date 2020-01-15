TEXTURE2D(_DistortionVectorMap);
SAMPLER(sampler_DistortionVectorMap);

TEXTURE2D(_UnlitColorMap);
SAMPLER(sampler_UnlitColorMap);

TEXTURE2D(_EmissiveColorMap);
SAMPLER(sampler_EmissiveColorMap);

CBUFFER_START(UnityPerMaterial)

float4  _UnlitColor;
float4 _UnlitColorMap_ST;
float4 _UnlitColorMap_TexelSize;
float4 _UnlitColorMap_MipInfo;

float3 _EmissiveColor;
float4 _EmissiveColorMap_ST;
float _EmissiveExposureWeight;

float _AlphaCutoff;
float _DistortionScale;
float _DistortionVectorScale;
float _DistortionVectorBias;
float _DistortionBlurScale;
float _DistortionBlurRemapMin;
float _DistortionBlurRemapMax;

// Caution: C# code in BaseLitUI.cs call LightmapEmissionFlagsProperty() which assume that there is an existing "_EmissionColor"
// value that exist to identify if the GI emission need to be enabled.
// In our case we don't use such a mechanism but need to keep the code quiet. We declare the value and always enable it.
// TODO: Fix the code in legacy unity so we can customize the behavior for GI
float3 _EmissionColor;

// For raytracing indirect illumination effects, we need to be able to define if the emissive part of the material should contribute or not (mainly for area light sources in order to avoid double contribution)
// By default, the emissive is contributing
float _IncludeIndirectLighting;

// Following two variables are feeded by the C++ Editor for Scene selection
int _ObjectId;
int _PassValue;

#if defined(WRITE_DECAL_BUFFER)
int _DecalLayerMask;
#endif

CBUFFER_END
