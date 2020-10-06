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
private Expanse.ExpanseCommon.SkyTextureQuality m_skyTextureQuality;
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
    || quality != m_skyTextureQuality) {

    /* Release existing tables. */
    for (int i = 0; i < m_numAtmosphereLayersEnabled; i++) {
      releaseTablesAtIndex(i);
    }

    /* Re-allocate space for RTHandle arrays. */
    m_TTables = new RTHandle[numEnabled];
    m_SSTables = new RTHandle[numEnabled];
    m_MSTables = new RTHandle[numEnabled];
    m_MSAccumulationTables = new RTHandle[numEnabled];
    m_LPTables = new RTHandle[numEnabled];
    m_GITables = new RTHandle[numEnabled];

    ExpanseCommon.SkyTextureResolution res =
      ExpanseCommon.skyQualityToSkyTextureResolution(quality);
    for (int i = 0; i < numEnabled; i++) {
      m_TTables[i] = allocateSky2DTable(res.T, i, "SkyT");
      m_SSTables[i] = allocateSky4DTable(res.SS, i, "SkySS");
      m_MSTables[i] = allocateSky2DTable(res.MS, i, "SkyMS");
      m_MSAccumulationTables[i] = allocateSky4DTable(res.MSAccumulation, i, "SkyMSAccumulation");
      m_LPTables[i] = allocateSky2DTable(res.LP, i, "SkyLP");
      m_GITables[i] = allocateSky1DTable(res.GI, i, "SkyGT");
    }

    m_numAtmosphereLayersEnabled = numEnabled;
    m_skyTextureQuality = quality;


    Debug.Log("Reallocated " + m_numAtmosphereLayersEnabled + " tables at " + m_skyTextureQuality + " quality.");
  }
}

