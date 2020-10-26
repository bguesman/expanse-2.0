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

private RTHandle m_TTable;                            /* Transmittance. */
private Texture2D m_TTableCPU;
private bool m_TTableCPUNeedsUpdate;
private RTHandle m_SSTableArray;                      /* Single Scattering. */
private RTHandle m_SSNoShadowTableArray;              /* Single Scattering. */
private RTHandle m_SSAerialPerspectiveTableLOD0Array; /* AerialPerspective. */
private RTHandle m_SSAerialPerspectiveTableLOD1Array; /* AerialPerspective. */
private RTHandle m_MSTable;                           /* Multiple Scattering. */
private RTHandle m_MSAccumulationTableArray;          /* Multiple Scattering. */
private RTHandle m_LPTableArray;                      /* Light Pollution. */
private RTHandle m_GITableArray;                      /* Ground Irradiance. */
/* For checking if table reallocation is required. */
private ExpanseCommon.SkyTextureResolution m_skyTextureResolution;
private int m_numAtmosphereLayersEnabled = 0;

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

    m_TTable = allocateSky2DTable(res.T, 0, "SkyT");
    m_MSTable = allocateSky2DTable(res.MS, 0, "SkyMS");

    m_SSTableArray = allocateSky4DArrayTable(res.SS, numEnabled, "SkySS");
    m_SSNoShadowTableArray = allocateSky4DArrayTable(res.SS, numEnabled, "SkySSNoShadow");
    m_SSAerialPerspectiveTableLOD0Array = allocateSky4DArrayTable(res.SS, numEnabled, "SkySSAerialPerspectiveLOD0");
    m_SSAerialPerspectiveTableLOD1Array = allocateSky4DArrayTable(res.SS, numEnabled, "SkySSAerialPerspectiveLOD1");
    m_MSAccumulationTableArray = allocateSky4DArrayTable(res.MSAccumulation, numEnabled, "SkyMSAcc");
    m_GITableArray = allocateSky1DArrayTable(res.GI, numEnabled, "SkyGI");
    m_LPTableArray = allocateSky2DArrayTable(res.LP, numEnabled, "SkyLP");

    m_numAtmosphereLayersEnabled = numEnabled;
    m_skyTextureResolution = res;

    /* Resize CPU copy of transmittance table. */
    m_TTableCPU = new Texture2D((int) res.T.x, (int) res.T.y, TextureFormat.RGBAFloat, false);
  }
}

void cleanupSkyTables() {
  RTHandles.Release(m_TTable);
  m_TTable = null;
  RTHandles.Release(m_MSTable);
  m_MSTable = null;
  RTHandles.Release(m_GITableArray);
  m_GITableArray = null;
  RTHandles.Release(m_LPTableArray);
  m_LPTableArray = null;
  RTHandles.Release(m_SSTableArray);
  m_SSTableArray = null;
  RTHandles.Release(m_SSNoShadowTableArray);
  m_SSNoShadowTableArray = null;
  RTHandles.Release(m_SSAerialPerspectiveTableLOD0Array);
  m_SSAerialPerspectiveTableLOD0Array = null;
  RTHandles.Release(m_SSAerialPerspectiveTableLOD1Array);
  m_SSAerialPerspectiveTableLOD1Array = null;
  RTHandles.Release(m_MSAccumulationTableArray);
  m_MSAccumulationTableArray = null;
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

/* Allocates 2D sky precomputation table. */
RTHandle allocateSky2DArrayTable(Vector2 resolution, int depth, string name) {
  var table = RTHandles.Alloc((int) resolution.x,
                              (int) resolution.y,
                              depth,
                              dimension: TextureDimension.Tex2DArray,
                              colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                              enableRandomWrite: true,
                              name: name);

  Debug.Assert(table != null);

  return table;
}


/* Allocates 2D sky precomputation table. */
RTHandle allocateSky1DArrayTable(int resolution, int depth, string name) {
  var table = RTHandles.Alloc(resolution,
                              1,
                              depth,
                              dimension: TextureDimension.Tex2DArray,
                              colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                              enableRandomWrite: true,
                              name: name);

  Debug.Assert(table != null);

  return table;
}

/* Allocates 4D sky precomputation table. */
RTHandle allocateSky4DTable(Vector4 resolution, int index, string name) {
  var table = RTHandles.Alloc((int) resolution.x,
                              (int) resolution.y,
                              ((int) resolution.z) * ((int) resolution.w),
                              dimension: TextureDimension.Tex3D,
                              colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                              enableRandomWrite: true,
                              name: string.Format(name + "{0}", index));

  Debug.Assert(table != null);

  return table;
}

/* Allocates 4D sky precomputation table. */
RTHandle allocateSky4DArrayTable(Vector4 resolution, int depth, string name) {
  var table = RTHandles.Alloc(((int) resolution.x) * ((int) resolution.y),
                              ((int) resolution.z) * ((int) resolution.w),
                              depth,
                              dimension: TextureDimension.Tex2DArray,
                              colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                              enableRandomWrite: true,
                              name: name);

  Debug.Assert(table != null);

  return table;
}

/******************************************************************************/
/************************ END SKY PRECOMPUTATION TABLES ***********************/
/******************************************************************************/

private RTHandle m_proceduralStarTexture;
private RTHandle m_proceduralNebulaeTexture;
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
  }
}

