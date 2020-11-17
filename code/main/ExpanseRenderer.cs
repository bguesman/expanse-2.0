using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Experimental.Rendering;
using ExpanseCommonNamespace;

class ExpanseRenderer : SkyRenderer
{

/******************************************************************************/
/**************************** SHADER PROPERTY ID'S ****************************/
/******************************************************************************/

public static readonly int _WorldSpaceCameraPos1ID = Shader.PropertyToID("_WorldSpaceCameraPos1");
public static readonly int _ViewMatrix1ID = Shader.PropertyToID("_ViewMatrix1");
public static readonly int _PixelCoordToViewDirWS = Shader.PropertyToID("_PixelCoordToViewDirWS");

/******************************************************************************/
/************************** END SHADER PROPERTY ID'S **************************/
/******************************************************************************/








/******************************************************************************/
/************************** SKY PRECOMPUTATION TABLES *************************/
/******************************************************************************/

/* LUTs. */
private RTHandle m_skyT;                              /* Transmittance. */
private Texture2D m_skyTCPU;
private bool m_skyTNeedsUpdate;
private RTHandle m_skyMS;                             /* Multiple Scattering. */

/* Textures. */
private RTHandle m_skySS;                             /* Single Scattering. */
private RTHandle m_skyMSAcc;                          /* Multiple Scattering. */
private RTHandle m_skyAP;                             /* Aerial Perspective. */

/* For checking if table reallocation is required. */
private ExpanseCommon.SkyTextureResolution m_skyTextureResolution;
private int m_numAtmosphereLayersEnabled = 0;
private int m_numBodiesEnabled = 0;
private Vector4[] m_bodyDirections = new Vector4[ExpanseCommon.kMaxCelestialBodies];

/* Allocates all sky precomputation tables for all atmosphere layers at a
 * specified quality level. */
void allocateSkyPrecomputationTables(Expanse sky) {

  /* Count how many layers are active. */
  int numEnabled = 0;
  for (int i = 0; i < ExpanseCommon.kMaxAtmosphereLayers; i++) {
    bool enabled = ((BoolParameter) sky.GetType().GetField("layerEnabled" + i).GetValue(sky)).value;
    if (enabled) {
      numEnabled++;
    }
  }

  /* Get sky texture quality. */
  ExpanseCommon.SkyTextureQuality quality = (ExpanseCommon.SkyTextureQuality) sky.skyTextureQuality.value;

  /* Reallocate tables if either of these things have changed. */
  if (numEnabled != m_numAtmosphereLayersEnabled
    || quality != m_skyTextureResolution.quality) {

    /* Release existing tables. */
    cleanupSkyTables();

    ExpanseCommon.SkyTextureResolution res =
      ExpanseCommon.skyQualityToSkyTextureResolution(quality);

    m_skyT = allocateSky2DTable(res.T, 0, "SkyT");
    m_skyMS = allocateSky2DTable(res.MS, 0, "SkyMS");

    m_skySS = allocateSky2DTable(res.SS, 0, "SkySS");
    m_skyMSAcc = allocateSky2DTable(res.MSAccumulation, 0, "SkyMSAcc");
    m_skyAP = allocateSky3DTable(res.AP, 0, "SkyAP");

    m_numAtmosphereLayersEnabled = numEnabled;
    m_skyTextureResolution = res;

    /* Resize CPU copy of transmittance table. */
    m_skyTCPU = new Texture2D((int) res.T.x, (int) res.T.y, TextureFormat.RGBAFloat, false);
  }
}

void cleanupSkyTables() {
  RTHandles.Release(m_skyT);
  m_skyT = null;
  RTHandles.Release(m_skyMS);
  m_skyMS = null;
  RTHandles.Release(m_skySS);
  m_skySS = null;
  RTHandles.Release(m_skyMSAcc);
  m_skyMSAcc = null;
  RTHandles.Release(m_skyAP);
  m_skyAP = null;
}

/* Allocates 1D sky precomputation table. */
RTHandle allocateSky1DTable(int resolution, int index, string name) {
  var table = RTHandles.Alloc((int) resolution,
                              1,
                              dimension: TextureDimension.Tex2D,
                              colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                              enableRandomWrite: true,
                              name: string.Format(name + "{0}", index));
  Debug.Assert(table != null);
  return table;
}

/* Allocates 2D sky precomputation table. */
RTHandle allocateSky2DTable(Vector2 resolution, int index, string name) {
  var table = RTHandles.Alloc((int) resolution.x,
                              (int) resolution.y,
                              dimension: TextureDimension.Tex2D,
                              colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                              enableRandomWrite: true,
                              name: string.Format(name + "{0}", index));
  Debug.Assert(table != null);
  return table;
}

/* Allocates 3D sky precomputation table. */
RTHandle allocateSky3DTable(Vector3 resolution, int index, string name) {
  var table = RTHandles.Alloc((int) resolution.x,
                              (int) resolution.y,
                              (int) resolution.z,
                              dimension: TextureDimension.Tex3D,
                              colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                              enableRandomWrite: true,
                              name: string.Format(name + "{0}", index));
  Debug.Assert(table != null);
  return table;
}


/******************************************************************************/
/************************ END SKY PRECOMPUTATION TABLES ***********************/
/******************************************************************************/








/******************************************************************************/
/****************************** PROCEDURAL STARS ******************************/
/******************************************************************************/

private RTHandle m_proceduralStarTexture;
private RTHandle m_proceduralNebulaeTexture;
private RTHandle m_defaultNebulaeTexture;
private ExpanseCommon.StarTextureResolution m_starTextureResolution;
private ExpanseCommon.StarTextureResolution m_nebulaeTextureResolution;

/* Emulates cubemap with texture 2D array of depth 6. */
RTHandle allocateProceduralStarTexture(Vector2 resolution, string name) {
  var table = RTHandles.Alloc((int) resolution.x,
                              (int) resolution.y,
                              6,
                              dimension: TextureDimension.Tex2DArray,
                              colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                              enableRandomWrite: true,
                              name: name);

  Debug.Assert(table != null);

  return table;
}

RTHandle allocateDefaultNebulaeTexture() {
  var table = RTHandles.Alloc(16, // Make extremely small to make performance hit negligible.
                              16,
                              dimension: TextureDimension.Cube,
                              colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                              enableRandomWrite: true,
                              name: "DefaultNebulaeTexture");

  Debug.Assert(table != null);

  return table;
}

void allocateStarTextures(Expanse sky) {
  if (sky.starTextureQuality.value != m_starTextureResolution.quality) {
    /* Cleanup existing tables. */
    cleanupStarTextures();
    ExpanseCommon.StarTextureResolution starRes = ExpanseCommon.StarQualityToStarTextureResolution(sky.starTextureQuality.value);
    m_proceduralStarTexture = allocateProceduralStarTexture(starRes.Star, "Star");
    m_starTextureResolution = starRes;
  }
  if (sky.nebulaeTextureQuality.value != m_nebulaeTextureResolution.quality) {
    /* Cleanup existing tables. */
    cleanupNebulaeTextures();
    ExpanseCommon.StarTextureResolution nebRes = ExpanseCommon.StarQualityToStarTextureResolution(sky.nebulaeTextureQuality.value);
    m_proceduralNebulaeTexture = allocateProceduralStarTexture(nebRes.Star, "Nebulae");
    m_nebulaeTextureResolution = nebRes;

    /* Reallocate default texture as well. */
    m_defaultNebulaeTexture = allocateDefaultNebulaeTexture();
  }
}

void cleanupStarTextures() {
  RTHandles.Release(m_proceduralStarTexture);
  m_proceduralStarTexture = null;
}

void cleanupNebulaeTextures() {
  RTHandles.Release(m_proceduralNebulaeTexture);
  m_proceduralNebulaeTexture = null;
  RTHandles.Release(m_defaultNebulaeTexture);
  m_defaultNebulaeTexture = null;
}

/******************************************************************************/
/**************************** END PROCEDURAL STARS ****************************/
/******************************************************************************/








/******************************************************************************/
/*********************************** CLOUDS ***********************************/
/******************************************************************************/

/* For keeping track of which cloud layers are enabled. */
private int[] m_enabledCloudLayers;

/* Cloud texture resolution for each layer. */
private ExpanseCommon.CloudTextureResolution[] m_cloudTextureResolutions;

/* Takes up less space by using a single color channel. */
RTHandle allocatedProceduralCloudTexture2D(Vector2 resolution, string name) {
  var table = RTHandles.Alloc((int) resolution.x,
                              (int) resolution.y,
                              dimension: TextureDimension.Tex2D,
                              colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                              enableRandomWrite: true,
                              useMipMap: true,
                              name: name);

  Debug.Assert(table != null);

  return table;
}

/* Takes up less space by using a single color channel. */
RTHandle allocatedProceduralCloudTexture3D(Vector3 resolution, string name) {
  var table = RTHandles.Alloc((int) resolution.x, // HACK
                              (int) resolution.y,
                              (int) resolution.z,
                              dimension: TextureDimension.Tex3D,
                              colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                              enableRandomWrite: true,
                              useMipMap: true,
                              name: name);

  Debug.Assert(table != null);

  return table;
}


/* Struct for holding handles to procedural cloud textures. */
struct CloudNoiseTexture {
  public int dimension;        /* For checking which shader variables to set. */
  public RTHandle coverageTex;
  public RTHandle baseTex;
  public RTHandle structureTex;
  public RTHandle detailTex;
  public RTHandle baseWarpTex;
  public RTHandle detailWarpTex;
};

/* Cloud noise textures for every layer. */
CloudNoiseTexture[] m_cloudNoiseTextures;

CloudNoiseTexture buildCloudNoiseTexture2D(ExpanseCommon.CloudTextureResolution res, int index) {
  CloudNoiseTexture c;
  c.dimension = 2;
  c.coverageTex = allocatedProceduralCloudTexture2D(new Vector2(res.Coverage, res.Coverage), "CloudCoverage_" + index);
  c.baseTex = allocatedProceduralCloudTexture2D(new Vector2(res.Base, res.Base), "CloudBase_" + index);
  c.structureTex = allocatedProceduralCloudTexture2D(new Vector2(res.Structure, res.Structure), "CloudStructure_" + index);
  c.detailTex = allocatedProceduralCloudTexture2D(new Vector2(res.Detail, res.Detail), "CloudDetail_" + index);
  c.baseWarpTex = allocatedProceduralCloudTexture2D(new Vector2(res.BaseWarp, res.BaseWarp), "CloudBaseWarp_" + index);
  c.detailWarpTex = allocatedProceduralCloudTexture2D(new Vector2(res.DetailWarp, res.DetailWarp), "CloudDetailWarp_" + index);
  return c;
}

CloudNoiseTexture buildCloudNoiseTexture3D(ExpanseCommon.CloudTextureResolution res, int index) {
  CloudNoiseTexture c;
  c.dimension = 3;
  /* Coverage is always 2D. */
  c.coverageTex = allocatedProceduralCloudTexture2D(new Vector2(res.Coverage, res.Coverage), "CloudCoverage_" + index);
  c.baseTex = allocatedProceduralCloudTexture3D(new Vector3(res.Base, ExpanseCommon.cloudXZResolutionToYResolution(res.Base), res.Base), "CloudBase_" + index);
  c.structureTex = allocatedProceduralCloudTexture3D(new Vector3(res.Structure, ExpanseCommon.cloudXZResolutionToYResolution(res.Structure), res.Structure), "CloudStructure_" + index);
  c.detailTex = allocatedProceduralCloudTexture3D(new Vector3(res.Detail, ExpanseCommon.cloudXZResolutionToYResolution(res.Detail), res.Detail), "CloudDetail_" + index);
  c.baseWarpTex = allocatedProceduralCloudTexture3D(new Vector3(res.BaseWarp, ExpanseCommon.cloudXZResolutionToYResolution(res.BaseWarp), res.BaseWarp), "CloudBaseWarp_" + index);
  c.detailWarpTex = allocatedProceduralCloudTexture3D(new Vector3(res.DetailWarp, ExpanseCommon.cloudXZResolutionToYResolution(res.DetailWarp), res.DetailWarp), "CloudDetailWarp_" + index);
  return c;
}

void destroyCloudNoiseTexture(CloudNoiseTexture c) {
  RTHandles.Release(c.coverageTex);
  RTHandles.Release(c.baseTex);
  RTHandles.Release(c.structureTex);
  RTHandles.Release(c.detailTex);
  RTHandles.Release(c.baseWarpTex);
  RTHandles.Release(c.detailWarpTex);
  c.coverageTex = null;
  c.baseTex = null;
  c.structureTex = null;
  c.detailTex = null;
  c.baseWarpTex = null;
  c.detailWarpTex = null;
}

void allocateCloudTextures(Expanse sky) {
  /* Count how many layers are enabled. */
  int n = (int) ExpanseCommon.kMaxCloudLayers;
  int numCloudLayersEnabled = 0;
  for (int i = 0; i < n; i++) {
    bool enabled = (((BoolParameter) sky.GetType().GetField("cloudLayerEnabled" + i).GetValue(sky)).value);
    if (enabled) {
      numCloudLayersEnabled++;
    }
  }

  bool layerCountChanged = (numCloudLayersEnabled != m_enabledCloudLayers.Length);

  /* Figure out which ones they are. */
  m_enabledCloudLayers = new int[numCloudLayersEnabled];
  numCloudLayersEnabled = 0;
  for (int i = 0; i < n; i++) {
    bool enabled = (((BoolParameter) sky.GetType().GetField("cloudLayerEnabled" + i).GetValue(sky)).value);
    if (enabled) {
      m_enabledCloudLayers[numCloudLayersEnabled] = i;
      numCloudLayersEnabled++;
    }
  }

  if (!layerCountChanged) {

    /* Check case-by-case if each texture's resolution or type has changed. */
    /* Loop through all the enabled textures and allocate each one. */
    for (int i = 0; i < m_enabledCloudLayers.Length; i++) {

      int layerIndex = m_enabledCloudLayers[i];
      ExpanseCommon.CloudTextureQuality quality = ((EnumParameter<ExpanseCommon.CloudTextureQuality>) sky.GetType().GetField("cloudNoiseQuality" + layerIndex).GetValue(sky)).value;
      ExpanseCommon.CloudGeometryType cloudGeometryType = ((EnumParameter<ExpanseCommon.CloudGeometryType>) sky.GetType().GetField("cloudGeometryType" + layerIndex).GetValue(sky)).value;
      ExpanseCommon.CloudNoiseDimension dimension = ExpanseCommon.cloudGeometryTypeToDimension[cloudGeometryType];

      if (quality != m_cloudTextureResolutions[i].quality && dimension != m_cloudTextureResolutions[i].dimension) {
        /* Have to update these tables. */
        ExpanseCommon.CloudTextureResolution res = ExpanseCommon.cloudQualityToCloudTextureResolution(quality, dimension);
        m_cloudTextureResolutions[i] = res;
        if (dimension == ExpanseCommon.CloudNoiseDimension.TwoD) {
          m_cloudNoiseTextures[i] = buildCloudNoiseTexture2D(res, i);
        } else if (dimension == ExpanseCommon.CloudNoiseDimension.ThreeD) {
          m_cloudNoiseTextures[i] = buildCloudNoiseTexture3D(res, i);
        } else {
          // TODO: Unhandled.
        }
      }

    }

    return;
  }

  /* Signal that we also need to reallocate the cloud framebuffers, and
   * cleanup all the existing textures. */
  reallocateCloudFramebuffers = true;
  cleanupCloudTextures();

  /* Recreate the arrays of texture resolutions and textures. */
  m_cloudTextureResolutions = new ExpanseCommon.CloudTextureResolution[m_enabledCloudLayers.Length];
  m_cloudNoiseTextures = new CloudNoiseTexture[m_enabledCloudLayers.Length];

  /* Loop through all the enabled textures and allocate each one. */
  for (int i = 0; i < m_enabledCloudLayers.Length; i++) {
    int layerIndex = m_enabledCloudLayers[i];
    ExpanseCommon.CloudGeometryType cloudGeometryType = ((EnumParameter<ExpanseCommon.CloudGeometryType>) sky.GetType().GetField("cloudGeometryType" + layerIndex).GetValue(sky)).value;
    ExpanseCommon.CloudTextureQuality quality = ((EnumParameter<ExpanseCommon.CloudTextureQuality>) sky.GetType().GetField("cloudNoiseQuality" + layerIndex).GetValue(sky)).value;
    ExpanseCommon.CloudNoiseDimension dimension = ExpanseCommon.cloudGeometryTypeToDimension[cloudGeometryType];
    ExpanseCommon.CloudTextureResolution res = ExpanseCommon.cloudQualityToCloudTextureResolution(quality, dimension);
    m_cloudTextureResolutions[i] = res;

    if (dimension == ExpanseCommon.CloudNoiseDimension.TwoD) {
      m_cloudNoiseTextures[i] = buildCloudNoiseTexture2D(res, i);
    } else if (dimension == ExpanseCommon.CloudNoiseDimension.ThreeD) {
      m_cloudNoiseTextures[i] = buildCloudNoiseTexture3D(res, i);
    } else {
      // TODO: Unhandled.
    }
  }

}


void cleanupCloudTextures() {
  /* To be safe, use the length of cloud noise textures. */
  for (int i = 0; i < m_cloudNoiseTextures.Length; i++) {
    destroyCloudNoiseTexture(m_cloudNoiseTextures[i]);
  }
}

/******************************************************************************/
/********************************* END CLOUDS *********************************/
/******************************************************************************/








/******************************************************************************/
/******************************** FRAMEBUFFERS ********************************/
/******************************************************************************/

private bool reallocateCloudFramebuffers = true;

/* 2 tables:
 *  1. Sky color. RGBA where A isn't used (currently).
 *  2. Sky transmittance. RGBA where A isn't used (currently). Computed
 *    using depth buffer. */
struct SkyRenderTexture {
  public RTHandle colorBuffer;
};
SkyRenderTexture m_fullscreenSkyRT;
SkyRenderTexture m_cubemapSkyRT;

/* 2 sets of 2 tables:
 *  1. Clouds lighting. RGBA where A isn't used (currently).
 *  2. Clouds transmittance. RGBA where RGB is transmittance and A is
 *    atmospheric blend factor.
 * There are 2 sets of tables because we need to maintain a "previous"
 * table for reprojection. We alternate between which is the previous
 * and which is the current render texture set to avoid  */
struct CloudRenderTexture {
 public RTHandle colorBuffer;         /* Color and blend. */
 public RTHandle transmittanceBuffer; /* Transmittance and hit depth for compositing. */
};

/* Compositing. */
CloudRenderTexture[] m_fullscreenCloudCompositeRT;
CloudRenderTexture[] m_cubemapCloudCompositeRT;
int m_currentFullscreenCloudsRT;
int m_currentCubemapCloudsRT;

/* Individual layers. */
CloudRenderTexture[] m_fullscreenCloudLayerRT;
CloudRenderTexture[] m_cubemapCloudLayerRT;

/* For keeping track of resolutions. */
Vector2 m_currentFullscreenRTSize;
Vector2 m_currentCubemapRTSize;

SkyRenderTexture buildSkyRenderTexture(Vector2 resolution, int index, string name) {
  SkyRenderTexture r = new SkyRenderTexture();
  r.colorBuffer = allocateSky2DTable(resolution, index, name + "_Color");
  return r;
}

CloudRenderTexture buildCloudRenderTexture(Vector2 resolution, int index, string name) {
  CloudRenderTexture r = new CloudRenderTexture();
  r.colorBuffer = allocateSky2DTable(resolution, index, name + "_Color");
  r.transmittanceBuffer = allocateSky2DTable(resolution, index, name + "_Transmittance");
  return r;
}

private void buildFullscreenRenderTextures(Vector2 resolution) {
  /* Sky. */
  m_fullscreenSkyRT = buildSkyRenderTexture(resolution, 0, "fullscreenSkyRT");

  /* Cloud compositing. */
  m_fullscreenCloudCompositeRT = new CloudRenderTexture[2];
  for (int i = 0; i < 2; i++) {
    m_fullscreenCloudCompositeRT[i] = buildCloudRenderTexture(resolution, i, "fullscreenCloudCompositeRT");
  }
  m_currentFullscreenCloudsRT = 0;

  /* Individual cloud layers. */
  m_fullscreenCloudLayerRT = new CloudRenderTexture[m_enabledCloudLayers.Length];
  for (int i = 0; i < m_enabledCloudLayers.Length; i++) {
    m_fullscreenCloudLayerRT[i] = buildCloudRenderTexture(resolution, i, "fullscreenCloudRT_Layer" + i);
  }

  m_currentFullscreenRTSize = resolution;
}

private void buildCubemapRenderTextures(Vector2 resolution) {
  /* Sky. */
  m_cubemapSkyRT = buildSkyRenderTexture(resolution, 0, "cubemapSkyRT");

  /* Cloud compositing. */
  m_cubemapCloudCompositeRT = new CloudRenderTexture[2];
  for (int i = 0; i < 2; i++) {
    m_cubemapCloudCompositeRT[i] = buildCloudRenderTexture(resolution, i, "cubemapCloudCompositeRT");
  }
  m_currentCubemapCloudsRT = 0;

  /* Individual cloud layers. */
  m_cubemapCloudLayerRT = new CloudRenderTexture[m_enabledCloudLayers.Length];
  for (int i = 0; i < m_enabledCloudLayers.Length; i++) {
    m_cubemapCloudLayerRT[i] = buildCloudRenderTexture(resolution, i, "cubemapCloudRT_Layer" + i);
  }

  m_currentCubemapRTSize = resolution;
}

private void cleanupFullscreenRenderTextures() {
  RTHandles.Release(m_fullscreenSkyRT.colorBuffer);
  m_fullscreenSkyRT.colorBuffer = null;

  for (int i = 0; i < 2; i++) {
    RTHandles.Release(m_fullscreenCloudCompositeRT[i].colorBuffer);
    RTHandles.Release(m_fullscreenCloudCompositeRT[i].transmittanceBuffer);
    m_fullscreenCloudCompositeRT[i].colorBuffer = null;
    m_fullscreenCloudCompositeRT[i].transmittanceBuffer = null;
  }

  for (int i = 0; i < m_fullscreenCloudLayerRT.Length; i++) {
    RTHandles.Release(m_fullscreenCloudLayerRT[i].colorBuffer);
    RTHandles.Release(m_fullscreenCloudLayerRT[i].transmittanceBuffer);
    m_fullscreenCloudLayerRT[i].colorBuffer = null;
    m_fullscreenCloudLayerRT[i].transmittanceBuffer = null;
  }
}

private void cleanupCubemapRenderTextures() {
  RTHandles.Release(m_cubemapSkyRT.colorBuffer);
  m_cubemapSkyRT.colorBuffer = null;

  for (int i = 0; i < 2; i++) {
    RTHandles.Release(m_cubemapCloudCompositeRT[i].colorBuffer);
    RTHandles.Release(m_cubemapCloudCompositeRT[i].transmittanceBuffer);
    m_cubemapCloudCompositeRT[i].colorBuffer = null;
    m_cubemapCloudCompositeRT[i].transmittanceBuffer = null;
  }

  for (int i = 0; i < m_cubemapCloudLayerRT.Length; i++) {
    RTHandles.Release(m_cubemapCloudLayerRT[i].colorBuffer);
    RTHandles.Release(m_cubemapCloudLayerRT[i].transmittanceBuffer);
    m_cubemapCloudLayerRT[i].colorBuffer = null;
    m_cubemapCloudLayerRT[i].transmittanceBuffer = null;
  }
}

/******************************************************************************/
/****************************** END FRAMEBUFFERS ******************************/
/******************************************************************************/








/******************************************************************************/
/****************************** MEMBER VARIABLES ******************************/
/******************************************************************************/

Material m_skyMaterial;
MaterialPropertyBlock m_PropertyBlock = new MaterialPropertyBlock();
ComputeShader m_skyCS;
ComputeShader m_starCS;
ComputeShader m_cloudCS;

/* Hash values for determining update behavior. */
int m_LastSkyHash;
int m_LastCloudHash;
int m_LastNightSkyHash;
Vector4 m_averageNightSkyColor;
private static int m_RenderCubemapSkyID = 0;
private static int m_RenderFullscreenSkyID = 1;
private static int m_RenderCubemapCloudsID = 2;
private static int m_RenderFullscreenCloudsID = 3;
private static int m_CompositeCubemapCloudsID = 4;
private static int m_CompositeFullscreenCloudsID = 5;
private static int m_CompositeCubemapSkyAndCloudsID = 6;
private static int m_CompositeFullscreenSkyAndCloudsID = 7;

/* Profiling samplers. */
ProfilingSampler m_DrawProfilingSampler = new ProfilingSampler("Expanse: Draw Sky");
ProfilingSampler m_SkyViewLUTProfilingSampler = new ProfilingSampler("Expanse: Compute Sky View LUT");
ProfilingSampler m_SkyAPLUTProfilingSampler = new ProfilingSampler("Expanse: Compute Aerial Perspective LUT");
ProfilingSampler m_SkyTLUTProfilingSampler = new ProfilingSampler("Expanse: Compute Transmittance LUT");
ProfilingSampler m_SkyMSLUTProfilingSampler = new ProfilingSampler("Expanse: Compute Multiple Scattering LUT");
ProfilingSampler m_StarProfilingSampler = new ProfilingSampler("Expanse: Generate Star Texture");
ProfilingSampler m_NebulaeProfilingSampler = new ProfilingSampler("Expanse: Generate Nebulae Texture");
ProfilingSampler m_DrawCloudsProfilingSampler = new ProfilingSampler("Expanse: Draw Cloud Layers");
ProfilingSampler m_GenerateCloudsProfilingSampler = new ProfilingSampler("Expanse: Generate Cloud Noises");

/******************************************************************************/
/**************************** END MEMBER VARIABLES ****************************/
/******************************************************************************/







public override void Build() {
  /* Create material for sky shader. */
  m_skyMaterial = CoreUtils.CreateEngineMaterial(GetSkyShader());

  /* Get handles to compute shaders. */
  m_skyCS = GetExpanseSkyPrecomputeShader();
  m_starCS = GetExpanseStarPrecomputeShader();
  m_cloudCS = GetExpanseCloudPrecomputeShader();

  /* Set up default cloud arrays. */
  m_enabledCloudLayers = new int[0];
  m_skyTextureResolution = ExpanseCommon.skyQualityToSkyTextureResolution(ExpanseCommon.SkyTextureQuality.Medium);
  m_cloudTextureResolutions = new ExpanseCommon.CloudTextureResolution[m_enabledCloudLayers.Length];
  for (int i = 0; i < m_enabledCloudLayers.Length; i++) {
    m_cloudTextureResolutions[i] = ExpanseCommon.cloudQualityToCloudTextureResolution(ExpanseCommon.CloudTextureQuality.Medium, ExpanseCommon.CloudNoiseDimension.TwoD);
  }
  m_cloudNoiseTextures = new CloudNoiseTexture[m_enabledCloudLayers.Length];

  /* Create the framebuffers we'll use for our multi-pass strategy.
   * This width and height is a best guess. It will be reset in the render
   * function if it's wrong. */
  buildFullscreenRenderTextures(new Vector2(Screen.width, Screen.height));
  buildCubemapRenderTextures(new Vector2(Screen.width, Screen.height));
}

/* Returns reference to Expanse sky shader. */
Shader GetSkyShader() {
  return Shader.Find("Hidden/HDRP/Sky/Expanse");
}

/* Returns reference to expanse sky precompute shader. */
ComputeShader GetExpanseSkyPrecomputeShader() {
    return Resources.Load<ComputeShader>("ExpanseSkyPrecompute");
}

/* Returns reference to expanse sky precompute shader. */
ComputeShader GetExpanseStarPrecomputeShader() {
    return Resources.Load<ComputeShader>("ExpanseStarPrecompute");
}

/* Returns reference to expanse sky precompute shader. */
ComputeShader GetExpanseCloudPrecomputeShader() {
  return Resources.Load<ComputeShader>("ExpanseCloudPrecompute");
}

public override void Cleanup()
{
  CoreUtils.Destroy(m_skyMaterial);

  cleanupFullscreenRenderTextures();
  cleanupCubemapRenderTextures();
  cleanupSkyTables();
  cleanupStarTextures();
  cleanupCloudTextures();
}

private void setLightingData(CommandBuffer cmd, Vector4 cameraPos, Expanse sky) {
  /* Use data from the property block, so that we don't go out of
   * sync. */
  float planetRadius = sky.planetRadius.value;
  float atmosphereRadius = planetRadius + sky.atmosphereThickness.value;
  Vector3 O = cameraPos - ((new Vector4(0, -sky.planetRadius.value, 0, 0)) + (new Vector4(sky.planetOriginOffset.value.x, sky.planetOriginOffset.value.y, sky.planetOriginOffset.value.z, 0)));
  Vector3 O3 = new Vector3(O.x, O.y, O.z);
  for (int i = 0; i < m_numBodiesEnabled; i++) {
    /* Check if the body is occluded by the planet. */
    Vector3 L = new Vector3(m_bodyDirections[i].x, m_bodyDirections[i].y, m_bodyDirections[i].z);
    Vector3 intersection = ExpanseCommon.intersectSphere(O3, L, planetRadius);
    if (intersection.z >= 0 && (intersection.x >= 0 || intersection.y >= 0)) {
      ExpanseCommon.bodyTransmittances[i] = new Vector3(0, 0, 0);
    } else {
      /* Sample transmittance. */
      Vector3 skyIntersection = ExpanseCommon.intersectSphere(O3, L, atmosphereRadius);
      float r = O3.magnitude;
      float mu = Vector3.Dot(Vector3.Normalize(O3), L);
      float d = (skyIntersection.x > 0) ? (skyIntersection.y > 0 ? Mathf.Min(skyIntersection.x, skyIntersection.y) : skyIntersection.x) : skyIntersection.y;
      Vector2 uv = ExpanseCommon.map_r_mu_transmittance(r, mu, atmosphereRadius, planetRadius,
        d, false, m_skyTextureResolution.T.x);
      Vector4 transmittance = m_skyTCPU.GetPixelBilinear(uv.x, uv.y);


      /* Calculate transmittance for the analytical layers. */
      /* P is the point to compute distance to attenuate from. */
      /* Integrate analytically. TODO: doesn't work for tent function yet. */
      Vector3 power = new Vector3(0, 0, 0);
      for (int j = 0; j < m_numAtmosphereLayersEnabled; j++) {
        ExpanseCommon.DensityDistribution densityDistribution = ((EnumParameter<ExpanseCommon.DensityDistribution>) sky.GetType().GetField("layerDensityDistribution" + j).GetValue(sky)).value;
        if (densityDistribution == ExpanseCommon.DensityDistribution.ExponentialAttenuated) {
          float m = ((MinFloatParameter) sky.GetType().GetField("layerAttenuationDistance" + j).GetValue(sky)).value;
          // TODO: current integration strategy makes this non-physical
          // float k = ((MinFloatParameter) sky.GetType().GetField("layerAttenuationBias" + j).GetValue(sky)).value;
          float H = ((MinFloatParameter) sky.GetType().GetField("layerThickness" + j).GetValue(sky)).value;
          Vector3 P = O;
          bool useCameraPos = ((BoolParameter) sky.GetType().GetField("layerDensityAttenuationPlayerOrigin" + j).GetValue(sky)).value;
          if (!useCameraPos) {
            P = ((Vector3Parameter) sky.GetType().GetField("layerDensityAttenuationOrigin" + j).GetValue(sky)).value - (new Vector3(0, -planetRadius, 0));
          }
          Vector3 deltaPO = O3 - P;
          float a = 1 / (m * m);
          float b = ((-2 * Vector3.Dot(deltaPO, L)) / (m * m)) - (Vector3.Dot(L, Vector3.Normalize(O3)) / H);
          // float c = ((planetRadius - r) / H) + ((k * k - Vector3.Dot(deltaPO, deltaPO)) / (m * m));
          float c = ((planetRadius - r) / H) + ((-Vector3.Dot(deltaPO, deltaPO)) / (m * m));

          float prefactor = Mathf.Exp(c + (b * b) / (4 * a)) * Mathf.Sqrt(Mathf.PI) / (2 * Mathf.Sqrt(a));

          float erf_f = ExpanseCommon.erf((2 * a * d - b) / (2 * Mathf.Sqrt(a)));
          float erf_0 = ExpanseCommon.erf((-b) / (2 * Mathf.Sqrt(a)));

          float layerDensity = ((MinFloatParameter) sky.GetType().GetField("layerDensity" + j).GetValue(sky)).value;
          Vector3 coefficients = ((Vector3Parameter) sky.GetType().GetField("layerCoefficientsA" + j).GetValue(sky)).value;

          float opticalDepth = layerDensity * prefactor * (erf_f - erf_0);

          Vector3 contrib = opticalDepth * coefficients;
          contrib = new Vector3(Mathf.Max(contrib.x, 0), Mathf.Max(contrib.y, 0), Mathf.Max(contrib.z, 0));

          power += contrib;
        }
      }

      power = -power;

      ExpanseCommon.bodyTransmittances[i] = new Vector3(Mathf.Exp(transmittance.x + power.x), Mathf.Exp(transmittance.y + power.y), Mathf.Exp(transmittance.z + power.z));
    }
  }
}

private Vector4 computeAverageNightSkyColor(Expanse sky) {
  if (sky.useProceduralNightSky.value) {
    /* Use an analytical hack to allow for realtime editing.
     * HACK: fudge factor here to make the difference between this and
     * a texture less extreme. You can think of this as an approximation
     * of the "fraction of the night sky that's a star". Don't tie this to
     * the procedural star density though, because that just needlessly
     * complicates things. */
    float approximateStarDensity = 0.0025f;
    return sky.nightSkyIntensity.value * sky.nightSkyTint.value * approximateStarDensity;
  } else if (sky.nightSkyTexture.value != null) {
    /* Actually compute the average. */
    Vector4 averageColor = new Vector4(0, 0, 0, 0);
    for (int i = 0; i < 6; i++) {
      Vector4 faceColor = new Vector4(0, 0, 0, 0);
      Color[] pixels = sky.nightSkyTexture.value.GetPixels(0);
      for (int p = 0; p < pixels.Length; p += 16) {
        Color c = pixels[p];
        faceColor += (Vector4) c;
      }
      faceColor /= pixels.Length;
      averageColor += faceColor;
    }
    averageColor /= 6;
    return sky.nightSkyIntensity.value * sky.nightSkyTint.value * averageColor;
  } else {
    return sky.nightSkyTint.value;
  }
}

protected override bool Update(BuiltinSkyParameters builtinParams)
{
  if (m_skyTNeedsUpdate) {
    RenderTexture.active = m_skyT;
    m_skyTCPU.ReadPixels(new Rect(0, 0, m_skyT.rt.width, m_skyT.rt.height), 0, 0);
    m_skyTCPU.Apply();
    RenderTexture.active = null;
    m_skyTNeedsUpdate = false;
  }

  var sky = builtinParams.skySettings as Expanse;

  allocateSkyPrecomputationTables(sky);
  allocateStarTextures(sky);
  allocateCloudTextures(sky);

  setMaterialPropertyBlock(builtinParams);
  setGlobalCBuffer(builtinParams);

  /* Check the sky hash and recompute if necessary. */
  int currentSkyHash = sky.GetSkyHashCode();
  if (currentSkyHash != m_LastSkyHash) {
    setSkyPrecomputationTables();
    DispatchSkyPrecompute(builtinParams.commandBuffer);
    m_skyTNeedsUpdate = true;
    m_LastSkyHash = currentSkyHash;
  }

  /* Render the single scattering, multiple scattering, and aerial perspective
   * textures. */
  setSkyRealtimeTables();
  DispatchSkyRealtimeCompute(builtinParams.commandBuffer);

  int currentCloudHash = sky.GetCloudHashCode();
  if (currentCloudHash != m_LastCloudHash) {
    DispatchCloudCompute(builtinParams.commandBuffer, sky);
    m_LastCloudHash = currentCloudHash;
  }

  /* Check if we need to recompute the average night sky color and night
   * sky texture. */
  int currentNightSkyHash = sky.GetNightSkyHashCode();
  if (currentNightSkyHash != m_LastNightSkyHash) {
    if (sky.useProceduralNightSky.value) {
      /* Generate nebula first so we can use it to affect star density. */
      if (sky.useProceduralNebulae.value) {
        setNebulaeRWTextures();
        DispatchNebulaeCompute(builtinParams.commandBuffer);
      }
      setStarRWTextures();
      DispatchStarCompute(builtinParams.commandBuffer);
    }
    m_averageNightSkyColor = computeAverageNightSkyColor(sky);
    m_LastNightSkyHash = currentNightSkyHash;
  }

  /* Set lighting properties so that light scripts can use them to affect
   * the directional lights in the scene. */
  setLightingData(builtinParams.commandBuffer, builtinParams.worldSpaceCameraPos, sky);

  return false;
}






/******************************************************************************/
/****************************** RENDER FUNCTIONS ******************************/
/******************************************************************************/

private void RenderSkyPass(BuiltinSkyParameters builtinParams, bool renderForCubemap) {
  int skyPassID = renderForCubemap ? m_RenderCubemapSkyID : m_RenderFullscreenSkyID;
  SkyRenderTexture outTex = renderForCubemap ? m_cubemapSkyRT : m_fullscreenSkyRT;
  RenderTargetIdentifier[] outputs = new RenderTargetIdentifier[] {
    new RenderTargetIdentifier(outTex.colorBuffer),
  };
  if (renderForCubemap) {
    CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_skyMaterial,
      outputs, m_PropertyBlock, skyPassID);
  } else {
    CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_skyMaterial,
      outputs, builtinParams.depthBuffer, m_PropertyBlock, skyPassID);
  }
}

