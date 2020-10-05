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

public static readonly int _SkyParam = Shader.PropertyToID("_SkyParam");
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
 * specified quality level.
 * TODO: something seems to work probabilistically about this function. */
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
/****************************** MEMBER VARIABLES ******************************/
/******************************************************************************/

Material m_skyMaterial;
MaterialPropertyBlock m_PropertyBlock = new MaterialPropertyBlock();
/* Hash values for determining update behavior. */
int m_LastSkyHash;
int m_LastCloudHash;
private static int m_RenderCubemapSkyID = 0;       // FragBakeSky
private static int m_RenderFullscreenSkyID = 1;    // FragRenderSky
private static int m_RenderCubemapCloudsID = 2;    // FragBakeClouds
private static int m_RenderFullscreenCloudsID = 3; // FragRenderClouds
private static int m_CompositeCubemapSkyAndCloudsID = 2;    // FragBakeComposite // TODO: wrong temporarily
private static int m_CompositeFullscreenSkyAndCloudsID = 3; // FragRenderComposite

/******************************************************************************/
/**************************** END MEMBER VARIABLES ****************************/
/******************************************************************************/

RTHandle[] m_skyRT;

public override void Build() {
  /* Create material for sky shader. */
  m_skyMaterial = CoreUtils.CreateEngineMaterial(GetSkyShader());

  m_skyRT = new RTHandle[1];
  m_skyRT[0] = allocateSky2DTable(new Vector2(Screen.width, Screen.height), 0, "ScreenRT");
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
  RTHandles.Release(m_skyRT[0]);
  m_skyRT[0] = null;
}

protected override bool Update(BuiltinSkyParameters builtinParams)
{
  var sky = builtinParams.skySettings as ExpanseSky;

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

public override void RenderSky(BuiltinSkyParameters builtinParams, bool renderForCubemap, bool renderSunDisk)
{
  using (new ProfilingSample(builtinParams.commandBuffer, "Draw sky"))
  {
    /* Get sky object. */
    var sky = builtinParams.skySettings as ExpanseSky;

    /* Set material properties for sky and clouds. */
    float intensity = GetSkyIntensity(sky, builtinParams.debugSettings);
    float phi = -Mathf.Deg2Rad * sky.rotation.value; // -rotation to match Legacy
    m_PropertyBlock.SetVector(_SkyParam, new Vector4(intensity, 0.0f, Mathf.Cos(phi), Mathf.Sin(phi)));
    m_PropertyBlock.SetMatrix(_PixelCoordToViewDirWS, builtinParams.pixelCoordToViewDirMatrix);

    /* Render sky pass. */

    int skyPassID = renderForCubemap ? m_RenderCubemapSkyID : m_RenderFullscreenSkyID;
    CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_skyMaterial, m_skyRT[0], m_PropertyBlock, skyPassID);

    m_PropertyBlock.SetTexture("_SkyRTFullscreen", m_skyRT[0]);

    /* Render clouds pass. */
    /* Composite. */
    int compositePassID = renderForCubemap ?
      m_CompositeCubemapSkyAndCloudsID : m_CompositeFullscreenSkyAndCloudsID;
    if (!renderForCubemap) {
      CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_skyMaterial, builtinParams.colorBuffer, builtinParams.depthBuffer, m_PropertyBlock, compositePassID);
    }
  }
}

}