void cleanupStarTextures() {
  RTHandles.Release(m_proceduralStarTexture);
  m_proceduralStarTexture = null;
}

void cleanupNebulaeTextures() {
  RTHandles.Release(m_proceduralNebulaeTexture);
  m_proceduralNebulaeTexture = null;
}

/******************************************************************************/
/****************************** PROCEDURAL STARS ******************************/
/******************************************************************************/



/******************************************************************************/
/**************************** END PROCEDURAL STARS ****************************/
/******************************************************************************/



/******************************************************************************/
/******************************** FRAMEBUFFERS ********************************/
/******************************************************************************/

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
 public RTHandle colorBuffer;
 public RTHandle transmittanceBuffer;
};
CloudRenderTexture[] m_fullscreenCloudRT;
CloudRenderTexture[] m_cubemapCloudRT;
int m_currentFullscreenCloudsRT;
int m_currentCubemapCloudsRT;
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
  m_fullscreenSkyRT = buildSkyRenderTexture(resolution, 0, "fullscreenSkyRT");
  m_fullscreenCloudRT = new CloudRenderTexture[2];
  for (int i = 0; i < 2; i++) {
    m_fullscreenCloudRT[i] = buildCloudRenderTexture(resolution, i, "fullscreenCloudRT");
  }
  m_currentFullscreenCloudsRT = 0;
  m_currentFullscreenRTSize = resolution;
}

private void buildCubemapRenderTextures(Vector2 resolution) {
  m_cubemapSkyRT = buildSkyRenderTexture(resolution, 0, "cubemapSkyRT");
  m_cubemapCloudRT = new CloudRenderTexture[2];
  for (int i = 0; i < 2; i++) {
    m_cubemapCloudRT[i] = buildCloudRenderTexture(resolution, i, "cubemapCloudRT");
  }
  m_currentCubemapCloudsRT = 0;
  m_currentCubemapRTSize = resolution;
}

private void cleanupFullscreenRenderTextures() {
  RTHandles.Release(m_fullscreenSkyRT.colorBuffer);
  m_fullscreenSkyRT.colorBuffer = null;

  for (int i = 0; i < 2; i++) {
    RTHandles.Release(m_fullscreenCloudRT[i].colorBuffer);
    RTHandles.Release(m_fullscreenCloudRT[i].transmittanceBuffer);
    m_fullscreenCloudRT[i].colorBuffer = null;
    m_fullscreenCloudRT[i].transmittanceBuffer = null;
  }
}