private void RenderCloudsPass(BuiltinSkyParameters builtinParams, bool renderForCubemap) {
  CommandBuffer cmd = builtinParams.commandBuffer;
  var sky = builtinParams.skySettings as Expanse;

  /* Only render fullscreen clouds for the moment. */
  if (!renderForCubemap) {
    /* TODO: might wanna set sky view LUT for GI. Cool we can do that!
     * multi-pass, f-yeah! */

    /* Render out each enabled cloud layer. */
    for (int i = 0; i < m_enabledCloudLayers.Length; i++) {
      int layerIndex = m_enabledCloudLayers[i];
      SetGlobalCloudTextures(cmd, sky, i, layerIndex);

      /* Render to this layer's cloud render texture. */
      RenderTargetIdentifier[] outputs = new RenderTargetIdentifier[] {
        new RenderTargetIdentifier(m_fullscreenCloudLayerRT[i].colorBuffer),
        new RenderTargetIdentifier(m_fullscreenCloudLayerRT[i].transmittanceBuffer)};
      CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_skyMaterial,
          outputs, m_PropertyBlock, m_RenderFullscreenCloudsID);
    }
  }
}

private void RenderCloudsCompositePass(BuiltinSkyParameters builtinParams, bool renderForCubemap) {
  /* Only composite fullscreen clouds for the moment. */
  if (!renderForCubemap) {

    /* Set all the render textures. */
    for (int i = 0; i < m_fullscreenCloudLayerRT.Length; i++) {
      /* Set the layer we're shading as a uniform variable. */
      m_PropertyBlock.SetTexture("_cloudColorLayer" + i, m_fullscreenCloudLayerRT[i].colorBuffer);
      m_PropertyBlock.SetTexture("_cloudTransmittanceLayer" + i, m_fullscreenCloudLayerRT[i].transmittanceBuffer);
    }

    /* Get the current and previous cloud render textures. */
    CloudRenderTexture prevTex = m_fullscreenCloudCompositeRT[(m_currentFullscreenCloudsRT+1)%2];
    CloudRenderTexture currTex = m_fullscreenCloudCompositeRT[m_currentFullscreenCloudsRT];

    /* Set the previous cloud render texture for use in temporal accumulation. */
    m_PropertyBlock.SetTexture("_lastFullscreenCloudColorRT", prevTex.colorBuffer);
    m_PropertyBlock.SetTexture("_lastFullscreenCloudTransmittanceRT", prevTex.transmittanceBuffer);

    /* Render to the current fullscreen cloud compositing texture. */
    RenderTargetIdentifier[] outputs = new RenderTargetIdentifier[] {
      new RenderTargetIdentifier(currTex.colorBuffer),
      new RenderTargetIdentifier(currTex.transmittanceBuffer)};
    CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_skyMaterial,
        outputs, m_PropertyBlock, m_CompositeFullscreenCloudsID);
  }
}

