using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Experimental.Rendering;
using Expanse;

class ExpanseSkyRenderer : SkyRenderer
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

private RTHandle[] m_TTables;                     /* Transmittance. */
private RTHandle[] m_SSTables;                    /* Single Scattering. */
private RTHandle[] m_MSTables;                    /* Multiple Scattering. */
private RTHandle[] m_MSAccumulationTables;        /* Multiple Scattering. */
private RTHandle[] m_LPTables;                    /* Light Pollution. */
private RTHandle[] m_GITables;                    /* Ground Irradiance. */
/* For checking if table reallocation is required. */
private Expanse.ExpanseCommon.SkyTextureResolution m_skyTextureResolution;
private int m_numAtmosphereLayersEnabled = 0;
/* Allocates all sky precomputation tables for all atmosphere layers at a
 * specified quality level. */
void allocateSkyPrecomputationTables(ExpanseSky sky) {

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

    /* Re-allocate space for RTHandle arrays. */
    m_TTables = new RTHandle[1];
    m_SSTables = new RTHandle[2 * numEnabled]; /* Shadow and no shadow. */
    m_MSTables = new RTHandle[1];
    m_MSAccumulationTables = new RTHandle[numEnabled];
    m_LPTables = new RTHandle[numEnabled];
    m_GITables = new RTHandle[numEnabled];


    ExpanseCommon.SkyTextureResolution res =
      ExpanseCommon.skyQualityToSkyTextureResolution(quality);

    m_TTables[0] = allocateSky2DTable(res.T, 0, "SkyT");
    m_MSTables[0] = allocateSky2DTable(res.MS, 0, "SkyMS");

    for (int i = 0; i < numEnabled; i++) {
      m_SSTables[2*i] = allocateSky4DTable(res.SS, i, "SkySS");
      m_SSTables[2*i+1] = allocateSky4DTable(res.SS, i, "SkySSNoShadow");
      m_MSAccumulationTables[i] = allocateSky4DTable(res.MSAccumulation, i, "SkyMSAccumulation");
      m_LPTables[i] = allocateSky2DTable(res.LP, i, "SkyLP");
      m_GITables[i] = allocateSky1DTable(res.GI, i, "SkyGT");
    }

    m_numAtmosphereLayersEnabled = numEnabled;
    m_skyTextureResolution = res;


    Debug.Log("Reallocated " + m_numAtmosphereLayersEnabled + " tables at " + m_skyTextureResolution.quality + " quality.");
  }
}

void releaseTablesAtIndex(int i) {

  if (m_SSTables != null && m_SSTables[2*i] != null) {
    RTHandles.Release(m_SSTables[2*i]);
    m_SSTables[2*i] = null;
  }
  if (m_SSTables != null && m_SSTables[2*i+1] != null) {
    RTHandles.Release(m_SSTables[2*i+1]);
    m_SSTables[2*i+1] = null;
  }
  if (m_MSAccumulationTables != null && m_MSAccumulationTables[i] != null) {
    RTHandles.Release(m_MSAccumulationTables[i]);
    m_MSAccumulationTables[i] = null;
  }
  if (m_LPTables != null && m_LPTables[i] != null) {
    RTHandles.Release(m_LPTables[i]);
    m_LPTables[i] = null;
  }
  if (m_GITables != null && m_GITables[i] != null) {
    RTHandles.Release(m_GITables[i]);
    m_GITables[i] = null;
  }
}