private void cleanupCubemapRenderTextures() {
  RTHandles.Release(m_cubemapSkyRT.colorBuffer);
  m_cubemapSkyRT.colorBuffer = null;

  for (int i = 0; i < 2; i++) {
    RTHandles.Release(m_cubemapCloudRT[i].colorBuffer);
    RTHandles.Release(m_cubemapCloudRT[i].transmittanceBuffer);
    m_cubemapCloudRT[i].colorBuffer = null;
    m_cubemapCloudRT[i].transmittanceBuffer = null;
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

/* Hash values for determining update behavior. */
int m_LastSkyHash;
int m_LastCloudHash;
int m_LastNightSkyHash;
Vector4 m_averageNightSkyColor;
private static int m_RenderCubemapSkyID = 0;
private static int m_RenderFullscreenSkyID = 1;
private static int m_RenderCubemapCloudsID = 2;
private static int m_RenderFullscreenCloudsID = 3;
private static int m_CompositeCubemapSkyAndCloudsID = 4;
private static int m_CompositeFullscreenSkyAndCloudsID = 5;

/******************************************************************************/
/**************************** END MEMBER VARIABLES ****************************/
/******************************************************************************/


public override void Build() {
  /* Create material for sky shader. */
  m_skyMaterial = CoreUtils.CreateEngineMaterial(GetSkyShader());

  /* Get handles to compute shaders. */
  m_skyCS = GetExpanseSkyPrecomputeShader();
  m_starCS = GetExpanseStarPrecomputeShader();

  /* Set up default texture quality. */
  m_skyTextureResolution = ExpanseCommon.skyQualityToSkyTextureResolution(ExpanseCommon.SkyTextureQuality.Medium);

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

public override void Cleanup()
{
  CoreUtils.Destroy(m_skyMaterial);

  cleanupFullscreenRenderTextures();
  cleanupCubemapRenderTextures();
  cleanupSkyTables();
  cleanupStarTextures();
}

private void setLightingData(Vector4 cameraPos, float planetRadius, float atmosphereRadius) {
  /* Use data from the property block, so that we don't go out of
   * sync. */
  int numActiveBodies = m_PropertyBlock.GetInt("_numActiveBodies");
  Vector4[] directions = m_PropertyBlock.GetVectorArray("_bodyDirection");
  Vector4 O = cameraPos - (new Vector4(0, -planetRadius, 0, 0));
  Vector3 O3 = new Vector3(O.x, O.y, O.z);
  for (int i = 0; i < numActiveBodies; i++) {
    /* Check if the body is occluded by the planet. */
    Vector3 L = new Vector3(directions[i].x, directions[i].y, directions[i].z);
    Vector3 intersection = ExpanseCommon.intersectSphere(O3, L, planetRadius);
    if (intersection.z >= 0 && (intersection.x >= 0 || intersection.y >= 0)) {
      ExpanseCommon.bodyTransmittances[i] = new Vector3(0, 0, 0);
    } else {
      /* Sample transmittance. */
      Vector3 skyIntersection = ExpanseCommon.intersectSphere(O3, L, atmosphereRadius);
      float r = O3.magnitude;
      float mu = Vector3.Dot(Vector3.Normalize(O3), L);
      float d = (skyIntersection.x > 0) ? (skyIntersection.y > 0 ? Mathf.Min(skyIntersection.x, skyIntersection.y) : skyIntersection.x) : skyIntersection.y;
      Vector2 uv = ExpanseCommon.map_r_mu(r, mu, atmosphereRadius, planetRadius,
        d, false);
      Vector4 transmittance = m_TTableCPU.GetPixelBilinear(uv.x, uv.y);
      ExpanseCommon.bodyTransmittances[i] = new Vector3(Mathf.Exp(transmittance.x), Mathf.Exp(transmittance.y), Mathf.Exp(transmittance.z));
    }
  }
}

private Vector4 computeAverageNightSkyColor(Expanse sky) {
  /* TODO: make more efficient. */
  if (sky.useProceduralNightSky.value) {
    /* Use an analytical hack to allow for realtime editing. */
    return sky.nightSkyIntensity.value * sky.nightSkyTint.value;
  } else if (sky.nightSkyTexture.value != null) {
    /* Actually compute the average. TODO: make more efficient. It's tough
     * because star textures are sparse, so there's no guarantee that
     * using a uniform sphere sample with a reasonable amount of samples
     * would give a good estimate. */
    Vector4 averageColor = new Vector4(0, 0, 0, 0);
    for (int i = 0; i < 6; i++) {
      Vector4 faceColor = new Vector4(0, 0, 0, 0);
      Color[] pixels = sky.nightSkyTexture.value.GetPixels(0);
      foreach (Color p in pixels) {
        faceColor += (Vector4) p;
      }
      faceColor /= pixels.Length;
      averageColor += faceColor;
    }
    averageColor /= 6;
    return averageColor;
  } else {
    return sky.nightSkyTint.value;
  }
}

protected override bool Update(BuiltinSkyParameters builtinParams)
{
  if (m_TTableCPUNeedsUpdate) {
    Debug.Log("Updating T Table");
    RenderTexture.active = m_TTable;
    m_TTableCPU.ReadPixels(new Rect(0, 0, m_TTable.rt.width, m_TTable.rt.height), 0, 0);
    m_TTableCPU.Apply();
    RenderTexture.active = null;
    m_TTableCPUNeedsUpdate = false;
  }

  var sky = builtinParams.skySettings as Expanse;

  /* Allocate the tables and update info about atmosphere layers that are
   * active. This will not reallocate if the table sizes have remained the
   * same and no atmosphere layers have been added or removed. */
  allocateSkyPrecomputationTables(sky);

  /* Allocate star textures if necessary. */
  allocateStarTextures(sky);

  /* Set everything in the material property block. */
  setMaterialPropertyBlock(builtinParams);

  /* Set everything in the global constant buffer. */
  setGlobalCBuffer(builtinParams);

  /* TODO: reallocate cloud noises. */

  /* Check the sky hash. */
  int currentSkyHash = sky.GetSkyHashCode();
  if (currentSkyHash != m_LastSkyHash) {
    /* Update the sky precomputation tables. */
    setSkyRWTextures();

    /* Run the compute shader kernels. */
    DispatchSkyCompute(builtinParams.commandBuffer);

    /* Update the CPU copy of the transmittance table. */
    m_TTableCPUNeedsUpdate = true;

    m_LastSkyHash = currentSkyHash;
  }

  int currentCloudHash = sky.GetCloudHashCode();
  if (currentCloudHash != m_LastCloudHash) {
    /* Update the cloud noise tables. */
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
  setLightingData(builtinParams.worldSpaceCameraPos, sky.planetRadius.value,
    sky.planetRadius.value + sky.atmosphereThickness.value);

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
      outputs, builtinParams.depthBuffer, m_PropertyBlock, skyPassID); // builtinParams.depthBuffer,
  }
}

private void RenderCloudsPass(BuiltinSkyParameters builtinParams, bool renderForCubemap) {
  int skyPassID = renderForCubemap ? m_RenderCubemapCloudsID : m_RenderFullscreenCloudsID;
  int currentRTID = renderForCubemap ? m_currentCubemapCloudsRT : m_currentFullscreenCloudsRT;
  int prevRTID = (currentRTID + 1) % 2;

  /* TODO: might wanna set cloud cube map tex for GI. Cool we can do that!
   * multi-pass, f-yeah! */

  /* Pass in the previous cloud render texture to use for reprojection. */
  CloudRenderTexture prevTex = (renderForCubemap ? m_cubemapCloudRT : m_fullscreenCloudRT)[prevRTID];
  if (renderForCubemap) {
    m_PropertyBlock.SetTexture("_lastCubemapCloudColorRT", prevTex.colorBuffer);
    m_PropertyBlock.SetTexture("_lastCubemapCloudTransmittanceRT", prevTex.transmittanceBuffer);
  } else {
    m_PropertyBlock.SetTexture("_lastFullscreenCloudColorRT", prevTex.colorBuffer);
    m_PropertyBlock.SetTexture("_lastFullscreenCloudTransmittanceRT", prevTex.transmittanceBuffer);
  }

  /* Set the render targets. */
  CloudRenderTexture outTex = (renderForCubemap ? m_cubemapCloudRT : m_fullscreenCloudRT)[currentRTID];
  RenderTargetIdentifier[] outputs = new RenderTargetIdentifier[] {
    new RenderTargetIdentifier(outTex.colorBuffer),
    new RenderTargetIdentifier(outTex.transmittanceBuffer)
  };

  /* Draw the clouds and update current render texture. */
  if (renderForCubemap) {
    CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_skyMaterial,
      outputs, m_PropertyBlock, skyPassID);
  } else {
    CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_skyMaterial,
      outputs, builtinParams.depthBuffer, m_PropertyBlock, skyPassID);
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
    m_PropertyBlock.SetTexture("_currCubemapCloudColorRT", m_cubemapCloudRT[m_currentCubemapCloudsRT].colorBuffer);
    m_PropertyBlock.SetTexture("_currCubemapCloudTransmittanceRT", m_cubemapCloudRT[m_currentCubemapCloudsRT].transmittanceBuffer);
    CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_skyMaterial,
      builtinParams.colorBuffer, m_PropertyBlock, compositePassID);
    m_currentCubemapCloudsRT = (m_currentCubemapCloudsRT + 1) % 2;
  } else {
    m_PropertyBlock.SetTexture("_fullscreenSkyColorRT", m_fullscreenSkyRT.colorBuffer);
    m_PropertyBlock.SetTexture("_currFullscreenCloudColorRT", m_fullscreenCloudRT[m_currentFullscreenCloudsRT].colorBuffer);
    m_PropertyBlock.SetTexture("_currFullscreenCloudTransmittanceRT", m_fullscreenCloudRT[m_currentFullscreenCloudsRT].transmittanceBuffer);
    CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_skyMaterial,
      builtinParams.colorBuffer, builtinParams.depthBuffer, m_PropertyBlock, compositePassID);
    m_currentFullscreenCloudsRT = (m_currentFullscreenCloudsRT + 1) % 2;
  }
}