private void RenderCompositePass(BuiltinSkyParameters builtinParams, bool renderForCubemap) {
  /* Set all the textures we just rendered to to be available in the
   * shader. */

  /* Composite. */
  int compositePassID = renderForCubemap ?
    m_CompositeCubemapSkyAndCloudsID : m_CompositeFullscreenSkyAndCloudsID;

  if (renderForCubemap) {
    m_PropertyBlock.SetTexture("_cubemapSkyColorRT", m_cubemapSkyRT.colorBuffer);
    m_PropertyBlock.SetTexture("_currCubemapCloudColorRT", m_cubemapCloudCompositeRT[m_currentCubemapCloudsRT].colorBuffer);
    m_PropertyBlock.SetTexture("_currCubemapCloudTransmittanceRT", m_cubemapCloudCompositeRT[m_currentCubemapCloudsRT].transmittanceBuffer);
    CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_skyMaterial,
      builtinParams.colorBuffer, m_PropertyBlock, compositePassID);
    m_currentCubemapCloudsRT = (m_currentCubemapCloudsRT + 1) % 2;
  } else {
    m_PropertyBlock.SetTexture("_fullscreenSkyColorRT", m_fullscreenSkyRT.colorBuffer);
    m_PropertyBlock.SetTexture("_currFullscreenCloudColorRT", m_fullscreenCloudCompositeRT[m_currentFullscreenCloudsRT].colorBuffer);
    m_PropertyBlock.SetTexture("_currFullscreenCloudTransmittanceRT", m_fullscreenCloudCompositeRT[m_currentFullscreenCloudsRT].transmittanceBuffer);
    CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_skyMaterial,
      builtinParams.colorBuffer, builtinParams.depthBuffer, m_PropertyBlock, compositePassID);
    m_currentFullscreenCloudsRT = (m_currentFullscreenCloudsRT + 1) % 2;
  }
}