void cleanupSkyTables() {
  if (m_TTables != null && m_TTables[0] != null) {
    RTHandles.Release(m_TTables[0]);
    m_TTables[0] = null;
  }
  if (m_MSTables != null && m_MSTables[0] != null) {
    RTHandles.Release(m_MSTables[0]);
    m_MSTables[0] = null;
  }
  for (int i = 0; i < m_numAtmosphereLayersEnabled; i++) {
    releaseTablesAtIndex(i);
  }
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

/******************************************************************************/
/************************ END SKY PRECOMPUTATION TABLES ***********************/
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
  public RTHandle transmittanceBuffer;
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
  r.transmittanceBuffer = allocateSky2DTable(resolution, index, name + "_Transmittance");
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
  RTHandles.Release(m_fullscreenSkyRT.transmittanceBuffer);
  m_fullscreenSkyRT.colorBuffer = null;
  m_fullscreenSkyRT.transmittanceBuffer = null;

  for (int i = 0; i < 2; i++) {
    RTHandles.Release(m_fullscreenCloudRT[i].colorBuffer);
    RTHandles.Release(m_fullscreenCloudRT[i].transmittanceBuffer);
    m_fullscreenCloudRT[i].colorBuffer = null;
    m_fullscreenCloudRT[i].transmittanceBuffer = null;
  }
}

private void cleanupCubemapRenderTextures() {
  RTHandles.Release(m_cubemapSkyRT.colorBuffer);
  RTHandles.Release(m_cubemapSkyRT.transmittanceBuffer);
  m_cubemapSkyRT.colorBuffer = null;
  m_cubemapSkyRT.transmittanceBuffer = null;

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

/* Hash values for determining update behavior. */
int m_LastSkyHash;
int m_LastCloudHash;
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
  return Shader.Find("Hidden/HDRP/Sky/ExpanseSky");
}

/* Returns reference to expanse sky precompute shader. */
ComputeShader GetExpanseSkyPrecomputeShader() {
    return Resources.Load<ComputeShader>("ExpanseSkyPrecompute");
}

public override void Cleanup()
{
  CoreUtils.Destroy(m_skyMaterial);

  cleanupSkyTables();
  cleanupFullscreenRenderTextures();
  cleanupCubemapRenderTextures();
}