private void checkAndResizeFramebuffers(BuiltinSkyParameters builtinParams, bool renderForCubemap) {
  Vector2 currSize = (renderForCubemap) ? m_currentCubemapRTSize : m_currentFullscreenRTSize;
  Vector2Int trueSize = Vector2Int.Max(new Vector2Int(1, 1),
    builtinParams.colorBuffer.GetScaledSize(builtinParams.colorBuffer.referenceSize));
  if (currSize.x != trueSize.x || currSize.y != trueSize.y) {
    if (renderForCubemap) {
      cleanupCubemapRenderTextures();
      buildCubemapRenderTextures(trueSize);
    } else {
      cleanupFullscreenRenderTextures();
      buildFullscreenRenderTextures(trueSize);
    }
  }
}

public override void RenderSky(BuiltinSkyParameters builtinParams, bool renderForCubemap, bool renderSunDisk) {
  using (new ProfilingSample(builtinParams.commandBuffer, "Draw sky"))
  {

    /* Check whether or not we have to resize the framebuffers, and do it
     * if we have to. */
    checkAndResizeFramebuffers(builtinParams, renderForCubemap);

    /* Set the depth buffer to use when computing transmittance. */

    /* Render sky pass. */
    RenderSkyPass(builtinParams, renderForCubemap);

    /* Render clouds pass. */
    RenderCloudsPass(builtinParams, renderForCubemap);

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

private void DispatchSkyCompute(CommandBuffer cmd) {
  using (new ProfilingSample(cmd, "Precompute Expanse Sky Tables"))
  {
    if (m_numAtmosphereLayersEnabled > 0) {
      int handle_T = m_skyCS.FindKernel("T");
      int handle_LP = m_skyCS.FindKernel("LP");
      int handle_GI = m_skyCS.FindKernel("GI");
      int handle_SS = m_skyCS.FindKernel("SS");
      int handle_SSAerialPerspective = m_skyCS.FindKernel("SSAerialPerspective");
      int handle_MS = m_skyCS.FindKernel("MS");
      int handle_MSAcc = m_skyCS.FindKernel("MSAcc");

      cmd.DispatchCompute(m_skyCS, handle_T,
        (int) m_skyTextureResolution.T.x / 8,
        (int) m_skyTextureResolution.T.y / 8, 1);

      cmd.DispatchCompute(m_skyCS, handle_LP,
        (int) m_skyTextureResolution.LP.x / 8,
        (int) m_skyTextureResolution.LP.y / 8, 1);

      cmd.DispatchCompute(m_skyCS, handle_SS,
        ((int) (m_skyTextureResolution.SS.x * m_skyTextureResolution.SS.y)) / 8,
        ((int) (m_skyTextureResolution.SS.z * m_skyTextureResolution.SS.w)) / 8, 1);

      cmd.DispatchCompute(m_skyCS, handle_SSAerialPerspective,
        ((int) (m_skyTextureResolution.SS.x * m_skyTextureResolution.SS.y)) / 8,
        ((int) (m_skyTextureResolution.SS.z * m_skyTextureResolution.SS.w)) / 8, 1);

      cmd.DispatchCompute(m_skyCS, handle_MS,
        (int) m_skyTextureResolution.MS.x / 8,
        (int) m_skyTextureResolution.MS.y / 8, 1);

      cmd.DispatchCompute(m_skyCS, handle_MSAcc,
        ((int) (m_skyTextureResolution.MSAccumulation.x * m_skyTextureResolution.MSAccumulation.y)) / 8,
        ((int) (m_skyTextureResolution.MSAccumulation.z * m_skyTextureResolution.MSAccumulation.z)) / 8, 1);

      cmd.DispatchCompute(m_skyCS, handle_GI,
        (int) m_skyTextureResolution.GI / 8, 1, 1);
    }
  }
}

private void DispatchStarCompute(CommandBuffer cmd) {
  using (new ProfilingSample(cmd, "Precompute Expanse Star Texture")) {
    int handle_Star = m_starCS.FindKernel("STAR");

    cmd.DispatchCompute(m_starCS, handle_Star,
      (int) m_starTextureResolution.Star.x / 8,
      (int) m_starTextureResolution.Star.y / 8, 6);
  }
}

private void DispatchNebulaeCompute(CommandBuffer cmd) {
  using (new ProfilingSample(cmd, "Precompute Expanse Nebula Texture")) {
    int handle_Nebulae = m_starCS.FindKernel("NEBULAE");

    cmd.DispatchCompute(m_starCS, handle_Nebulae,
      (int) m_nebulaeTextureResolution.Star.x / 8,
      (int) m_nebulaeTextureResolution.Star.y / 8, 6);
  }
}

/******************************************************************************/
/************************ END COMPUTE SHADER FUNCTIONS ************************/
/******************************************************************************/



/******************************************************************************/
/****************************** RW TEXTURE SETTERS ****************************/
/******************************************************************************/

private void setSkyRWTextures() {
  int handle_T = m_skyCS.FindKernel("T");
  int handle_LP = m_skyCS.FindKernel("LP");
  int handle_GI = m_skyCS.FindKernel("GI");
  int handle_SS = m_skyCS.FindKernel("SS");
  int handle_SSAerialPerspective = m_skyCS.FindKernel("SSAerialPerspective");
  int handle_MS = m_skyCS.FindKernel("MS");
  int handle_MSAcc = m_skyCS.FindKernel("MSAcc");
  if (m_numAtmosphereLayersEnabled > 0) {
    m_skyCS.SetTexture(handle_T, "_T_RW", m_TTable);
    m_skyCS.SetTexture(handle_MS, "_MS_RW", m_MSTable);
    m_skyCS.SetTexture(handle_GI, "_GI_RW", m_GITableArray);
    m_skyCS.SetTexture(handle_LP, "_LP_RW", m_LPTableArray);
    m_skyCS.SetTexture(handle_SS, "_SS_RW", m_SSTableArray);
    m_skyCS.SetTexture(handle_SS, "_SSNoShadow_RW", m_SSNoShadowTableArray);
    m_skyCS.SetTexture(handle_SSAerialPerspective, "_SSAerialPerspectiveLOD0_RW", m_SSAerialPerspectiveTableLOD0Array);
    m_skyCS.SetTexture(handle_SSAerialPerspective, "_SSAerialPerspectiveLOD1_RW", m_SSAerialPerspectiveTableLOD1Array);
    m_skyCS.SetTexture(handle_MSAcc, "_MSAcc_RW", m_MSAccumulationTableArray);
  }
}

private void setStarRWTextures() {
  int handle_Star = m_starCS.FindKernel("STAR");
  int handle_Nebulae = m_starCS.FindKernel("STAR");
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
  setGlobalCBufferAtmosphereLayers(builtinParams.commandBuffer, sky);

  /* Night Sky. */
  setGlobalCBufferNightSky(builtinParams.commandBuffer, sky);

  /* Aerial Perspective. */
  setGlobalCBufferAerialPerspective(builtinParams.commandBuffer, sky);

  /* Quality. */
  setGlobalCBufferQuality(builtinParams.commandBuffer, sky);
}

private void setGlobalCBufferPlanet(CommandBuffer cmd, Expanse sky) {
  cmd.SetGlobalFloat("_atmosphereRadius", sky.planetRadius.value + sky.atmosphereThickness.value);
  cmd.SetGlobalFloat("_planetRadius", sky.planetRadius.value);
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

private void setGlobalCBufferAtmosphereLayers(CommandBuffer cmd, Expanse sky) {

  int n = (int) ExpanseCommon.kMaxAtmosphereLayers;

  Vector4[] layerCoefficientsA = new Vector4[n];
  Vector4[] layerCoefficientsS = new Vector4[n];
  float[] layerDensityDistribution = new float[n]; /* Should be int, but unity can only set float arrays. */
  float[] layerHeight = new float[n];
  float[] layerThickness = new float[n];
  float[] layerPhaseFunction = new float[n]; /* Should be int, but unity can only set float arrays. */
  float[] layerAnisotropy = new float[n];
  float[] layerDensity = new float[n];
  float[] layerUseDensityAttenuation = new float[n];
  float[] layerAttenuationDistance = new float[n];
  float[] layerAttenuationBias = new float[n];
  Vector4[] layerTint = new Vector4[n];

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
      layerUseDensityAttenuation[numActiveLayers] = ((BoolParameter) sky.GetType().GetField("layerUseDensityAttenuation" + i).GetValue(sky)).value ? 1 : 0;
      layerAttenuationDistance[numActiveLayers] = ((MinFloatParameter) sky.GetType().GetField("layerAttenuationDistance" + i).GetValue(sky)).value;
      layerAttenuationBias[numActiveLayers] = ((MinFloatParameter) sky.GetType().GetField("layerAttenuationBias" + i).GetValue(sky)).value;
      layerTint[numActiveLayers] = ((ColorParameter) sky.GetType().GetField("layerTint" + i).GetValue(sky)).value;

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
  cmd.SetGlobalFloatArray("_layerUseDensityAttenuation", layerUseDensityAttenuation);
  cmd.SetGlobalFloatArray("_layerAttenuationDistance", layerAttenuationDistance);
  cmd.SetGlobalFloatArray("_layerAttenuationBias", layerAttenuationBias);
  cmd.SetGlobalVectorArray("_layerTint", layerTint);
}

private void setGlobalCBufferAtmosphereTables(CommandBuffer cmd, Expanse sky) {
  if (m_numAtmosphereLayersEnabled > 0) {
    cmd.SetGlobalTexture("_GI", m_GITableArray);
    cmd.SetGlobalInt("_resGI", m_skyTextureResolution.GI);
    cmd.SetGlobalTexture("_T", m_TTable);
    cmd.SetGlobalVector("_resT", m_skyTextureResolution.T);
    cmd.SetGlobalTexture("_MS", m_MSTable);
    cmd.SetGlobalVector("_resMS", m_skyTextureResolution.MS);
    cmd.SetGlobalTexture("_LP", m_LPTableArray);
    cmd.SetGlobalVector("_resLP", m_skyTextureResolution.LP);
    cmd.SetGlobalTexture("_SS", m_SSTableArray);
    cmd.SetGlobalVector("_resSS", m_skyTextureResolution.SS);
    cmd.SetGlobalTexture("_SSNoShadow", m_SSNoShadowTableArray);
    cmd.SetGlobalTexture("_SSAerialPerspectiveLOD0", m_SSAerialPerspectiveTableLOD0Array);
    cmd.SetGlobalTexture("_SSAerialPerspectiveLOD1", m_SSAerialPerspectiveTableLOD1Array);
    cmd.SetGlobalTexture("_MSAcc", m_MSAccumulationTableArray);
    cmd.SetGlobalVector("_resMSAcc", m_skyTextureResolution.MSAccumulation);
  }
}

private void setGlobalCBufferNightSky(CommandBuffer cmd, Expanse sky) {
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
    if (!sky.useProceduralNebulae.value) {
      cmd.SetGlobalFloat("_hasNebulaeTexture", (sky.nebulaeTexture.value == null) ? 0 : 1);
      if (sky.nebulaeTexture.value != null) {
        cmd.SetGlobalTexture("_nebulaeTexture", sky.nebulaeTexture.value);
      }
    } else {
      cmd.SetGlobalVector("_resNebulae", m_nebulaeTextureResolution.Star);
      cmd.SetGlobalFloat("_nebulaOverallDefinition", sky.nebulaOverallDefinition.value);
      cmd.SetGlobalFloat("_nebulaOverallIntensity", sky.nebulaOverallIntensity.value);
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

      /* Set the procedural nebulae texture for use in the star generation. */
      cmd.SetGlobalTexture("_proceduralNebulae", m_proceduralNebulaeTexture);
    }
  }
}

private void setGlobalCBufferAerialPerspective(CommandBuffer cmd, Expanse sky) {
  cmd.SetGlobalFloat("_aerialPerspectiveTableDistanceLOD0", sky.aerialPerspectiveTableDistances.value.x);
  cmd.SetGlobalFloat("_aerialPerspectiveTableDistanceLOD1", sky.aerialPerspectiveTableDistances.value.y);
}

private void setGlobalCBufferQuality(CommandBuffer cmd, Expanse sky) {
  cmd.SetGlobalInt("_numTSamples", sky.numberOfTransmittanceSamples.value);
  cmd.SetGlobalInt("_numLPSamples", sky.numberOfLightPollutionSamples.value);
  cmd.SetGlobalInt("_numSSSamples", sky.numberOfSingleScatteringSamples.value);
  cmd.SetGlobalInt("_numGISamples", sky.numberOfGroundIrradianceSamples.value);
  cmd.SetGlobalInt("_numMSSamples", sky.numberOfMultipleScatteringSamples.value);
  cmd.SetGlobalInt("_numMSAccumulationSamples", sky.numberOfMultipleScatteringAccumulationSamples.value);
  cmd.SetGlobalFloat("_useImportanceSampling", sky.useImportanceSampling.value ? 1 : 0);
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

  /* Atmosphere Layers. */
  setMaterialPropertyBlockAtmosphereLayers(sky);

  /* Celestial bodies. */
  setMaterialPropertyBlockCelestialBodies(sky);

  /* Night sky. */
  setMaterialPropertyBlockNightSky(sky);

  /* Aerial Perspective. */
  setMaterialPropertyBlockAerialPerspective(sky);

  /* Quality. */
  setMaterialPropertyBlockQuality(sky);

  m_PropertyBlock.SetVector(_WorldSpaceCameraPos1ID, builtinParams.worldSpaceCameraPos);
  m_PropertyBlock.SetMatrix(_PixelCoordToViewDirWS, builtinParams.pixelCoordToViewDirMatrix);
}

private void setMaterialPropertyBlockAtmosphereLayers(Expanse sky) {

  int n = (int) ExpanseCommon.kMaxCelestialBodies;

  /* Set up arrays to pass to shader. */
  float[] layerMultipleScatteringMultiplier = new float[n];

  int numActiveLayers = 0;
  for (int i = 0; i < ExpanseCommon.kMaxAtmosphereLayers; i++) {
    bool enabled = (((BoolParameter) sky.GetType().GetField("layerEnabled" + i).GetValue(sky)).value);
    if (enabled) {
      layerMultipleScatteringMultiplier[numActiveLayers] = ((MinFloatParameter) sky.GetType().GetField("layerMultipleScatteringMultiplier" + i).GetValue(sky)).value;
      numActiveLayers++;
    }
  }

  /* Actually set everything in the property block. */
  m_PropertyBlock.SetFloatArray("_layerMultipleScatteringMultiplier", layerMultipleScatteringMultiplier);
}

private void setMaterialPropertyBlockCelestialBodies(Expanse sky) {

  int n = (int) ExpanseCommon.kMaxCelestialBodies;

  /* Set up arrays to pass to shader. */
  Vector4[] bodyDirection = new Vector4[n];
  float[] bodyAngularRadius = new float[n];
  float[] bodyDistance = new float[n];
  float[] bodyReceivesLight = new float[n];
  Matrix4x4[] bodyAlbedoTextureRotation = new Matrix4x4[n];
  Vector4[] bodyAlbedoTint = new Vector4[n];
  float[] bodyEmissive = new float[n];
  Vector4[] bodyLightColor = new Vector4[n];
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

      /* TODO: this mapping could use work. */
      Vector3 angles = ((Vector3Parameter) sky.GetType().GetField("bodyDirection" + i).GetValue(sky)).value;
      Quaternion bodyLightRotation = Quaternion.Euler(angles.x, angles.y, angles.z);
      Vector3 direction = bodyLightRotation * (new Vector3(0, 1, 0));
      bodyDirection[numActiveBodies] = new Vector4(direction.x, direction.y, direction.z, 0);

      bodyAngularRadius[numActiveBodies] = Mathf.PI * (((ClampedFloatParameter) sky.GetType().GetField("bodyAngularRadius" + i).GetValue(sky)).value / 180);
      bodyDistance[numActiveBodies] = ((MinFloatParameter) sky.GetType().GetField("bodyDistance" + i).GetValue(sky)).value;
      bodyReceivesLight[numActiveBodies] = (((BoolParameter) sky.GetType().GetField("bodyReceivesLight" + i).GetValue(sky)).value) ? 1 : 0;

      Vector3 albedoTexRotationV3 = ((Vector3Parameter) sky.GetType().GetField("bodyAlbedoTextureRotation" + i).GetValue(sky)).value;
      Quaternion albedoTexRotation = Quaternion.Euler(albedoTexRotationV3.x,
                                                    albedoTexRotationV3.y,
                                                    albedoTexRotationV3.z);
      bodyAlbedoTextureRotation[numActiveBodies] = Matrix4x4.Rotate(albedoTexRotation);

      bodyAlbedoTint[numActiveBodies] = ((ColorParameter) sky.GetType().GetField("bodyAlbedoTint" + i).GetValue(sky)).value;
      bodyEmissive[numActiveBodies] = (((BoolParameter) sky.GetType().GetField("bodyEmissive" + i).GetValue(sky)).value) ? 1 : 0;

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
        m_PropertyBlock.SetTexture("_bodyAlbedoTexture" + i, albedoTexture);
      }

      Texture emissionTexture = ((CubemapParameter) sky.GetType().GetField("bodyEmissionTexture" + i).GetValue(sky)).value;
      bodyEmissionTextureEnabled[numActiveBodies] = (emissionTexture == null) ? 0 : 1;
      if (emissionTexture != null) {
        m_PropertyBlock.SetTexture("_bodyEmissionTexture" + i, emissionTexture);
      }

      numActiveBodies++;
    }
  }

  /* Actually set everything in the property block. */

  m_PropertyBlock.SetInt("_numActiveBodies", numActiveBodies);
  m_PropertyBlock.SetVectorArray("_bodyDirection", bodyDirection);
  m_PropertyBlock.SetFloatArray("_bodyAngularRadius", bodyAngularRadius);
  m_PropertyBlock.SetFloatArray("_bodyDistance", bodyDistance);
  m_PropertyBlock.SetFloatArray("_bodyReceivesLight", bodyReceivesLight);
  m_PropertyBlock.SetMatrixArray("_bodyAlbedoTextureRotation", bodyAlbedoTextureRotation);
  m_PropertyBlock.SetVectorArray("_bodyAlbedoTint", bodyAlbedoTint);
  m_PropertyBlock.SetFloatArray("_bodyEmissive", bodyEmissive);
  m_PropertyBlock.SetVectorArray("_bodyLightColor", bodyLightColor);
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
  } else {
    m_PropertyBlock.SetFloat("_hasNightSkyTexture", (sky.nightSkyTexture.value == null) ? 0 : 1);
    if (sky.nightSkyTexture.value != null) {
      m_PropertyBlock.SetTexture("_nightSkyTexture", sky.nightSkyTexture.value);
    }
  }

  m_PropertyBlock.SetVector("_lightPollutionTint", sky.lightPollutionTint.value
    * sky.lightPollutionIntensity.value);
  Vector3 nightSkyRotation = sky.nightSkyRotation.value;
  Quaternion nightSkyRotationMatrix = Quaternion.Euler(nightSkyRotation.x,
                                                nightSkyRotation.y,
                                                nightSkyRotation.z);
  m_PropertyBlock.SetMatrix("_nightSkyRotation", Matrix4x4.Rotate(nightSkyRotationMatrix));
  m_PropertyBlock.SetVector("_nightSkyTint", sky.nightSkyTint.value
    * sky.nightSkyIntensity.value);
  m_PropertyBlock.SetVector("_averageNightSkyColor", m_averageNightSkyColor);
  m_PropertyBlock.SetVector("_nightSkyScatterTint", sky.nightSkyTint.value
    * sky.nightSkyScatterTint.value * sky.nightSkyScatterIntensity.value
    * sky.nightSkyIntensity.value);

  m_PropertyBlock.SetFloat("_useTwinkle", (sky.useTwinkle.value) ? 1 : 0);
  m_PropertyBlock.SetFloat("_twinkleThreshold", sky.twinkleThreshold.value);
  m_PropertyBlock.SetFloat("_twinkleFrequencyMin", sky.twinkleFrequencyRange.value.x);
  m_PropertyBlock.SetFloat("_twinkleFrequencyMax", sky.twinkleFrequencyRange.value.y);
  m_PropertyBlock.SetFloat("_twinkleBias", sky.twinkleBias.value);
  m_PropertyBlock.SetFloat("_twinkleSmoothAmplitude", sky.twinkleSmoothAmplitude.value);
  m_PropertyBlock.SetFloat("_twinkleChaoticAmplitude", sky.twinkleChaoticAmplitude.value);
}

private void setMaterialPropertyBlockAerialPerspective(Expanse sky) {
  m_PropertyBlock.SetFloat("_aerialPerspectiveOcclusionBiasUniform", sky.aerialPerspectiveOcclusionBiasUniform.value);
  m_PropertyBlock.SetFloat("_aerialPerspectiveOcclusionPowerUniform", sky.aerialPerspectiveOcclusionPowerUniform.value);
    m_PropertyBlock.SetFloat("_aerialPerspectiveOcclusionBiasDirectional", sky.aerialPerspectiveOcclusionBiasDirectional.value);
    m_PropertyBlock.SetFloat("_aerialPerspectiveOcclusionPowerDirectional", sky.aerialPerspectiveOcclusionPowerDirectional.value);
}

private void setMaterialPropertyBlockQuality(Expanse sky) {
  m_PropertyBlock.SetFloat("_useAntiAliasing", sky.useAntiAliasing.value ? 1 : 0);
  m_PropertyBlock.SetFloat("_ditherAmount", sky.ditherAmount.value);
}

/******************************************************************************/
/************************ END MATERIAL PROPERTY SETTERS ***********************/
/******************************************************************************/

}