private void checkAndResizeFramebuffers(BuiltinSkyParameters builtinParams, bool renderForCubemap) {
  /* Compute the number of layers we have enabled. */
  Vector2 currSize = (renderForCubemap) ? m_currentCubemapRTSize : m_currentFullscreenRTSize;
  Vector2Int trueSize = Vector2Int.Max(new Vector2Int(1, 1),
    builtinParams.colorBuffer.GetScaledSize(builtinParams.colorBuffer.referenceSize));
  if (currSize.x != trueSize.x || currSize.y != trueSize.y || reallocateCloudFramebuffers) {
    if (reallocateCloudFramebuffers) {
      cleanupCubemapRenderTextures();
      buildCubemapRenderTextures(trueSize);
      cleanupFullscreenRenderTextures();
      buildFullscreenRenderTextures(trueSize);
      reallocateCloudFramebuffers = false;
    }
    else if (renderForCubemap) {
      cleanupCubemapRenderTextures();
      buildCubemapRenderTextures(trueSize);
    } else {
      cleanupFullscreenRenderTextures();
      buildFullscreenRenderTextures(trueSize);
    }
  }
}

public override void RenderSky(BuiltinSkyParameters builtinParams,
  bool renderForCubemap, bool renderSunDisk) {
  using (new ProfilingScope(builtinParams.commandBuffer, m_DrawProfilingSampler)) {

    /* Check whether or not we have to resize the framebuffers, and do it
     * if we have to. */
    checkAndResizeFramebuffers(builtinParams, renderForCubemap);

    /* Render sky pass. */
    RenderSkyPass(builtinParams, renderForCubemap);

    /* Render clouds pass. */
    RenderCloudsPass(builtinParams, renderForCubemap);

    /* Composite the clouds. */
    RenderCloudsCompositePass(builtinParams, renderForCubemap);

    /* Composite the two together. */
    RenderCompositePass(builtinParams, renderForCubemap);
  }
}

/******************************************************************************/
/**************************** END RENDER FUNCTIONS ****************************/
/******************************************************************************/








/******************************************************************************/
/************************** COMPUTE SHADER FUNCTIONS **************************/
/******************************************************************************/

private void DispatchSkyPrecompute(CommandBuffer cmd) {
  if (m_numAtmosphereLayersEnabled > 0) {
    int handle_T = m_skyCS.FindKernel("T");
    int handle_MS = m_skyCS.FindKernel("MS");

    using (new ProfilingScope(cmd, m_SkyTLUTProfilingSampler)) {
      cmd.DispatchCompute(m_skyCS, handle_T,
        (int) m_skyTextureResolution.T.x / 8,
        (int) m_skyTextureResolution.T.y / 8, 1);
    }

    using (new ProfilingScope(cmd, m_SkyMSLUTProfilingSampler)) {
      cmd.DispatchCompute(m_skyCS, handle_MS,
        (int) m_skyTextureResolution.MS.x / 8,
        (int) m_skyTextureResolution.MS.y / 8, 1);
    }
  }
}

private void DispatchSkyRealtimeCompute(CommandBuffer cmd) {
  if (m_numAtmosphereLayersEnabled > 0) {
    int handle_SS = m_skyCS.FindKernel("SS");
    int handle_MSAcc = m_skyCS.FindKernel("MSAcc");
    int handle_AP = m_skyCS.FindKernel("AP");

    using (new ProfilingScope(cmd, m_SkyViewLUTProfilingSampler)) {
      cmd.DispatchCompute(m_skyCS, handle_SS,
        (int) (m_skyTextureResolution.SS.x) / 8,
        (int) (m_skyTextureResolution.SS.y) / 8, 1);

      cmd.DispatchCompute(m_skyCS, handle_MSAcc,
        (int) (m_skyTextureResolution.MSAccumulation.x) / 8,
        (int) (m_skyTextureResolution.MSAccumulation.y) / 8, 1);
    }

    using (new ProfilingScope(cmd, m_SkyAPLUTProfilingSampler)) {
      cmd.DispatchCompute(m_skyCS, handle_AP,
        (int) (m_skyTextureResolution.AP.x) / 4,
        (int) (m_skyTextureResolution.AP.y) / 4,
        (int) (m_skyTextureResolution.AP.z) / 4);
    }
  }
}

private void DispatchStarCompute(CommandBuffer cmd) {
  using (new ProfilingScope(cmd, m_StarProfilingSampler)) {
    int handle_Star = m_starCS.FindKernel("STAR");

    cmd.DispatchCompute(m_starCS, handle_Star,
      (int) m_starTextureResolution.Star.x / 8,
      (int) m_starTextureResolution.Star.y / 8, 6);
  }
}

private void DispatchNebulaeCompute(CommandBuffer cmd) {
  using (new ProfilingScope(cmd, m_NebulaeProfilingSampler)) {
    int handle_Nebulae = m_starCS.FindKernel("NEBULAE");

    cmd.DispatchCompute(m_starCS, handle_Nebulae,
      (int) m_nebulaeTextureResolution.Star.x / 8,
      (int) m_nebulaeTextureResolution.Star.y / 8, 6);
  }
}

private void DispatchCloudCompute(CommandBuffer cmd, Expanse sky) {
  using (new ProfilingScope(cmd, m_GenerateCloudsProfilingSampler)) {
    /* Update the cloud noise tables. */
    for (int i = 0; i < m_enabledCloudLayers.Length; i++) {
      /* Get the index of this layer. */
      int layerIndex = m_enabledCloudLayers[i];
      /* Get its textures. */
      CloudNoiseTexture tex = m_cloudNoiseTextures[i];
      ExpanseCommon.CloudTextureResolution res = m_cloudTextureResolutions[i];
      if (tex.dimension == 2) {
        DispatchCloudCompute2D(cmd, sky, i, layerIndex, res, tex);
      } else if (tex.dimension == 3) {
        DispatchCloudCompute3D(cmd, sky, i, layerIndex, res, tex);
      }
    }
  }
}

private void DispatchCloudCompute2D(CommandBuffer cmd, Expanse sky, int layer,
  int layerIndex, ExpanseCommon.CloudTextureResolution res, CloudNoiseTexture tex) {
  string[] layerName = {"cloudCoverage", "cloudBase",
    "cloudStructure", "cloudDetail",
    "cloudBaseWarp", "cloudDetailWarp"};
  // string[] kernelName = {"VALUE2D", "WORLEY2D", "VALUE2D", "VALUE2D", "VALUE2D", "VALUE2D"};
  RTHandle[] noiseTexture = {tex.coverageTex, tex.baseTex, tex.structureTex,
    tex.detailTex, tex.baseWarpTex, tex.detailWarpTex};
  int[] resolution = {res.Coverage, res.Base, res.Structure, res.Detail,
    res.BaseWarp, res.DetailWarp};

  for (int i = 0; i < layerName.Length; i++) {
    bool procedural = ((BoolParameter) sky.GetType().GetField(layerName[i] + "NoiseProcedural" + layerIndex).GetValue(sky)).value;
    if (procedural) {
      /* Gather and set parameters. */
      ExpanseCommon.CloudNoiseType noiseType = ((EnumParameter<ExpanseCommon.CloudNoiseType>) sky.GetType().GetField(layerName[i] + "NoiseType" + layerIndex).GetValue(sky)).value;
      string kernelName = ExpanseCommon.cloudNoiseTypeToKernelName[noiseType] + "2D";
      Vector2 gridScale = ((Vector2Parameter) sky.GetType().GetField(layerName[i] + "GridScale" + layerIndex).GetValue(sky)).value;
      int octaves = ((ClampedIntParameter) sky.GetType().GetField(layerName[i] + "Octaves" + layerIndex).GetValue(sky)).value;
      float octaveScale = ((MinFloatParameter) sky.GetType().GetField(layerName[i] + "OctaveScale" + layerIndex).GetValue(sky)).value;
      float octaveMultiplier = ((MinFloatParameter) sky.GetType().GetField(layerName[i] + "OctaveMultiplier" + layerIndex).GetValue(sky)).value;

      /* HACK: workaround overwriting data. */
      ComputeShader cs = (ComputeShader) UnityEngine.Object.Instantiate(m_cloudCS);
      int handle = cs.FindKernel(kernelName);

      /* Set all the parameters. */
      cs.SetTexture(handle, "Noise_2D", noiseTexture[i]);
      cs.SetVector("_resNoise", new Vector4(resolution[i], resolution[i], 0, 0));
      cs.SetVector("_gridScale", new Vector4(gridScale.x, gridScale.y, 1, 1));
      cs.SetFloat("_octaveScale", octaveScale);
      cs.SetFloat("_octaveMultiplier", octaveMultiplier);
      cs.SetInt("_octaves", octaves);

      /* Dispatch! */
      cmd.DispatchCompute(cs, handle,
        (int) resolution[i] / 8,
        (int) resolution[i] / 8, 1);
    }
  }
}

private void DispatchCloudCompute3D(CommandBuffer cmd, Expanse sky, int layer,
  int layerIndex, ExpanseCommon.CloudTextureResolution res, CloudNoiseTexture tex) {
  string[] layerName = {"cloudCoverage", "cloudBase",
    "cloudStructure", "cloudDetail",
    "cloudBaseWarp", "cloudDetailWarp"};
  // string[] kernelName = {"VALUE2D", "WORLEY2D", "VALUE2D", "VALUE2D", "VALUE2D", "VALUE2D"};
  RTHandle[] noiseTexture = {tex.coverageTex, tex.baseTex, tex.structureTex,
    tex.detailTex, tex.baseWarpTex, tex.detailWarpTex};
  int[] resolution = {res.Coverage, res.Base, res.Structure, res.Detail,
    res.BaseWarp, res.DetailWarp};

  for (int i = 0; i < layerName.Length; i++) {
    bool procedural = ((BoolParameter) sky.GetType().GetField(layerName[i] + "NoiseProcedural" + layerIndex).GetValue(sky)).value;
    if (procedural) {
      /* Gather and set parameters. */
      ExpanseCommon.CloudNoiseType noiseType = ((EnumParameter<ExpanseCommon.CloudNoiseType>) sky.GetType().GetField(layerName[i] + "NoiseType" + layerIndex).GetValue(sky)).value;
      string kernelName = ExpanseCommon.cloudNoiseTypeToKernelName[noiseType] + ((i == 0) ? "2D" : "3D"); // coverage is always 2D
      Vector2 gridScale = ((Vector2Parameter) sky.GetType().GetField(layerName[i] + "GridScale" + layerIndex).GetValue(sky)).value;
      int octaves = ((ClampedIntParameter) sky.GetType().GetField(layerName[i] + "Octaves" + layerIndex).GetValue(sky)).value;
      float octaveScale = ((MinFloatParameter) sky.GetType().GetField(layerName[i] + "OctaveScale" + layerIndex).GetValue(sky)).value;
      float octaveMultiplier = ((MinFloatParameter) sky.GetType().GetField(layerName[i] + "OctaveMultiplier" + layerIndex).GetValue(sky)).value;

      /* HACK: workaround overwriting data. */
      ComputeShader cs = (ComputeShader) UnityEngine.Object.Instantiate(m_cloudCS);
      int handle = cs.FindKernel(kernelName);

      /* Set all the parameters. */
      int yRes = ExpanseCommon.cloudXZResolutionToYResolution(resolution[i]);
      if (i > 0) {
        cs.SetTexture(handle, "Noise_3D", noiseTexture[i]);
        cs.SetVector("_resNoise", new Vector4(resolution[i], yRes, resolution[i], 0));
        cs.SetVector("_gridScale", new Vector4(gridScale.x, gridScale.x/10, gridScale.y, 1)); // TODO HACK: y grid scale ratio?
      } else {
        /* Coverage texture is always 2d. */
        cs.SetTexture(handle, "Noise_2D", noiseTexture[i]);
        cs.SetVector("_resNoise", new Vector4(resolution[i], resolution[i], 0, 0));
        cs.SetVector("_gridScale", new Vector4(gridScale.x, gridScale.y, 1, 1));
      }
      cs.SetFloat("_octaveScale", octaveScale);
      cs.SetFloat("_octaveMultiplier", octaveMultiplier);
      cs.SetInt("_octaves", octaves);

      /* Dispatch! */
      if (i > 0) {
        cmd.DispatchCompute(cs, handle,
          (int) resolution[i] / 8,
          (int) yRes / 8, (int) resolution[i] / 8);
      } else {
        cmd.DispatchCompute(cs, handle,
          (int) resolution[i] / 8,
          (int) resolution[i] / 8, 1);
      }
    }
  }
}
/******************************************************************************/
/************************ END COMPUTE SHADER FUNCTIONS ************************/
/******************************************************************************/