protected override bool Update(BuiltinSkyParameters builtinParams)
{
  var sky = builtinParams.skySettings as ExpanseSky;

  /* Allocate the tables and update info about atmosphere layers that are
   * active. This will not reallocate if the table sizes have remained the
   * same and no atmosphere layers have been added or removed. */
  allocateSkyPrecomputationTables(sky);

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

    Debug.Log("Recomputed sky tables.");

    m_LastSkyHash = currentSkyHash;
  }

  int currentCloudHash = sky.GetCloudHashCode();
  if (currentCloudHash != m_LastCloudHash) {
    /* Update the cloud noise tables. */
    m_LastCloudHash = currentCloudHash;
    /* TODO */
    Debug.Log("Regenerated cloud noise textures.");
  }

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
    new RenderTargetIdentifier(outTex.transmittanceBuffer)
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
  int skyPassID = renderForCubemap ? m_RenderCubemapCloudsID : m_RenderFullscreenCloudsID;
  int currentRTID = renderForCubemap ? m_currentCubemapCloudsRT : m_currentFullscreenCloudsRT;
  int prevRTID = (currentRTID + 1) % 2;

  /* TODO: might wanna set cloud cube map tex for GI. Cool we can do that!
   * multi-pass, f-yeah! */

  /* Pass in the previous cloud render texture to use for reprojection.
   * TODO: probably also pass in camera velocity here. */
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
    m_PropertyBlock.SetTexture("_cubemapSkyTransmittanceRT", m_cubemapSkyRT.transmittanceBuffer);
    m_PropertyBlock.SetTexture("_currCubemapCloudColorRT", m_cubemapCloudRT[m_currentCubemapCloudsRT].colorBuffer);
    m_PropertyBlock.SetTexture("_currCubemapCloudTransmittanceRT", m_cubemapCloudRT[m_currentCubemapCloudsRT].transmittanceBuffer);
    CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_skyMaterial,
      builtinParams.colorBuffer, m_PropertyBlock, compositePassID);
    m_currentCubemapCloudsRT = (m_currentCubemapCloudsRT + 1) % 2;
  } else {
    m_PropertyBlock.SetTexture("_fullscreenSkyColorRT", m_fullscreenSkyRT.colorBuffer);
    m_PropertyBlock.SetTexture("_fullscreenSkyTransmittanceRT", m_fullscreenSkyRT.transmittanceBuffer);
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
    if (!renderForCubemap) {
      m_PropertyBlock.SetTexture("_depthBuffer", builtinParams.depthBuffer);
    }

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

private void DispatchSkyCompute(CommandBuffer cmd) {
  using (new ProfilingSample(cmd, "Precompute Expanse Sky Tables"))
  {
    int handle_T = m_skyCS.FindKernel("T");
    int handle_LP = m_skyCS.FindKernel("LP");
    int handle_GI = m_skyCS.FindKernel("GI");
    int handle_SS1 = m_skyCS.FindKernel("SS1");
    int handle_SS2 = m_skyCS.FindKernel("SS2");
    int handle_MS = m_skyCS.FindKernel("MS");
    int handle_MSAcc = m_skyCS.FindKernel("MSAcc");

    cmd.DispatchCompute(m_skyCS, handle_T,
      (int) m_skyTextureResolution.T.x / 4,
      (int) m_skyTextureResolution.T.y / 4, 1);


    cmd.DispatchCompute(m_skyCS, handle_LP,
      (int) m_skyTextureResolution.LP.x / 4,
      (int) m_skyTextureResolution.LP.y / 4, 1);


    cmd.DispatchCompute(m_skyCS, handle_SS1,
      (int) m_skyTextureResolution.SS.x / 4,
      (int) m_skyTextureResolution.SS.y / 4,
      (int) (m_skyTextureResolution.SS.z * m_skyTextureResolution.SS.w) / 4);

    if (m_numAtmosphereLayersEnabled > 4) {
      cmd.DispatchCompute(m_skyCS, handle_SS2,
        (int) m_skyTextureResolution.SS.x / 4,
        (int) m_skyTextureResolution.SS.y / 4,
        (int) (m_skyTextureResolution.SS.z * m_skyTextureResolution.SS.w) / 4);
    }

    cmd.DispatchCompute(m_skyCS, handle_MS,
      (int) m_skyTextureResolution.MS.x / 4,
      (int) m_skyTextureResolution.MS.y / 4, 1);


    cmd.DispatchCompute(m_skyCS, handle_MSAcc,
      (int) m_skyTextureResolution.MSAccumulation.x / 4,
      (int) m_skyTextureResolution.MSAccumulation.y / 4,
      (int) (m_skyTextureResolution.MSAccumulation.z * m_skyTextureResolution.MSAccumulation.z) / 4);


    cmd.DispatchCompute(m_skyCS, handle_GI,
      (int) m_skyTextureResolution.GI / 4, 1, 1);
  }
}

/******************************************************************************/
/************************** COMPUTE SHADER FUNCTIONS **************************/
/******************************************************************************/



/******************************************************************************/
/************************ END COMPUTE SHADER FUNCTIONS ************************/
/******************************************************************************/



/******************************************************************************/
/************************* PHYSICAL PROPERTY FUNCTIONS ************************/
/******************************************************************************/

private Vector4 blackbodyTempToColor(float t) {
  t = t / 100;
  float r = 0;
  float g = 0;
  float b = 0;

  /* Red. */
  if (t <= 66) {
    r = 255;
  } else {
    r = t - 60;
    r = 329.698727446f * (Mathf.Pow(r, -0.1332047592f));
  }

  /* Green. */
  if (t <= 66) {
    g = t;
    g = 99.4708025861f * Mathf.Log(t) - 161.1195681661f;
  } else {
    g = 288.1221695283f * (Mathf.Pow((t-60), -0.0755148492f));
  }

  /* Blue. */
  if (t >= 66) {
    b = 255;
  } else {
    if (t <= 19) {
      b = 0;
    } else {
      b = 138.5177312231f * Mathf.Log(t-b) - 305.0447927307f;
    }
  }

  r = Mathf.Clamp(r, 0, 255) / 255;
  g = Mathf.Clamp(g, 0, 255) / 255;
  b = Mathf.Clamp(b, 0, 255) / 255;
  return new Vector4(r, g, b, 0);
}

/******************************************************************************/
/*********************** END PHYSICAL PROPERTY FUNCTIONS **********************/
/******************************************************************************/



/******************************************************************************/
/****************************** RW TEXTURE SETTERS ****************************/
/******************************************************************************/

private void setSkyRWTextures() {
  int handle_T = m_skyCS.FindKernel("T");
  int handle_LP = m_skyCS.FindKernel("LP");
  int handle_GI = m_skyCS.FindKernel("GI");
  int handle_SS1 = m_skyCS.FindKernel("SS1");
  int handle_SS2 = m_skyCS.FindKernel("SS2");
  int handle_MS = m_skyCS.FindKernel("MS");
  int handle_MSAcc = m_skyCS.FindKernel("MSAcc");

  /* TODO: we have only one multiple scattering and one transmittance
   * table. */
  m_skyCS.SetTexture(handle_T, "_T_RW", m_TTables[0]);
  m_skyCS.SetTexture(handle_MS, "_MS_RW", m_MSTables[0]);

  for (int i = 0; i < m_numAtmosphereLayersEnabled; i++) {
    /* Only set up properties if this layer is enabled. */
    m_skyCS.SetTexture(handle_LP, "_LP" + i + "_RW", m_LPTables[i]);
    m_skyCS.SetTexture(handle_MSAcc, "_MSAcc" + i + "_RW", m_MSAccumulationTables[i]);
    m_skyCS.SetTexture(handle_GI, "_GI" + i + "_RW", m_GITables[i]);
  }

  Debug.Log(m_numAtmosphereLayersEnabled);

  /* First half of SS tables go into ss1. */
  for (int i = 0; i < Mathf.Min(4, m_numAtmosphereLayersEnabled); i++) {
    m_skyCS.SetTexture(handle_SS1, "_SS" + i + "_RW", m_SSTables[2*i]);
    m_skyCS.SetTexture(handle_SS1, "_SSNoShadow" + i + "_RW", m_SSTables[2*i+1]);
  }

  /* Second half of SS tables go into ss1. */
  for (int i = 4; i < m_numAtmosphereLayersEnabled; i++) {
    m_skyCS.SetTexture(handle_SS2, "_SS" + i + "_RW", m_SSTables[2*i]);
    m_skyCS.SetTexture(handle_SS2, "_SSNoShadow" + i + "_RW", m_SSTables[2*i+1]);
  }
}

/******************************************************************************/
/**************************** END RW TEXTURE SETTERS **************************/
/******************************************************************************/



/******************************************************************************/
/*************************** GLOBAL C BUFFER SETTERS **************************/
/******************************************************************************/

private void setGlobalCBuffer(BuiltinSkyParameters builtinParams) {
  /* Get sky object. */
  var sky = builtinParams.skySettings as ExpanseSky;

  /* Precomputed Tables. */
  setGlobalCBufferAtmosphereTables(builtinParams.commandBuffer, sky);

  /* Planet. */
  setGlobalCBufferPlanet(builtinParams.commandBuffer, sky);

  /* Atmosphere. */
  setGlobalCBufferAtmosphereLayers(builtinParams.commandBuffer, sky);

  /* Quality. */
  setGlobalCBufferQuality(builtinParams.commandBuffer, sky);
}

private void setGlobalCBufferPlanet(CommandBuffer cmd, ExpanseSky sky) {
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

private void setGlobalCBufferAtmosphereLayers(CommandBuffer cmd, ExpanseSky sky) {

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
      layerUseDensityAttenuation[numActiveLayers] = ((BoolParameter) sky.GetType().GetField("layerUseDensityAttenuation" + i).GetValue(sky)).value ? 1 : 0;
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
  cmd.SetGlobalFloatArray("_layerUseDensityAttenuation", layerUseDensityAttenuation);
  cmd.SetGlobalFloatArray("_layerAttenuationDistance", layerAttenuationDistance);
  cmd.SetGlobalFloatArray("_layerAttenuationBias", layerAttenuationBias);
  cmd.SetGlobalVectorArray("_layerTint", layerTint);
  cmd.SetGlobalFloatArray("_layerMultipleScatteringMultiplier", layerMultipleScatteringMultiplier);
}

private void setGlobalCBufferAtmosphereTables(CommandBuffer cmd, ExpanseSky sky) {
  for (int i = 0; i < m_numAtmosphereLayersEnabled; i++) {
    /* Only set up properties if this layer is enabled. */
    cmd.SetGlobalTexture("_LP" + i, m_LPTables[i]);
    cmd.SetGlobalVector("_resLP", m_skyTextureResolution.LP);
    cmd.SetGlobalTexture("_SS" + i, m_SSTables[2*i]);
    cmd.SetGlobalTexture("_SSNoShadow" + i, m_SSTables[2*i+1]);
    cmd.SetGlobalVector("_resSS", m_skyTextureResolution.SS);
    cmd.SetGlobalTexture("_MSAcc" + i, m_MSAccumulationTables[i]);
    cmd.SetGlobalVector("_resMSAcc", m_skyTextureResolution.MSAccumulation);
    cmd.SetGlobalTexture("_GI" + i, m_GITables[i]);
    cmd.SetGlobalInt("_resGI", m_skyTextureResolution.GI);
  }

  /* TODO: we have only one multiple scattering and one transmittance
   * table. */
  cmd.SetGlobalTexture("_T", m_TTables[0]);
  cmd.SetGlobalVector("_resT", m_skyTextureResolution.T);
  cmd.SetGlobalTexture("_MS", m_MSTables[0]);
  cmd.SetGlobalVector("_resMS", m_skyTextureResolution.MS);
}

private void setGlobalCBufferQuality(CommandBuffer cmd, ExpanseSky sky) {
  cmd.SetGlobalInt("_numTSamples", sky.numberOfTransmittanceSamples.value);
  cmd.SetGlobalInt("_numLPSamples", sky.numberOfLightPollutionSamples.value);
  cmd.SetGlobalInt("_numSSSamples", sky.numberOfSingleScatteringSamples.value);
  cmd.SetGlobalInt("_numGISamples", sky.numberOfGroundIrradianceSamples.value);
  cmd.SetGlobalInt("_numMSSamples", sky.numberOfMultipleScatteringSamples.value);
  cmd.SetGlobalInt("_numMSAccumulationSamples", sky.numberOfMultipleScatteringAccumulationSamples.value);
  cmd.SetGlobalFloat("_useImportanceSampling", sky.useImportanceSampling.value ? 0 : 1);
}

/******************************************************************************/
/************************* END GLOBAL C BUFFER SETTERS ************************/
/******************************************************************************/



/******************************************************************************/
/************************** MATERIAL PROPERTY SETTERS *************************/
/******************************************************************************/

private void setMaterialPropertyBlock(BuiltinSkyParameters builtinParams) {
  /* Get sky object. */
  var sky = builtinParams.skySettings as ExpanseSky;

  /* Celestial bodies. */
  setMaterialPropertyBlockCelestialBodies(sky);

  /* Quality. */
  setMaterialPropertyBlockQuality(sky);

  m_PropertyBlock.SetVector(_WorldSpaceCameraPos1ID, builtinParams.worldSpaceCameraPos);
  m_PropertyBlock.SetMatrix(_PixelCoordToViewDirWS, builtinParams.pixelCoordToViewDirMatrix);
}

private void setMaterialPropertyBlockCelestialBodies(ExpanseSky sky) {

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
      Vector2 angles = ((Vector2Parameter) sky.GetType().GetField("bodyDirection" + i).GetValue(sky)).value;
      angles.x += 90;
      angles.y += 90;

      Vector3 direction = ExpanseCommon.anglesToDirectionVector(ExpanseCommon.degreesToRadians(angles));
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
        Vector4 temperatureColor = blackbodyTempToColor(temperature);
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

private void setMaterialPropertyBlockQuality(ExpanseSky sky) {
  m_PropertyBlock.SetFloat("_useAntiAliasing", sky.useAntiAliasing.value ? 1 : 0);
  m_PropertyBlock.SetFloat("_ditherAmount", sky.ditherAmount.value);
}

/******************************************************************************/
/************************ END MATERIAL PROPERTY SETTERS ***********************/
/******************************************************************************/

}