void releaseTablesAtIndex(int i) {
  if (m_TTables != null && m_TTables[i] != null) {
    RTHandles.Release(m_TTables[i]);
    m_TTables[i] = null;
  }
  if (m_SSTables != null && m_SSTables[i] != null) {
    RTHandles.Release(m_SSTables[i]);
    m_SSTables[i] = null;
  }
  if (m_MSTables != null && m_MSTables[i] != null) {
    RTHandles.Release(m_MSTables[i]);
    m_MSTables[i] = null;
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

/* Allocates 1D sky precomputation table. */
RTHandle allocateSky1DTable(uint resolution, int index, string name) {
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

public override void Cleanup()
{
  CoreUtils.Destroy(m_skyMaterial);

  for (int i = 0; i < m_numAtmosphereLayersEnabled; i++) {
    releaseTablesAtIndex(i);
  }

  cleanupFullscreenRenderTextures();
  cleanupCubemapRenderTextures();
}

protected override bool Update(BuiltinSkyParameters builtinParams)
{
  var sky = builtinParams.skySettings as ExpanseSky;

  /* Set everything in the material property block. */
  /* TODO: may be able to put this in update, so it doesn't happen
   * so frequently and slow things down .*/
  setMaterialPropertyBlock(builtinParams);

  /* Allocate the tables and update info about atmosphere layers that are
   * active. This will not reallocate if the table sizes have remained the
   * same and no atmosphere layers have been added or removed. */
  allocateSkyPrecomputationTables(sky);

  /* TODO: reallocate cloud noises. */

  /* Check the sky hash. */
  int currentSkyHash = sky.GetSkyHashCode();
  if (currentSkyHash != m_LastSkyHash) {
    /* Update the sky precomputation tables. */
    m_LastSkyHash = currentSkyHash;
    /* TODO */
    Debug.Log("Recomputed sky tables.");
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
    m_currentCubemapCloudsRT = prevRTID;
  } else {
    CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_skyMaterial,
      outputs, builtinParams.depthBuffer, m_PropertyBlock, skyPassID);
    m_currentFullscreenCloudsRT = prevRTID;
  }
}

private void RenderCompositePass(BuiltinSkyParameters builtinParams, bool renderForCubemap) {
  /* Set all the textures we just rendered to to be available in the
   * shader. */
  m_PropertyBlock.SetTexture("_fullscreenSkyColorRT", m_fullscreenSkyRT.colorBuffer);
  m_PropertyBlock.SetTexture("_fullscreenSkyTransmittanceRT", m_fullscreenSkyRT.transmittanceBuffer);
  m_PropertyBlock.SetTexture("_fullscreenSkyColorRT", m_fullscreenSkyRT.colorBuffer);
  m_PropertyBlock.SetTexture("_fullscreenSkyTransmittanceRT", m_fullscreenSkyRT.transmittanceBuffer);

  /* Composite. */
  int compositePassID = renderForCubemap ?
    m_CompositeCubemapSkyAndCloudsID : m_CompositeFullscreenSkyAndCloudsID;

  if (renderForCubemap) {
    CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_skyMaterial,
      builtinParams.colorBuffer, m_PropertyBlock, compositePassID);
  } else {
    CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_skyMaterial,
      builtinParams.colorBuffer, builtinParams.depthBuffer, m_PropertyBlock, compositePassID);
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


public override void RenderSky(BuiltinSkyParameters builtinParams, bool renderForCubemap, bool renderSunDisk)
{
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
/************************** MATERIAL PROPERTY SETTERS *************************/
/******************************************************************************/


private void setMaterialPropertyBlock(BuiltinSkyParameters builtinParams) {
  /* Get sky object. */
  var sky = builtinParams.skySettings as ExpanseSky;

  /* Set material properties for sky and clouds. */


  /* Celestial bodies. */
  setMaterialPropertyBlockCelestialBodies(sky);

  m_PropertyBlock.SetMatrix(_PixelCoordToViewDirWS, builtinParams.pixelCoordToViewDirMatrix);
}

private void setMaterialPropertyBlockCelestialBodies(ExpanseSky sky) {

  int n = (int) ExpanseCommon.kMaxCelestialBodies;

  /* Set up arrays to pass to shader. */
  float[] bodyEnabled = new float[n];
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

  int numActiveBodies = 0;
  for (int i = 0; i < ExpanseCommon.kMaxCelestialBodies; i++) {
    bool enabled = (((BoolParameter) sky.GetType().GetField("bodyEnabled" + i).GetValue(sky)).value);
    if (enabled) {
      /* Only set up remaining properties if this body is enabled. */
      bodyEnabled[i] = (((BoolParameter) sky.GetType().GetField("bodyEnabled" + i).GetValue(sky)).value) ? 1 : 0;

      Vector3 direction = ExpanseCommon.anglesToDirectionVector(ExpanseCommon.degreesToRadians(((Vector2Parameter) sky.GetType().GetField("bodyDirection" + i).GetValue(sky)).value));
      bodyDirection[i] = new Vector4(direction.x, direction.y, direction.z, 0);

      bodyAngularRadius[i] = ((ClampedFloatParameter) sky.GetType().GetField("bodyAngularRadius" + i).GetValue(sky)).value;
      bodyDistance[i] = ((MinFloatParameter) sky.GetType().GetField("bodyDistance" + i).GetValue(sky)).value;
      bodyReceivesLight[i] = (((BoolParameter) sky.GetType().GetField("bodyReceivesLight" + i).GetValue(sky)).value) ? 1 : 0;

      Vector3 albedoTexRotationV3 = ((Vector3Parameter) sky.GetType().GetField("bodyAlbedoTextureRotation" + i).GetValue(sky)).value;
      Quaternion albedoTexRotation = Quaternion.Euler(albedoTexRotationV3.x,
                                                    albedoTexRotationV3.y,
                                                    albedoTexRotationV3.z);
      bodyAlbedoTextureRotation[i] = Matrix4x4.Rotate(albedoTexRotation);

      bodyAlbedoTint[i] = ((ColorParameter) sky.GetType().GetField("bodyAlbedoTint" + i).GetValue(sky)).value;
      bodyEmissive[i] = (((BoolParameter) sky.GetType().GetField("bodyEmissive" + i).GetValue(sky)).value) ? 1 : 0;

      bool useTemperature = ((BoolParameter) sky.GetType().GetField("bodyUseTemperature" + i).GetValue(sky)).value;
      float lightIntensity = ((MinFloatParameter) sky.GetType().GetField("bodyLightIntensity" + i).GetValue(sky)).value;
      Vector4 lightColor = ((ColorParameter) sky.GetType().GetField("bodyLightColor" + i).GetValue(sky)).value;
      if (useTemperature) {
        float temperature = ((ClampedFloatParameter) sky.GetType().GetField("bodyLightTemperature" + i).GetValue(sky)).value;
        Vector4 temperatureColor = blackbodyTempToColor(temperature);
        bodyLightColor[i] = lightIntensity * (new Vector4(temperatureColor.x * lightColor.x,
          temperatureColor.y * lightColor.y,
          temperatureColor.z * lightColor.z,
          temperatureColor.w * lightColor.w));
      } else {
        bodyLightColor[i] = lightColor * lightIntensity;
      }

      bodyLimbDarkening[i] = ((MinFloatParameter) sky.GetType().GetField("bodyLimbDarkening" + i).GetValue(sky)).value;


      Vector3 emissionTexRotationV3 = ((Vector3Parameter) sky.GetType().GetField("bodyEmissionTextureRotation" + i).GetValue(sky)).value;
      Quaternion emissionTexRotation = Quaternion.Euler(emissionTexRotationV3.x,
                                                    emissionTexRotationV3.y,
                                                    emissionTexRotationV3.z);
      bodyEmissionTextureRotation[i] = Matrix4x4.Rotate(emissionTexRotation);

      float emissionMultiplier = ((MinFloatParameter) sky.GetType().GetField("bodyEmissionMultiplier" + i).GetValue(sky)).value;
      bodyEmissionTint[i] = emissionMultiplier * ((ColorParameter) sky.GetType().GetField("bodyEmissionTint" + i).GetValue(sky)).value;

      numActiveBodies++;
    }
  }

  /* Actually set everything in the property block. */

  m_PropertyBlock.SetInt("_numActiveBodies", numActiveBodies);
  m_PropertyBlock.SetFloatArray("_bodyEnabled", bodyEnabled);
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

  //
  // /* TODO: texcube array...? commenting out for now*/
  // // _bodyAlbedoTexture0, bodyAlbedoTexture1, bodyAlbedoTexture2,
  // //   bodyAlbedoTexture3, bodyAlbedoTexture4, bodyAlbedoTexture5, bodyAlbedoTexture6, bodyAlbedoTexture7;

  //
  // /* TODO: texcube array...? commenting out for now. */
  // // _bodyEmissionTexture0, bodyEmissionTexture1, bodyEmissionTexture2,
  // //   bodyEmissionTexture3, bodyEmissionTexture4, bodyEmissionTexture5, bodyEmissionTexture6, bodyEmissionTexture7;
  // /* Displayed on null check of body albedo texture. */
  //
  //
}

/******************************************************************************/
/************************ END MATERIAL PROPERTY SETTERS ***********************/
/******************************************************************************/

}