/******************************************************************************/
/****************************** RW TEXTURE SETTERS ****************************/
/******************************************************************************/

private void setSkyPrecomputationTables() {
  int handle_T = m_skyCS.FindKernel("T");
  int handle_MS = m_skyCS.FindKernel("MS");
  if (m_numAtmosphereLayersEnabled > 0) {
    m_skyCS.SetTexture(handle_T, "_T_RW", m_skyT);
    m_skyCS.SetTexture(handle_MS, "_MS_RW", m_skyMS);
  }
}

private void setSkyRealtimeTables() {
  int handle_SS = m_skyCS.FindKernel("SS");
  int handle_MSAcc = m_skyCS.FindKernel("MSAcc");
  int handle_AP = m_skyCS.FindKernel("AP");
  if (m_numAtmosphereLayersEnabled > 0) {
    m_skyCS.SetTexture(handle_SS, "_SS_RW", m_skySS);
    m_skyCS.SetTexture(handle_MSAcc, "_MSAcc_RW", m_skyMSAcc);
    m_skyCS.SetTexture(handle_AP, "_AP_RW", m_skyAP);
  }
}

private void setStarRWTextures() {
  int handle_Star = m_starCS.FindKernel("STAR");
  m_starCS.SetTexture(handle_Star, "_Star_RW", m_proceduralStarTexture);
}

private void setNebulaeRWTextures() {
  int handle_Nebulae = m_starCS.FindKernel("NEBULAE");
  m_starCS.SetTexture(handle_Nebulae, "_Nebulae_RW", m_proceduralNebulaeTexture);
}

/******************************************************************************/
/**************************** END RW TEXTURE SETTERS **************************/
/******************************************************************************/








/******************************************************************************/
/*************************** GLOBAL C BUFFER SETTERS **************************/
/******************************************************************************/

private void setGlobalCBuffer(BuiltinSkyParameters builtinParams) {
  /* Get sky object. */
  var sky = builtinParams.skySettings as Expanse;

  /* Precomputed Tables. */
  setGlobalCBufferAtmosphereTables(builtinParams.commandBuffer, sky);

  /* Planet. */
  setGlobalCBufferPlanet(builtinParams.commandBuffer, sky);

  /* Atmosphere. */
  setGlobalCBufferAtmosphereLayers(builtinParams.commandBuffer, sky, builtinParams.worldSpaceCameraPos);

  /* Celestial bodies. */
  setGlobalCBufferCelestialBodies(builtinParams.commandBuffer, sky);

  /* Night Sky. */
  setGlobalCBufferNightSky(builtinParams.commandBuffer, sky);

  /* Aerial Perspective. */
  setGlobalCBufferAerialPerspective(builtinParams.commandBuffer, sky);

  /* Quality. */
  setGlobalCBufferQuality(builtinParams.commandBuffer, sky);

  /* Clouds. */
  setGlobalCBufferClouds(builtinParams.commandBuffer, sky);

  /* Camera params. */
  builtinParams.commandBuffer.SetGlobalVector(_WorldSpaceCameraPos1ID, builtinParams.worldSpaceCameraPos);
  builtinParams.commandBuffer.SetGlobalMatrix(_PixelCoordToViewDirWS, builtinParams.pixelCoordToViewDirMatrix);
  builtinParams.commandBuffer.SetGlobalMatrix("_pCoordToViewDir", builtinParams.pixelCoordToViewDirMatrix);
  builtinParams.commandBuffer.SetGlobalVector("_currentScreenSize", builtinParams.hdCamera.screenSize);
  builtinParams.commandBuffer.SetGlobalFloat("_farClip", builtinParams.hdCamera.frustum.planes[5].distance);

  /* Time tick variable. */
  builtinParams.commandBuffer.SetGlobalFloat("_tick", Time.realtimeSinceStartup);
}

private void setGlobalCBufferPlanet(CommandBuffer cmd, Expanse sky) {
  cmd.SetGlobalFloat("_atmosphereRadius", sky.planetRadius.value + sky.atmosphereThickness.value);
  cmd.SetGlobalFloat("_planetRadius", sky.planetRadius.value);
  cmd.SetGlobalVector("_planetOriginOffset", sky.planetOriginOffset.value);
  cmd.SetGlobalVector("_groundTint", sky.groundTint.value);
  cmd.SetGlobalFloat("_groundEmissionMultiplier", sky.groundEmissionMultiplier.value);

  Vector3 planetRotation = sky.planetRotation.value;
  Quaternion planetRotationMatrix = Quaternion.Euler(planetRotation.x,
                                                planetRotation.y,
                                                planetRotation.z);
  cmd.SetGlobalMatrix("_planetRotation", Matrix4x4.Rotate(planetRotationMatrix));

  Texture albedoTexture = sky.groundAlbedoTexture.value;
  cmd.SetGlobalFloat("_groundAlbedoTextureEnabled", (albedoTexture == null) ? 0 : 1);
  if (albedoTexture != null) {
    cmd.SetGlobalTexture("_groundAlbedoTexture", albedoTexture);
  }

  Texture emissionTexture = sky.groundEmissionTexture.value;
  cmd.SetGlobalFloat("_groundEmissionTextureEnabled", (emissionTexture == null) ? 0 : 1);
  if (emissionTexture != null) {
    cmd.SetGlobalTexture("_groundEmissionTexture", emissionTexture);
  }
}

private void setGlobalCBufferAtmosphereLayers(CommandBuffer cmd, Expanse sky, Vector3 cameraPos) {

  int n = (int) ExpanseCommon.kMaxAtmosphereLayers;

  Vector4[] layerCoefficientsA = new Vector4[n];
  Vector4[] layerCoefficientsS = new Vector4[n];
  float[] layerDensityDistribution = new float[n]; /* Should be int, but unity can only set float arrays. */
  float[] layerHeight = new float[n];
  float[] layerThickness = new float[n];
  float[] layerPhaseFunction = new float[n]; /* Should be int, but unity can only set float arrays. */
  float[] layerAnisotropy = new float[n];
  float[] layerDensity = new float[n];
  Vector4[] layerDensityAttenuationOrigin = new Vector4[n];
  float[] layerAttenuationDistance = new float[n];
  float[] layerAttenuationBias = new float[n];
  Vector4[] layerTint = new Vector4[n];
  float[] layerMultipleScatteringMultiplier = new float[n];

  int numActiveLayers = 0;
  for (int i = 0; i < n; i++) {
    bool enabled = (((BoolParameter) sky.GetType().GetField("layerEnabled" + i).GetValue(sky)).value);
    if (enabled) {
      /* Only set up properties if this layer is enabled. */
      Vector3 coefficientsA = ((Vector3Parameter) sky.GetType().GetField("layerCoefficientsA" + i).GetValue(sky)).value;
      layerCoefficientsA[numActiveLayers] = new Vector4(coefficientsA.x, coefficientsA.y, coefficientsA.z, 1);


      Vector3 coefficientsS = ((Vector3Parameter) sky.GetType().GetField("layerCoefficientsS" + i).GetValue(sky)).value;
      layerCoefficientsS[numActiveLayers] = new Vector4(coefficientsS.x, coefficientsS.y, coefficientsS.z, 1);

      layerDensityDistribution[numActiveLayers] = (float) ((EnumParameter<ExpanseCommon.DensityDistribution>) sky.GetType().GetField("layerDensityDistribution" + i).GetValue(sky)).value;
      layerHeight[numActiveLayers] = ((MinFloatParameter) sky.GetType().GetField("layerHeight" + i).GetValue(sky)).value;
      layerThickness[numActiveLayers] = ((MinFloatParameter) sky.GetType().GetField("layerThickness" + i).GetValue(sky)).value;
      layerPhaseFunction[numActiveLayers] = (float) ((EnumParameter<ExpanseCommon.PhaseFunction>) sky.GetType().GetField("layerPhaseFunction" + i).GetValue(sky)).value;
      layerAnisotropy[numActiveLayers] = ((ClampedFloatParameter) sky.GetType().GetField("layerAnisotropy" + i).GetValue(sky)).value;
      layerDensity[numActiveLayers] = ((MinFloatParameter) sky.GetType().GetField("layerDensity" + i).GetValue(sky)).value;
      if (((BoolParameter) sky.GetType().GetField("layerDensityAttenuationPlayerOrigin" + i).GetValue(sky)).value) {
        layerDensityAttenuationOrigin[numActiveLayers] = cameraPos;
      } else {
        layerDensityAttenuationOrigin[numActiveLayers] = ((Vector3Parameter) sky.GetType().GetField("layerDensityAttenuationOrigin" + i).GetValue(sky)).value;
      }
      // Convert to planet space.
      layerDensityAttenuationOrigin[numActiveLayers] -= -(new Vector4(0, sky.planetRadius.value, 0, 0)) +(new Vector4(sky.planetOriginOffset.value.x, sky.planetOriginOffset.value.y, sky.planetOriginOffset.value.z, 0));
      layerAttenuationDistance[numActiveLayers] = ((MinFloatParameter) sky.GetType().GetField("layerAttenuationDistance" + i).GetValue(sky)).value;
      layerAttenuationBias[numActiveLayers] = ((MinFloatParameter) sky.GetType().GetField("layerAttenuationBias" + i).GetValue(sky)).value;
      layerTint[numActiveLayers] = ((ColorParameter) sky.GetType().GetField("layerTint" + i).GetValue(sky)).value;
      layerMultipleScatteringMultiplier[numActiveLayers] = ((MinFloatParameter) sky.GetType().GetField("layerMultipleScatteringMultiplier" + i).GetValue(sky)).value;

      numActiveLayers++;
    }
  }

  cmd.SetGlobalInt("_numActiveLayers", numActiveLayers);
  cmd.SetGlobalVectorArray("_layerCoefficientsA", layerCoefficientsA);
  cmd.SetGlobalVectorArray("_layerCoefficientsS", layerCoefficientsS);
  cmd.SetGlobalFloatArray("_layerDensityDistribution", layerDensityDistribution);
  cmd.SetGlobalFloatArray("_layerHeight", layerHeight);
  cmd.SetGlobalFloatArray("_layerThickness", layerThickness);
  cmd.SetGlobalFloatArray("_layerPhaseFunction", layerPhaseFunction);
  cmd.SetGlobalFloatArray("_layerAnisotropy", layerAnisotropy);
  cmd.SetGlobalFloatArray("_layerDensity", layerDensity);
  cmd.SetGlobalVectorArray("_layerDensityAttenuationOrigin", layerDensityAttenuationOrigin);
  cmd.SetGlobalFloatArray("_layerAttenuationDistance", layerAttenuationDistance);
  cmd.SetGlobalFloatArray("_layerAttenuationBias", layerAttenuationBias);
  cmd.SetGlobalVectorArray("_layerTint", layerTint);
  cmd.SetGlobalFloatArray("_layerMultipleScatteringMultiplier", layerMultipleScatteringMultiplier);
}


private void setGlobalCBufferCelestialBodies(CommandBuffer cmd, Expanse sky) {

    int n = (int) ExpanseCommon.kMaxCelestialBodies;

    /* Set up arrays to pass to shader. */
    Vector4[] bodyLightColor = new Vector4[n];
    float[] bodyAngularRadius = new float[n];

    int numActiveBodies = 0;
    for (int i = 0; i < ExpanseCommon.kMaxCelestialBodies; i++) {
      bool enabled = (((BoolParameter) sky.GetType().GetField("bodyEnabled" + i).GetValue(sky)).value);
      if (enabled) {
        bodyAngularRadius[numActiveBodies] = Mathf.PI * (((ClampedFloatParameter) sky.GetType().GetField("bodyAngularRadius" + i).GetValue(sky)).value / 180);

        /* Only set up remaining properties if this body is enabled. */
        bool usesDateTime = (((BoolParameter) sky.GetType().GetField("bodyUseDateTime" + i).GetValue(sky)).value);
        if (usesDateTime) {
          /* Get the date time. */
          DateTime dateTime = ((ExpanseCommonNamespace.DateTimeParameter) sky.GetType().GetField("bodyDateTime" + i).GetValue(sky)).getDateTime();
          /* Convert date time to azimuth and zenith. */
          double azimuthAngle, zenithAngle;
          Vector2 latLong = ((Vector2Parameter) sky.GetType().GetField("bodyPlayerLatitudeLongitude" + i).GetValue(sky)).value;
          ExpanseDateTimeControl.CalculateSunPosition(dateTime, latLong.x, latLong.y, out azimuthAngle, out zenithAngle);
          float x = Mathf.Sin((float) zenithAngle) * Mathf.Cos((float) azimuthAngle);
          float y = Mathf.Sin((float) zenithAngle) * Mathf.Sin((float) azimuthAngle);
          float z = Mathf.Cos((float) zenithAngle);
          Vector3 direction = new Vector3(x, y, z);
          m_bodyDirections[numActiveBodies] = new Vector4(direction.x, direction.y, direction.z, 0);
        } else {
          Vector3 angles = ((Vector3Parameter) sky.GetType().GetField("bodyDirection" + i).GetValue(sky)).value;
          Quaternion bodyLightRotation = Quaternion.Euler(angles.x, angles.y, angles.z);
          Vector3 direction = bodyLightRotation * (new Vector3(0, 0, -1));
          m_bodyDirections[numActiveBodies] = new Vector4(direction.x, direction.y, direction.z, 0);
        }

        bool useTemperature = ((BoolParameter) sky.GetType().GetField("bodyUseTemperature" + i).GetValue(sky)).value;
        float lightIntensity = ((MinFloatParameter) sky.GetType().GetField("bodyLightIntensity" + i).GetValue(sky)).value;
        Vector4 lightColor = ((ColorParameter) sky.GetType().GetField("bodyLightColor" + i).GetValue(sky)).value;
        if (useTemperature) {
          float temperature = ((ClampedFloatParameter) sky.GetType().GetField("bodyLightTemperature" + i).GetValue(sky)).value;
          Vector4 temperatureColor = ExpanseCommon.blackbodyTempToColor(temperature);
          bodyLightColor[numActiveBodies] = lightIntensity * (new Vector4(temperatureColor.x * lightColor.x,
            temperatureColor.y * lightColor.y,
            temperatureColor.z * lightColor.z,
            temperatureColor.w * lightColor.w));
        } else {
          bodyLightColor[numActiveBodies] = lightColor * lightIntensity;
        }

        numActiveBodies++;
      }
    }

    // For our own internal bookkeeping, used for things like updating the
    // directional light transmittances.
    m_numBodiesEnabled = numActiveBodies;

    cmd.SetGlobalInt("_numActiveBodies", numActiveBodies);
    cmd.SetGlobalVectorArray("_bodyDirection", m_bodyDirections);
    cmd.SetGlobalVectorArray("_bodyLightColor", bodyLightColor);
    cmd.SetGlobalFloatArray("_bodyAngularRadius", bodyAngularRadius);
}

private void setGlobalCBufferAtmosphereTables(CommandBuffer cmd, Expanse sky) {
  if (m_numAtmosphereLayersEnabled > 0) {
    cmd.SetGlobalTexture("_T", m_skyT);
    cmd.SetGlobalVector("_resT", m_skyTextureResolution.T);
    cmd.SetGlobalTexture("_MS", m_skyMS);
    cmd.SetGlobalVector("_resMS", m_skyTextureResolution.MS);
    cmd.SetGlobalTexture("_SS", m_skySS);
    cmd.SetGlobalVector("_resSS", m_skyTextureResolution.SS);
    cmd.SetGlobalTexture("_AP", m_skyAP);
    cmd.SetGlobalVector("_resAP", m_skyTextureResolution.AP);
    cmd.SetGlobalTexture("_MSAcc", m_skyMSAcc);
    cmd.SetGlobalVector("_resMSAcc", m_skyTextureResolution.MSAccumulation);
  }
}

private void setGlobalCBufferNightSky(CommandBuffer cmd, Expanse sky) {
  cmd.SetGlobalVector("_averageNightSkyColor", m_averageNightSkyColor);
  cmd.SetGlobalVector("_nightSkyScatterTint", sky.nightSkyTint.value
    * sky.nightSkyScatterTint.value * sky.nightSkyScatterIntensity.value
    * sky.nightSkyIntensity.value);
  cmd.SetGlobalVector("_lightPollutionTint", sky.lightPollutionTint.value
    * sky.lightPollutionIntensity.value);

  if (sky.useProceduralNightSky.value) {
    cmd.SetGlobalVector("_resStar", m_starTextureResolution.Star);
    cmd.SetGlobalFloat("_useHighDensityMode", sky.useHighDensityMode.value ? 1 : 0);
    cmd.SetGlobalFloat("_starDensity", sky.starDensity.value);
    cmd.SetGlobalVector("_starDensitySeed", sky.starDensitySeed.value);
    cmd.SetGlobalFloat("_starSizeMin", sky.starSizeRange.value.x);
    cmd.SetGlobalFloat("_starSizeMax", sky.starSizeRange.value.y);
    cmd.SetGlobalFloat("_starSizeBias", sky.starSizeBias.value);
    cmd.SetGlobalVector("_starSizeSeed", sky.starSizeSeed.value);
    cmd.SetGlobalFloat("_starIntensityMin", sky.starIntensityRange.value.x);
    cmd.SetGlobalFloat("_starIntensityMax", sky.starIntensityRange.value.y);
    cmd.SetGlobalFloat("_starIntensityBias", sky.starIntensityBias.value);
    cmd.SetGlobalVector("_starIntensitySeed", sky.starIntensitySeed.value);
    cmd.SetGlobalFloat("_starTemperatureMin", sky.starTemperatureRange.value.x);
    cmd.SetGlobalFloat("_starTemperatureMax", sky.starTemperatureRange.value.y);
    cmd.SetGlobalFloat("_starTemperatureBias", sky.starTemperatureBias.value);
    cmd.SetGlobalVector("_starTemperatureSeed", sky.starTemperatureSeed.value);

    /* Nebulae. */
    cmd.SetGlobalFloat("_useProceduralNebulae", (sky.useProceduralNebulae.value) ? 1 : 0);
    cmd.SetGlobalFloat("_starNebulaFollowAmount", sky.starNebulaFollowAmount.value);
    cmd.SetGlobalFloat("_starNebulaFollowSpread", sky.starNebulaFollowSpread.value);

    cmd.SetGlobalFloat("_hasNebulaeTexture", (sky.nebulaeTexture.value == null) ? 0 : 1);
    // Set either way so that star precompute can be used. But be diligent
    // about only sampling it when _hasNebulaTexture is true.
    if (sky.nebulaeTexture.value) {
      cmd.SetGlobalTexture("_nebulaeTexture", sky.nebulaeTexture.value);
    } else {
      /* HACK: to avoid errors when no nebula texture is set, use
       * a super low res default nebula texture. */
      cmd.SetGlobalTexture("_nebulaeTexture", m_defaultNebulaeTexture);
    }
    /* Set the procedural nebulae texture for use in the star generation. */
    cmd.SetGlobalTexture("_proceduralNebulae", m_proceduralNebulaeTexture);
    cmd.SetGlobalFloat("_nebulaOverallIntensity", sky.nebulaOverallIntensity.value);

    if (sky.useProceduralNebulae.value) {
      cmd.SetGlobalVector("_resNebulae", m_nebulaeTextureResolution.Star);
      cmd.SetGlobalFloat("_nebulaOverallDefinition", sky.nebulaOverallDefinition.value);
      cmd.SetGlobalFloat("_nebulaCoverageScale", sky.nebulaCoverageScale.value);

      cmd.SetGlobalVector("_nebulaHazeColor", sky.nebulaHazeBrightness.value * sky.nebulaHazeColor.value);
      cmd.SetGlobalFloat("_nebulaHazeScale", sky.nebulaHazeScale.value);
      cmd.SetGlobalFloat("_nebulaHazeScaleFactor", sky.nebulaHazeScaleFactor.value);
      cmd.SetGlobalFloat("_nebulaHazeDetailBalance", sky.nebulaHazeDetailBalance.value);
      cmd.SetGlobalFloat("_nebulaHazeOctaves", sky.nebulaHazeOctaves.value);
      cmd.SetGlobalFloat("_nebulaHazeBias", sky.nebulaHazeBias.value);
      cmd.SetGlobalFloat("_nebulaHazeSpread", sky.nebulaHazeSpread.value);
      cmd.SetGlobalFloat("_nebulaHazeCoverage", sky.nebulaHazeCoverage.value);
      cmd.SetGlobalFloat("_nebulaHazeStrength", sky.nebulaHazeStrength.value);

      cmd.SetGlobalVector("_nebulaCloudColor", sky.nebulaCloudBrightness.value * sky.nebulaCloudColor.value);
      cmd.SetGlobalFloat("_nebulaCloudScale", sky.nebulaCloudScale.value);
      cmd.SetGlobalFloat("_nebulaCloudScaleFactor", sky.nebulaCloudScaleFactor.value);
      cmd.SetGlobalFloat("_nebulaCloudDetailBalance", sky.nebulaCloudDetailBalance.value);
      cmd.SetGlobalFloat("_nebulaCloudOctaves", sky.nebulaCloudOctaves.value);
      cmd.SetGlobalFloat("_nebulaCloudBias", sky.nebulaCloudBias.value);
      cmd.SetGlobalFloat("_nebulaCloudSpread", sky.nebulaCloudSpread.value);
      cmd.SetGlobalFloat("_nebulaCloudCoverage", sky.nebulaCloudCoverage.value);
      cmd.SetGlobalFloat("_nebulaCloudStrength", sky.nebulaCloudStrength.value);

      cmd.SetGlobalVector("_nebulaCoarseStrandColor", sky.nebulaCoarseStrandBrightness.value * sky.nebulaCoarseStrandColor.value);
      cmd.SetGlobalFloat("_nebulaCoarseStrandScale", sky.nebulaCoarseStrandScale.value);
      cmd.SetGlobalFloat("_nebulaCoarseStrandScaleFactor", sky.nebulaCoarseStrandScaleFactor.value);
      cmd.SetGlobalFloat("_nebulaCoarseStrandDetailBalance", sky.nebulaCoarseStrandDetailBalance.value);
      cmd.SetGlobalFloat("_nebulaCoarseStrandOctaves", sky.nebulaCoarseStrandOctaves.value);
      cmd.SetGlobalFloat("_nebulaCoarseStrandBias", sky.nebulaCoarseStrandBias.value);
      cmd.SetGlobalFloat("_nebulaCoarseStrandDefinition", sky.nebulaCoarseStrandDefinition.value);
      cmd.SetGlobalFloat("_nebulaCoarseStrandSpread", sky.nebulaCoarseStrandSpread.value);
      cmd.SetGlobalFloat("_nebulaCoarseStrandCoverage", sky.nebulaCoarseStrandCoverage.value);
      cmd.SetGlobalFloat("_nebulaCoarseStrandStrength", sky.nebulaCoarseStrandStrength.value);
      cmd.SetGlobalFloat("_nebulaCoarseStrandWarpScale", sky.nebulaCoarseStrandWarpScale.value);
      cmd.SetGlobalFloat("_nebulaCoarseStrandWarp", sky.nebulaCoarseStrandWarp.value);

      cmd.SetGlobalVector("_nebulaFineStrandColor", sky.nebulaFineStrandBrightness.value * sky.nebulaFineStrandColor.value);
      cmd.SetGlobalFloat("_nebulaFineStrandScale", sky.nebulaFineStrandScale.value);
      cmd.SetGlobalFloat("_nebulaFineStrandScaleFactor", sky.nebulaFineStrandScaleFactor.value);
      cmd.SetGlobalFloat("_nebulaFineStrandDetailBalance", sky.nebulaFineStrandDetailBalance.value);
      cmd.SetGlobalFloat("_nebulaFineStrandOctaves", sky.nebulaFineStrandOctaves.value);
      cmd.SetGlobalFloat("_nebulaFineStrandBias", sky.nebulaFineStrandBias.value);
      cmd.SetGlobalFloat("_nebulaFineStrandDefinition", sky.nebulaFineStrandDefinition.value);
      cmd.SetGlobalFloat("_nebulaFineStrandSpread", sky.nebulaFineStrandSpread.value);
      cmd.SetGlobalFloat("_nebulaFineStrandCoverage", sky.nebulaFineStrandCoverage.value);
      cmd.SetGlobalFloat("_nebulaFineStrandStrength", sky.nebulaFineStrandStrength.value);
      cmd.SetGlobalFloat("_nebulaFineStrandWarpScale", sky.nebulaFineStrandWarpScale.value);
      cmd.SetGlobalFloat("_nebulaFineStrandWarp", sky.nebulaFineStrandWarp.value);

      cmd.SetGlobalFloat("_nebulaTransmittanceMin", sky.nebulaTransmittanceRange.value.x);
      cmd.SetGlobalFloat("_nebulaTransmittanceMax", sky.nebulaTransmittanceRange.value.y);
      cmd.SetGlobalFloat("_nebulaTransmittanceScale", sky.nebulaTransmittanceScale.value);

      /* Seeds. */
      cmd.SetGlobalVector("_nebulaCoverageSeed", sky.nebulaCoverageSeed.value);
      cmd.SetGlobalVector("_nebulaHazeSeedX", sky.nebulaHazeSeedX.value);
      cmd.SetGlobalVector("_nebulaHazeSeedY", sky.nebulaHazeSeedY.value);
      cmd.SetGlobalVector("_nebulaHazeSeedZ", sky.nebulaHazeSeedZ.value);
      cmd.SetGlobalVector("_nebulaCloudSeedX", sky.nebulaCloudSeedX.value);
      cmd.SetGlobalVector("_nebulaCloudSeedY", sky.nebulaCloudSeedY.value);
      cmd.SetGlobalVector("_nebulaCloudSeedZ", sky.nebulaCloudSeedZ.value);
      cmd.SetGlobalVector("_nebulaCoarseStrandSeedX", sky.nebulaCoarseStrandSeedX.value);
      cmd.SetGlobalVector("_nebulaCoarseStrandSeedY", sky.nebulaCoarseStrandSeedY.value);
      cmd.SetGlobalVector("_nebulaCoarseStrandSeedZ", sky.nebulaCoarseStrandSeedZ.value);
      cmd.SetGlobalVector("_nebulaCoarseStrandWarpSeedX", sky.nebulaCoarseStrandWarpSeedX.value);
      cmd.SetGlobalVector("_nebulaCoarseStrandWarpSeedY", sky.nebulaCoarseStrandWarpSeedY.value);
      cmd.SetGlobalVector("_nebulaCoarseStrandWarpSeedZ", sky.nebulaCoarseStrandWarpSeedZ.value);
      cmd.SetGlobalVector("_nebulaFineStrandSeedX", sky.nebulaFineStrandSeedX.value);
      cmd.SetGlobalVector("_nebulaFineStrandSeedY", sky.nebulaFineStrandSeedY.value);
      cmd.SetGlobalVector("_nebulaFineStrandSeedZ", sky.nebulaFineStrandSeedZ.value);
      cmd.SetGlobalVector("_nebulaFineStrandWarpSeedX", sky.nebulaFineStrandWarpSeedX.value);
      cmd.SetGlobalVector("_nebulaFineStrandWarpSeedY", sky.nebulaFineStrandWarpSeedY.value);
      cmd.SetGlobalVector("_nebulaFineStrandWarpSeedZ", sky.nebulaFineStrandWarpSeedZ.value);
      cmd.SetGlobalVector("_nebulaTransmittanceSeedX", sky.nebulaTransmittanceSeedX.value);
      cmd.SetGlobalVector("_nebulaTransmittanceSeedY", sky.nebulaTransmittanceSeedY.value);
      cmd.SetGlobalVector("_nebulaTransmittanceSeedZ", sky.nebulaTransmittanceSeedZ.value);
    }
  }
}

private void setGlobalCBufferAerialPerspective(CommandBuffer cmd, Expanse sky) {
  cmd.SetGlobalFloat("_aerialPerspectiveOcclusionBiasUniform", sky.aerialPerspectiveOcclusionBiasUniform.value);
  cmd.SetGlobalFloat("_aerialPerspectiveOcclusionPowerUniform", sky.aerialPerspectiveOcclusionPowerUniform.value);
  cmd.SetGlobalFloat("_aerialPerspectiveOcclusionBiasDirectional", sky.aerialPerspectiveOcclusionBiasDirectional.value);
  cmd.SetGlobalFloat("_aerialPerspectiveOcclusionPowerDirectional", sky.aerialPerspectiveOcclusionPowerDirectional.value);
  cmd.SetGlobalFloat("_aerialPerspectiveNightScatteringMultiplier", sky.aerialPerspectiveNightScatteringMultiplier.value);
}

private void setGlobalCBufferQuality(CommandBuffer cmd, Expanse sky) {
  cmd.SetGlobalInt("_numTSamples", sky.numberOfTransmittanceSamples.value);
  cmd.SetGlobalInt("_numSSSamples", sky.numberOfSingleScatteringSamples.value);
  cmd.SetGlobalInt("_numMSSamples", sky.numberOfMultipleScatteringSamples.value);
  cmd.SetGlobalInt("_numMSAccumulationSamples", sky.numberOfMultipleScatteringAccumulationSamples.value);
  cmd.SetGlobalInt("_numAPSamples", sky.numberOfAerialPerspectiveSamples.value);
  cmd.SetGlobalFloat("_useImportanceSampling", sky.useImportanceSampling.value ? 1 : 0);
  cmd.SetGlobalFloat("_aerialPerspectiveUseImportanceSampling", sky.aerialPerspectiveUseImportanceSampling.value ? 1 : 0);
  cmd.SetGlobalFloat("_useAntiAliasing", sky.useAntiAliasing.value ? 1 : 0);
  cmd.SetGlobalFloat("_aerialPerspectiveDepthSkew", sky.aerialPerspectiveDepthSkew.value);
  cmd.SetGlobalFloat("_useDither", sky.useDither.value ? 1 : 0);
}

private void setGlobalCBufferClouds(CommandBuffer cmd, Expanse sky) {
  setGlobalCBufferCloudsGeometry(cmd, sky);
  setGlobalCBufferCloudsLighting(cmd, sky);
}

private void setGlobalCBufferCloudsGeometry(CommandBuffer cmd, Expanse sky) {

  float[] cloudGeometryType = new float[(int) Mathf.Max(1, m_enabledCloudLayers.Length)]; /* Should be int, but unity can only set float arrays. */
  float[] cloudGeometryXMin = new float[(int) Mathf.Max(1, m_enabledCloudLayers.Length)];
  float[] cloudGeometryXMax = new float[(int) Mathf.Max(1, m_enabledCloudLayers.Length)];
  float[] cloudGeometryYMin = new float[(int) Mathf.Max(1, m_enabledCloudLayers.Length)];
  float[] cloudGeometryYMax = new float[(int) Mathf.Max(1, m_enabledCloudLayers.Length)];
  float[] cloudGeometryZMin = new float[(int) Mathf.Max(1, m_enabledCloudLayers.Length)];
  float[] cloudGeometryZMax = new float[(int) Mathf.Max(1, m_enabledCloudLayers.Length)];
  float[] cloudGeometryHeight = new float[(int) Mathf.Max(1, m_enabledCloudLayers.Length)];

  for (int i = 0; i < m_enabledCloudLayers.Length; i++) {
    int layerIndex = m_enabledCloudLayers[i];
    cloudGeometryType[i] = (float) ((EnumParameter<ExpanseCommon.CloudGeometryType>) sky.GetType().GetField("cloudGeometryType" + layerIndex).GetValue(sky)).value;

    Vector2 xExtent = ((Vector2Parameter) sky.GetType().GetField("cloudGeometryXExtent" + layerIndex).GetValue(sky)).value;
    cloudGeometryXMin[i] = xExtent.x;
    cloudGeometryXMax[i] = xExtent.y;

    Vector2 yExtent = ((Vector2Parameter) sky.GetType().GetField("cloudGeometryYExtent" + layerIndex).GetValue(sky)).value;
    cloudGeometryYMin[i] = yExtent.x;
    cloudGeometryYMax[i] = yExtent.y;

    Vector2 zExtent = ((Vector2Parameter) sky.GetType().GetField("cloudGeometryZExtent" + layerIndex).GetValue(sky)).value;
    cloudGeometryZMin[i] = zExtent.x;
    cloudGeometryZMax[i] = zExtent.y;

    cloudGeometryHeight[i] = ((FloatParameter) sky.GetType().GetField("cloudGeometryHeight" + layerIndex).GetValue(sky)).value;
  }

  cmd.SetGlobalInt("_numActiveCloudLayers", m_enabledCloudLayers.Length);
  cmd.SetGlobalFloatArray("_cloudGeometryType", cloudGeometryType);
  cmd.SetGlobalFloatArray("_cloudGeometryXMin", cloudGeometryXMin);
  cmd.SetGlobalFloatArray("_cloudGeometryXMax", cloudGeometryXMax);
  cmd.SetGlobalFloatArray("_cloudGeometryYMin", cloudGeometryYMin);
  cmd.SetGlobalFloatArray("_cloudGeometryYMax", cloudGeometryYMax);
  cmd.SetGlobalFloatArray("_cloudGeometryZMin", cloudGeometryZMin);
  cmd.SetGlobalFloatArray("_cloudGeometryZMax", cloudGeometryZMax);
  cmd.SetGlobalFloatArray("_cloudGeometryHeight", cloudGeometryHeight);
}

private void setGlobalCBufferCloudsLighting(CommandBuffer cmd, Expanse sky) {

  float[] cloudThickness = new float[(int) Mathf.Max(1, m_enabledCloudLayers.Length)];
  float[] cloudDensity = new float[(int) Mathf.Max(1, m_enabledCloudLayers.Length)];
  float[] cloudDensityAttenuationDistance = new float[(int) Mathf.Max(1, m_enabledCloudLayers.Length)];
  float[] cloudDensityAttenuationBias = new float[(int) Mathf.Max(1, m_enabledCloudLayers.Length)];
  Vector4[] cloudAbsorptionCoefficients = new Vector4[(int) Mathf.Max(1, m_enabledCloudLayers.Length)];
  Vector4[] cloudScatteringCoefficients = new Vector4[(int) Mathf.Max(1, m_enabledCloudLayers.Length)];

  for (int i = 0; i < m_enabledCloudLayers.Length; i++) {
    int layerIndex = m_enabledCloudLayers[i];
    cloudThickness[i] = ((MinFloatParameter) sky.GetType().GetField("cloudThickness" + layerIndex).GetValue(sky)).value;
    cloudDensity[i] = ((MinFloatParameter) sky.GetType().GetField("cloudDensity" + layerIndex).GetValue(sky)).value;
    cloudDensityAttenuationDistance[i] = ((MinFloatParameter) sky.GetType().GetField("cloudDensityAttenuationDistance" + layerIndex).GetValue(sky)).value;
    cloudDensityAttenuationBias[i] = ((MinFloatParameter) sky.GetType().GetField("cloudDensityAttenuationBias" + layerIndex).GetValue(sky)).value;

    Vector3 aCoefficients = ((Vector3Parameter) sky.GetType().GetField("cloudAbsorptionCoefficients" + layerIndex).GetValue(sky)).value;
    cloudAbsorptionCoefficients[i] = new Vector4(aCoefficients.x, aCoefficients.y, aCoefficients.z, 0);
    Vector3 sCoefficients = ((Vector3Parameter) sky.GetType().GetField("cloudScatteringCoefficients" + layerIndex).GetValue(sky)).value;
    cloudScatteringCoefficients[i] = new Vector4(sCoefficients.x, sCoefficients.y, sCoefficients.z, 0);
  }

  cmd.SetGlobalFloatArray("_cloudThickness", cloudThickness);
  cmd.SetGlobalFloatArray("_cloudDensity", cloudDensity);
  cmd.SetGlobalFloatArray("_cloudDensityAttenuationDistance", cloudDensityAttenuationDistance);
  cmd.SetGlobalFloatArray("_cloudDensityAttenuationBias", cloudDensityAttenuationBias);
  cmd.SetGlobalVectorArray("_cloudAbsorptionCoefficients", cloudAbsorptionCoefficients);
  cmd.SetGlobalVectorArray("_cloudScatteringCoefficients", cloudScatteringCoefficients);
}

void SetGlobalCloudTextures(CommandBuffer cmd, Expanse sky, int layer, int layerIndex) {
  /* Set the layer we're shading as a uniform variable. */
  m_PropertyBlock.SetInt("_cloudLayerToDraw", layer);

  /* Set the noise textures we'll be using. */
  SetGlobalCloudTexturesCommon(cmd, sky, layer, layerIndex);

  if (m_cloudTextureResolutions[layer].dimension == ExpanseCommon.CloudNoiseDimension.TwoD) {
    SetGlobalCloudTextures2D(cmd, sky, layer, layerIndex);
  } else {
    SetGlobalCloudTextures3D(cmd, sky, layer, layerIndex);
  }
}

void SetGlobalCloudTexturesCommon(CommandBuffer cmd, Expanse sky, int layer, int layerIndex) {
  /* Set the texture sampling parameters. */
  cmd.SetGlobalInt("_cloudCoverageTile", ((MinIntParameter) sky.GetType().GetField("cloudCoverageTile" + layerIndex).GetValue(sky)).value);
  cmd.SetGlobalFloat("_cloudCoverageIntensity", ((ClampedFloatParameter) sky.GetType().GetField("cloudCoverageIntensity" + layerIndex).GetValue(sky)).value);
  cmd.SetGlobalInt("_cloudBaseTile", ((MinIntParameter) sky.GetType().GetField("cloudBaseTile" + layerIndex).GetValue(sky)).value);
  cmd.SetGlobalInt("_cloudStructureTile", ((MinIntParameter) sky.GetType().GetField("cloudStructureTile" + layerIndex).GetValue(sky)).value);
  cmd.SetGlobalFloat("_cloudStructureIntensity", ((ClampedFloatParameter) sky.GetType().GetField("cloudStructureIntensity" + layerIndex).GetValue(sky)).value);
  cmd.SetGlobalInt("_cloudDetailTile", ((MinIntParameter) sky.GetType().GetField("cloudDetailTile" + layerIndex).GetValue(sky)).value);
  cmd.SetGlobalFloat("_cloudDetailIntensity", ((ClampedFloatParameter) sky.GetType().GetField("cloudDetailIntensity" + layerIndex).GetValue(sky)).value);
  cmd.SetGlobalInt("_cloudBaseWarpTile", ((MinIntParameter) sky.GetType().GetField("cloudBaseWarpTile" + layerIndex).GetValue(sky)).value);
  cmd.SetGlobalFloat("_cloudBaseWarpIntensity", ((ClampedFloatParameter) sky.GetType().GetField("cloudBaseWarpIntensity" + layerIndex).GetValue(sky)).value);
  cmd.SetGlobalInt("_cloudDetailWarpTile", ((MinIntParameter) sky.GetType().GetField("cloudDetailWarpTile" + layerIndex).GetValue(sky)).value);
  cmd.SetGlobalFloat("_cloudDetailWarpIntensity", ((ClampedFloatParameter) sky.GetType().GetField("cloudDetailWarpIntensity" + layerIndex).GetValue(sky)).value);

  /* Set the lighting parameters. */
  cmd.SetGlobalFloat("_cloudMSAmount", ((ClampedFloatParameter) sky.GetType().GetField("cloudMSAmount" + layerIndex).GetValue(sky)).value);
  cmd.SetGlobalFloat("_cloudMSBias", ((ClampedFloatParameter) sky.GetType().GetField("cloudMSBias" + layerIndex).GetValue(sky)).value);
  cmd.SetGlobalFloat("_cloudSilverSpread", ((ClampedFloatParameter) sky.GetType().GetField("cloudSilverSpread" + layerIndex).GetValue(sky)).value);
  cmd.SetGlobalFloat("_cloudSilverIntensity", ((ClampedFloatParameter) sky.GetType().GetField("cloudSilverIntensity" + layerIndex).GetValue(sky)).value);
  cmd.SetGlobalFloat("_cloudAnisotropy", ((ClampedFloatParameter) sky.GetType().GetField("cloudAnisotropy" + layerIndex).GetValue(sky)).value);
}

void SetGlobalCloudTextures2D(CommandBuffer cmd, Expanse sky, int layer, int layerIndex) {
  CloudNoiseTexture proceduralTextures = m_cloudNoiseTextures[layer];
  /* This array pattern is to keep things concise. */
  string[] noiseProcedural = {"cloudCoverageNoiseProcedural", "cloudBaseNoiseProcedural",
    "cloudStructureNoiseProcedural", "cloudDetailNoiseProcedural",
    "cloudBaseWarpNoiseProcedural", "cloudDetailWarpNoiseProcedural"};
  string[] shaderVariable = {"_cloudCoverageNoise", "_cloudBaseNoise2D",
    "_cloudStructureNoise2D", "_cloudDetailNoise2D", "_cloudBaseWarpNoise2D",
    "_cloudDetailWarpNoise2D"};
  string[] imageTexture = {"cloudCoverageNoiseTexture", "cloudBaseNoiseTexture2D",
    "cloudStructureNoiseTexture2D", "cloudDetailNoiseTexture2D",
    "cloudBaseWarpNoiseTexture2D", "cloudDetailWarpNoiseTexture2D"};
  RTHandle[] proceduralTexture = {proceduralTextures.coverageTex, proceduralTextures.baseTex,
    proceduralTextures.structureTex, proceduralTextures.detailTex,
    proceduralTextures.baseWarpTex, proceduralTextures.detailWarpTex};

  for (int i = 0; i < noiseProcedural.Length; i++) {
    bool procedural = ((BoolParameter) sky.GetType().GetField(noiseProcedural[i] + layerIndex).GetValue(sky)).value;
    if (procedural) {
      cmd.SetGlobalTexture(shaderVariable[i], proceduralTexture[i]);
    } else {
      Texture tex = ((TextureParameter) sky.GetType().GetField(imageTexture[i] + layerIndex).GetValue(sky)).value;
      cmd.SetGlobalTexture(shaderVariable[i], tex);
    }
  }
}

void SetGlobalCloudTextures3D(CommandBuffer cmd, Expanse sky, int layer, int layerIndex) {
  CloudNoiseTexture proceduralTextures = m_cloudNoiseTextures[layer];
  /* This array pattern is to keep things concise. */
  string[] noiseProcedural = {"cloudCoverageNoiseProcedural", "cloudBaseNoiseProcedural",
    "cloudStructureNoiseProcedural", "cloudDetailNoiseProcedural",
    "cloudBaseWarpNoiseProcedural", "cloudDetailWarpNoiseProcedural"};
  string[] shaderVariable = {"_cloudCoverageNoise", "_cloudBaseNoise3D",
    "_cloudStructureNoise3D", "_cloudDetailNoise3D", "_cloudBaseWarpNoise3D",
    "_cloudDetailWarpNoise3D"};
  string[] imageTexture = {"cloudCoverageNoiseTexture", "cloudBaseNoiseTexture3D",
    "cloudStructureNoiseTexture3D", "cloudDetailNoiseTexture3D",
    "cloudBaseWarpNoiseTexture3D", "cloudDetailWarpNoiseTexture3D"};
  RTHandle[] proceduralTexture = {proceduralTextures.coverageTex, proceduralTextures.baseTex,
    proceduralTextures.structureTex, proceduralTextures.detailTex,
    proceduralTextures.baseWarpTex, proceduralTextures.detailWarpTex};

  for (int i = 0; i < noiseProcedural.Length; i++) {
    bool procedural = ((BoolParameter) sky.GetType().GetField(noiseProcedural[i] + layerIndex).GetValue(sky)).value;
    if (procedural) {
      cmd.SetGlobalTexture(shaderVariable[i], proceduralTexture[i]);
    } else {
      Texture tex = ((TextureParameter) sky.GetType().GetField(imageTexture[i] + layerIndex).GetValue(sky)).value;
      cmd.SetGlobalTexture(shaderVariable[i], tex);
    }
  }
}

/******************************************************************************/
/************************* END GLOBAL C BUFFER SETTERS ************************/
/******************************************************************************/








/******************************************************************************/
/************************** MATERIAL PROPERTY SETTERS *************************/
/******************************************************************************/

private void setMaterialPropertyBlock(BuiltinSkyParameters builtinParams) {
  /* Get sky object. */
  var sky = builtinParams.skySettings as Expanse;

  /* Celestial bodies. */
  setMaterialPropertyBlockCelestialBodies(sky);

  /* Night sky. */
  setMaterialPropertyBlockNightSky(sky);
}

private void setMaterialPropertyBlockCelestialBodies(Expanse sky) {

  int n = (int) ExpanseCommon.kMaxCelestialBodies;

  /* Set up arrays to pass to shader. */
  float[] bodyDistance = new float[n];
  float[] bodyReceivesLight = new float[n];
  Matrix4x4[] bodyAlbedoTextureRotation = new Matrix4x4[n];
  Vector4[] bodyAlbedoTint = new Vector4[n];
  float[] bodyEmissive = new float[n];
  float[] bodyLimbDarkening = new float[n];
  Matrix4x4[] bodyEmissionTextureRotation = new Matrix4x4[n];
  Vector4[] bodyEmissionTint = new Vector4[n];

  float[] bodyAlbedoTextureEnabled = new float[n];
  float[] bodyEmissionTextureEnabled = new float[n];

  int numActiveBodies = 0;
  for (int i = 0; i < ExpanseCommon.kMaxCelestialBodies; i++) {
    bool enabled = (((BoolParameter) sky.GetType().GetField("bodyEnabled" + i).GetValue(sky)).value);
    if (enabled) {
      /* Only set up remaining properties if this body is enabled. */

      bodyDistance[numActiveBodies] = ((MinFloatParameter) sky.GetType().GetField("bodyDistance" + i).GetValue(sky)).value;
      bodyReceivesLight[numActiveBodies] = (((BoolParameter) sky.GetType().GetField("bodyReceivesLight" + i).GetValue(sky)).value) ? 1 : 0;

      Vector3 albedoTexRotationV3 = ((Vector3Parameter) sky.GetType().GetField("bodyAlbedoTextureRotation" + i).GetValue(sky)).value;
      Quaternion albedoTexRotation = Quaternion.Euler(albedoTexRotationV3.x,
                                                    albedoTexRotationV3.y,
                                                    albedoTexRotationV3.z);
      bodyAlbedoTextureRotation[numActiveBodies] = Matrix4x4.Rotate(albedoTexRotation);

      bodyAlbedoTint[numActiveBodies] = ((ColorParameter) sky.GetType().GetField("bodyAlbedoTint" + i).GetValue(sky)).value;
      bodyEmissive[numActiveBodies] = (((BoolParameter) sky.GetType().GetField("bodyEmissive" + i).GetValue(sky)).value) ? 1 : 0;

      bodyLimbDarkening[numActiveBodies] = ((MinFloatParameter) sky.GetType().GetField("bodyLimbDarkening" + i).GetValue(sky)).value;


      Vector3 emissionTexRotationV3 = ((Vector3Parameter) sky.GetType().GetField("bodyEmissionTextureRotation" + i).GetValue(sky)).value;
      Quaternion emissionTexRotation = Quaternion.Euler(emissionTexRotationV3.x,
                                                    emissionTexRotationV3.y,
                                                    emissionTexRotationV3.z);
      bodyEmissionTextureRotation[numActiveBodies] = Matrix4x4.Rotate(emissionTexRotation);

      float emissionMultiplier = ((MinFloatParameter) sky.GetType().GetField("bodyEmissionMultiplier" + i).GetValue(sky)).value;
      bodyEmissionTint[numActiveBodies] = emissionMultiplier * ((ColorParameter) sky.GetType().GetField("bodyEmissionTint" + i).GetValue(sky)).value;

      /* Textures, which can't be set as arrays. */
      Texture albedoTexture = ((CubemapParameter) sky.GetType().GetField("bodyAlbedoTexture" + i).GetValue(sky)).value;
      bodyAlbedoTextureEnabled[numActiveBodies] = (albedoTexture == null) ? 0 : 1;
      if (albedoTexture != null) {
        m_PropertyBlock.SetTexture("_bodyAlbedoTexture" + numActiveBodies, albedoTexture);
      }

      Texture emissionTexture = ((CubemapParameter) sky.GetType().GetField("bodyEmissionTexture" + i).GetValue(sky)).value;
      bodyEmissionTextureEnabled[numActiveBodies] = (emissionTexture == null) ? 0 : 1;
      if (emissionTexture != null) {
        m_PropertyBlock.SetTexture("_bodyEmissionTexture" + numActiveBodies, emissionTexture);
      }

      numActiveBodies++;
    }
  }

  /* Actually set everything in the property block. */
  m_PropertyBlock.SetFloatArray("_bodyDistance", bodyDistance);
  m_PropertyBlock.SetFloatArray("_bodyReceivesLight", bodyReceivesLight);
  m_PropertyBlock.SetMatrixArray("_bodyAlbedoTextureRotation", bodyAlbedoTextureRotation);
  m_PropertyBlock.SetVectorArray("_bodyAlbedoTint", bodyAlbedoTint);
  m_PropertyBlock.SetFloatArray("_bodyEmissive", bodyEmissive);
  m_PropertyBlock.SetFloatArray("_bodyLimbDarkening", bodyLimbDarkening);
  m_PropertyBlock.SetMatrixArray("_bodyEmissionTextureRotation", bodyEmissionTextureRotation);
  m_PropertyBlock.SetVectorArray("_bodyEmissionTint", bodyEmissionTint);

  m_PropertyBlock.SetFloatArray("_bodyAlbedoTextureEnabled", bodyAlbedoTextureEnabled);
  m_PropertyBlock.SetFloatArray("_bodyEmissionTextureEnabled", bodyEmissionTextureEnabled);
}

private void setMaterialPropertyBlockNightSky(Expanse sky) {
  m_PropertyBlock.SetFloat("_useProceduralNightSky", (sky.useProceduralNightSky.value) ? 1 : 0);
  if (sky.useProceduralNightSky.value) {
    m_PropertyBlock.SetTexture("_Star", m_proceduralStarTexture);
    m_PropertyBlock.SetVector("_starTint", sky.starTint.value);
  } else {
    m_PropertyBlock.SetFloat("_hasNightSkyTexture", (sky.nightSkyTexture.value == null) ? 0 : 1);
    if (sky.nightSkyTexture.value != null) {
      m_PropertyBlock.SetTexture("_nightSkyTexture", sky.nightSkyTexture.value);
    }
  }

  Vector3 nightSkyRotation = sky.nightSkyRotation.value;
  Quaternion nightSkyRotationMatrix = Quaternion.Euler(nightSkyRotation.x,
                                                nightSkyRotation.y,
                                                nightSkyRotation.z);
  m_PropertyBlock.SetMatrix("_nightSkyRotation", Matrix4x4.Rotate(nightSkyRotationMatrix));
  m_PropertyBlock.SetVector("_nightSkyTint", sky.nightSkyTint.value
    * sky.nightSkyIntensity.value);
  m_PropertyBlock.SetFloat("_nightSkyAmbientMultiplier", sky.nightSkyAmbientMultiplier.value);

  m_PropertyBlock.SetFloat("_useTwinkle", (sky.useTwinkle.value) ? 1 : 0);
  m_PropertyBlock.SetFloat("_twinkleThreshold", sky.twinkleThreshold.value);
  m_PropertyBlock.SetFloat("_twinkleFrequencyMin", sky.twinkleFrequencyRange.value.x);
  m_PropertyBlock.SetFloat("_twinkleFrequencyMax", sky.twinkleFrequencyRange.value.y);
  m_PropertyBlock.SetFloat("_twinkleBias", sky.twinkleBias.value);
  m_PropertyBlock.SetFloat("_twinkleSmoothAmplitude", sky.twinkleSmoothAmplitude.value);
  m_PropertyBlock.SetFloat("_twinkleChaoticAmplitude", sky.twinkleChaoticAmplitude.value);
}

/******************************************************************************/
/************************ END MATERIAL PROPERTY SETTERS ***********************/
/******************************************************************************/

}
