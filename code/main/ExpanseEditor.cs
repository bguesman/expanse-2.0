using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor.Rendering.HighDefinition;
using UnityEditor.AnimatedValues;
using ExpanseCommonNamespace;

// [CanEditMultipleObjects]
[VolumeComponentEditor(typeof(Expanse))]
class ExpanseEditor : SkySettingsEditor
{

/******************************************************************************/
/******************************* UI VARIABLES *********************************/
/******************************************************************************/

/* Specifies if an atmosphere layer ui should be shown. */
AnimBool[] m_showAtmosphereLayer =
  new AnimBool[ExpanseCommon.kMaxAtmosphereLayers];
/* Which atmosphere layer is currently shown for editing. */
ExpanseCommon.AtmosphereLayer m_atmosphereLayerSelect = ExpanseCommon.AtmosphereLayer.Layer0;

/* Specifies if a celestial body ui should be shown. */
AnimBool[] m_showCelestialBody =
  new AnimBool[ExpanseCommon.kMaxCelestialBodies];
/* Which celestial body is currently shown for editing. */
ExpanseCommon.CelestialBody m_celestialBodySelect = ExpanseCommon.CelestialBody.Body0;

/* Specifies if a cloud layer ui should be shown. */
AnimBool[] m_showCloudLayer =
  new AnimBool[ExpanseCommon.kMaxCloudLayers];
/* Which cloud layer is currently shown for editing. */
ExpanseCommon.CloudLayer m_cloudLayerSelect = ExpanseCommon.CloudLayer.Layer0;

/* Specifies if a cloud noise layer ui should be shown. */
AnimBool[] m_showCloudNoiseLayer =
  new AnimBool[ExpanseCommon.kCloudNoiseLayers];
/* Which cloud noise layer is currently shown for editing. */
ExpanseCommon.CloudNoiseLayer m_cloudNoiseLayerSelect = ExpanseCommon.CloudNoiseLayer.Base;

/* Foldout state. */
bool planetFoldout = false;
bool atmosphereFoldout = false;
bool celestialBodyFoldout = false;
/* Begin night sky foldouts. */
bool nightSkyFoldout = false;
bool nightSkySkyFoldout = false;
bool nightSkyStarsFoldout = false;
bool nightSkyTwinkleFoldout = false;
bool nightSkyLightPollutionFoldout = false;
bool nightSkyNebulaeFoldout = false;
bool nightSkyNebulaeGeneralFoldout = false;
bool nightSkyNebulaeLayerEditorFoldout = false;
/* End night sky foldouts. */
bool aerialPerspectiveFoldout = false;
bool qualityFoldout = false;
/* Cloud foldouts. */
bool cloudGeometryFoldout = false;
bool cloudNoiseFoldout = false;
bool cloudMovementFoldout = false;
bool cloudLightingFoldout = false;
bool cloudSamplingFoldout = false;

/******************************************************************************/
/****************************** END UI VARIABLES ******************************/
/******************************************************************************/











/******************************************************************************/
/*************************** SERIALIZED PARAMETERS ****************************/
/******************************************************************************/

/***********************/
/********* Sky *********/
/***********************/
/* Planet parameters. */
SerializedDataParameter atmosphereThickness;
SerializedDataParameter planetRadius;
SerializedDataParameter planetOriginOffset;
SerializedDataParameter planetRotation;
SerializedDataParameter groundAlbedoTexture;
SerializedDataParameter groundTint;
SerializedDataParameter groundEmissionTexture;
SerializedDataParameter groundEmissionTint;
SerializedDataParameter groundEmissionMultiplier;

/* Atmosphere layers. */
SerializedDataParameter[] layerEnabled
 = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerCoefficientsA
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerCoefficientsS
 = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerDensityDistribution
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerHeight
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
/* Only used for tent layers. */
SerializedDataParameter[] layerThickness
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerPhaseFunction
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerAnisotropy
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerDensityAttenuationPlayerOrigin
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerDensityAttenuationOrigin
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerDensity
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerAttenuationDistance
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerAttenuationBias
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerTint
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];
SerializedDataParameter[] layerMultipleScatteringMultiplier
  = new SerializedDataParameter[ExpanseCommon.kMaxAtmosphereLayers];

/* Celestial Bodies. */
SerializedDataParameter[] bodyEnabled
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyUseDateTime
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyDirection
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyDateTime
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyPlayerLatitudeLongitude
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyAngularRadius
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyDistance
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyReceivesLight
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyAlbedoTexture
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
/* Displayed on null check of body albedo texture. */
SerializedDataParameter[] bodyAlbedoTextureRotation
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyAlbedoTint
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyEmissive
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyLightIntensity
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyUseTemperature
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
/* Display as "filter" in temperature mode. */
SerializedDataParameter[] bodyLightColor
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyLightTemperature
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyLimbDarkening
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyEmissionTexture
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
/* Displayed on null check of body albedo texture. */
SerializedDataParameter[] bodyEmissionTextureRotation
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyEmissionTint
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];
SerializedDataParameter[] bodyEmissionMultiplier
  = new SerializedDataParameter[ExpanseCommon.kMaxCelestialBodies];

/* Night Sky. */
SerializedDataParameter useProceduralNightSky;
/* Stars. */
SerializedDataParameter starTextureQuality;
SerializedDataParameter showStarSeeds;
SerializedDataParameter starDensity;
SerializedDataParameter starDensitySeed;
SerializedDataParameter starSizeRange;
SerializedDataParameter starSizeBias;
SerializedDataParameter starSizeSeed;
SerializedDataParameter starIntensityRange;
SerializedDataParameter starIntensityBias;
SerializedDataParameter starIntensitySeed;
SerializedDataParameter starTemperatureRange;
SerializedDataParameter starTemperatureBias;
SerializedDataParameter starTemperatureSeed;
SerializedDataParameter useHighDensityMode;
SerializedDataParameter starTint;
/* Nebulae. */
SerializedDataParameter useProceduralNebulae;
SerializedDataParameter nebulaeTextureQuality;
/* Procedural nebulae. */
SerializedDataParameter nebulaOverallDefinition;
SerializedDataParameter nebulaOverallIntensity;
SerializedDataParameter nebulaCoverageScale;

/* For UI selection dropdown. */
string[] nebulaLayerDropdownOptions = { "Haze", "Clouds", "Big Strands", "Small Strands" };
int nebulaLayerDropdownSelection = 0;

SerializedDataParameter nebulaHazeBrightness;
SerializedDataParameter nebulaHazeColor;
SerializedDataParameter nebulaHazeScale;
SerializedDataParameter nebulaHazeScaleFactor;
SerializedDataParameter nebulaHazeDetailBalance;
SerializedDataParameter nebulaHazeOctaves;
SerializedDataParameter nebulaHazeBias;
SerializedDataParameter nebulaHazeSpread;
SerializedDataParameter nebulaHazeCoverage;
SerializedDataParameter nebulaHazeStrength;

SerializedDataParameter nebulaCloudBrightness;
SerializedDataParameter nebulaCloudColor;
SerializedDataParameter nebulaCloudScale;
SerializedDataParameter nebulaCloudScaleFactor;
SerializedDataParameter nebulaCloudDetailBalance;
SerializedDataParameter nebulaCloudOctaves;
SerializedDataParameter nebulaCloudBias;
SerializedDataParameter nebulaCloudSpread;
SerializedDataParameter nebulaCloudCoverage;
SerializedDataParameter nebulaCloudStrength;

SerializedDataParameter nebulaCoarseStrandBrightness;
SerializedDataParameter nebulaCoarseStrandColor;
SerializedDataParameter nebulaCoarseStrandScale;
SerializedDataParameter nebulaCoarseStrandScaleFactor;
SerializedDataParameter nebulaCoarseStrandDetailBalance;
SerializedDataParameter nebulaCoarseStrandOctaves;
SerializedDataParameter nebulaCoarseStrandBias;
SerializedDataParameter nebulaCoarseStrandDefinition;
SerializedDataParameter nebulaCoarseStrandSpread;
SerializedDataParameter nebulaCoarseStrandCoverage;
SerializedDataParameter nebulaCoarseStrandStrength;
SerializedDataParameter nebulaCoarseStrandWarpScale;
SerializedDataParameter nebulaCoarseStrandWarp;

SerializedDataParameter nebulaFineStrandBrightness;
SerializedDataParameter nebulaFineStrandColor;
SerializedDataParameter nebulaFineStrandScale;
SerializedDataParameter nebulaFineStrandScaleFactor;
SerializedDataParameter nebulaFineStrandDetailBalance;
SerializedDataParameter nebulaFineStrandOctaves;
SerializedDataParameter nebulaFineStrandBias;
SerializedDataParameter nebulaFineStrandDefinition;
SerializedDataParameter nebulaFineStrandSpread;
SerializedDataParameter nebulaFineStrandCoverage;
SerializedDataParameter nebulaFineStrandStrength;
SerializedDataParameter nebulaFineStrandWarpScale;
SerializedDataParameter nebulaFineStrandWarp;

SerializedDataParameter nebulaTransmittanceRange;
SerializedDataParameter nebulaTransmittanceScale;

SerializedDataParameter starNebulaFollowAmount;
SerializedDataParameter starNebulaFollowSpread;

SerializedDataParameter showNebulaeSeeds;
SerializedDataParameter nebulaCoverageSeed;
SerializedDataParameter nebulaHazeSeedX;
SerializedDataParameter nebulaHazeSeedY;
SerializedDataParameter nebulaHazeSeedZ;
SerializedDataParameter nebulaCloudSeedX;
SerializedDataParameter nebulaCloudSeedY;
SerializedDataParameter nebulaCloudSeedZ;
SerializedDataParameter nebulaCoarseStrandSeedX;
SerializedDataParameter nebulaCoarseStrandSeedY;
SerializedDataParameter nebulaCoarseStrandSeedZ;
SerializedDataParameter nebulaCoarseStrandWarpSeedX;
SerializedDataParameter nebulaCoarseStrandWarpSeedY;
SerializedDataParameter nebulaCoarseStrandWarpSeedZ;
SerializedDataParameter nebulaFineStrandSeedX;
SerializedDataParameter nebulaFineStrandSeedY;
SerializedDataParameter nebulaFineStrandSeedZ;
SerializedDataParameter nebulaFineStrandWarpSeedX;
SerializedDataParameter nebulaFineStrandWarpSeedY;
SerializedDataParameter nebulaFineStrandWarpSeedZ;
SerializedDataParameter nebulaTransmittanceSeedX;
SerializedDataParameter nebulaTransmittanceSeedY;
SerializedDataParameter nebulaTransmittanceSeedZ;

/* Regular nebulae. */
SerializedDataParameter nebulaeTexture;

/* Regular. */
SerializedDataParameter lightPollutionTint;
SerializedDataParameter lightPollutionIntensity;
SerializedDataParameter nightSkyTexture;
SerializedDataParameter nightSkyRotation;
SerializedDataParameter nightSkyTint;
SerializedDataParameter nightSkyIntensity;
SerializedDataParameter nightSkyAmbientMultiplier;
SerializedDataParameter nightSkyScatterTint;
SerializedDataParameter nightSkyScatterIntensity;
SerializedDataParameter useTwinkle;
SerializedDataParameter twinkleThreshold;
SerializedDataParameter twinkleFrequencyRange;
SerializedDataParameter twinkleBias;
SerializedDataParameter twinkleSmoothAmplitude;
SerializedDataParameter twinkleChaoticAmplitude;

/* Aerial Perspective. */
SerializedDataParameter aerialPerspectiveOcclusionPowerUniform;
SerializedDataParameter aerialPerspectiveOcclusionBiasUniform;
SerializedDataParameter aerialPerspectiveOcclusionPowerDirectional;
SerializedDataParameter aerialPerspectiveOcclusionBiasDirectional;
SerializedDataParameter aerialPerspectiveNightScatteringMultiplier;

/* Quality parameters. */
SerializedDataParameter skyTextureQuality;
SerializedDataParameter numberOfTransmittanceSamples;
SerializedDataParameter numberOfAerialPerspectiveSamples;
SerializedDataParameter numberOfSingleScatteringSamples;
SerializedDataParameter numberOfMultipleScatteringSamples;
SerializedDataParameter numberOfMultipleScatteringAccumulationSamples;
SerializedDataParameter useImportanceSampling;
SerializedDataParameter aerialPerspectiveUseImportanceSampling;
SerializedDataParameter aerialPerspectiveDepthSkew;
SerializedDataParameter useAntiAliasing;
SerializedDataParameter useDither;

/***********************/
/******* Clouds ********/
/***********************/

/* General. */
SerializedDataParameter[] cloudLayerEnabled
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];

/* Geometry. TODO */
SerializedDataParameter[] cloudGeometryType
= new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudGeometryXExtent
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudGeometryYExtent
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudGeometryZExtent
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudGeometryHeight
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];

/* Noise generation. */
SerializedDataParameter[] cloudNoiseQuality
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
/* Coverage. */
SerializedDataParameter[] cloudCoverageNoiseProcedural
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudCoverageNoiseTexture
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudCoverageNoiseType
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudCoverageGridScale
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudCoverageOctaves
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudCoverageOctaveScale
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudCoverageOctaveMultiplier
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudCoverageTile
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudCoverageIntensity
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
/* Base. */
SerializedDataParameter[] cloudBaseNoiseProcedural
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudBaseNoiseTexture2D
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudBaseNoiseTexture3D
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudBaseNoiseType
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudBaseGridScale
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudBaseOctaves
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudBaseOctaveScale
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudBaseOctaveMultiplier
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudBaseTile
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
/* Structure. */
SerializedDataParameter[] cloudStructureNoiseProcedural
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudStructureNoiseTexture2D
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudStructureNoiseTexture3D
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudStructureNoiseType
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudStructureGridScale
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudStructureOctaves
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudStructureOctaveScale
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudStructureOctaveMultiplier
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudStructureTile
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudStructureIntensity
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
/* Detail. */
SerializedDataParameter[] cloudDetailNoiseProcedural
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailNoiseTexture2D
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailNoiseTexture3D
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailNoiseType
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailGridScale
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailOctaves
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailOctaveScale
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailOctaveMultiplier
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailTile
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailIntensity
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
/* Base Warp. */
SerializedDataParameter[] cloudBaseWarpNoiseProcedural
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudBaseWarpNoiseTexture2D
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudBaseWarpNoiseTexture3D
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudBaseWarpNoiseType
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudBaseWarpGridScale
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudBaseWarpOctaves
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudBaseWarpOctaveScale
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudBaseWarpOctaveMultiplier
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudBaseWarpTile
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudBaseWarpIntensity
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
/* Detail Warp. */
SerializedDataParameter[] cloudDetailWarpNoiseProcedural
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailWarpNoiseTexture2D
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailWarpNoiseTexture3D
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailWarpNoiseType
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailWarpGridScale
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailWarpOctaves
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailWarpOctaveScale
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailWarpOctaveMultiplier
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailWarpTile
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDetailWarpIntensity
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
/* Height gradient. */
SerializedDataParameter[] cloudHeightGradientBottom
= new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudHeightGradientTop
= new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];

/* Movement---sampling offsets primarily. TODO */

/* Lighting. TODO */
/* 2D. */
SerializedDataParameter[] cloudThickness
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
/* 3D. */
SerializedDataParameter[] cloudVerticalProbabilityHeightRange
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudVerticalProbabilityStrength
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDepthProbabilityHeightRange
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDepthProbabilityStrengthRange
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDepthProbabilityDensityMultiplier
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDepthProbabilityBias
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
/* 2D and 3D. */
SerializedDataParameter[] cloudDensity
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDensityAttenuationDistance
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudDensityAttenuationBias
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudAbsorptionCoefficients
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudScatteringCoefficients
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudMSAmount
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudMSBias
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudSilverSpread
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudSilverIntensity
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];
SerializedDataParameter[] cloudAnisotropy
  = new SerializedDataParameter[ExpanseCommon.kMaxCloudLayers];

/* Sampling. TODO */
/* TODO: debug goes here. */

/******************************************************************************/
/************************** END SERIALIZED PARAMETERS *************************/
/******************************************************************************/











/******************************************************************************/
/****************************** MAIN FUNCTIONS ********************************/
/******************************************************************************/

public override void OnEnable()
{
  /* Boilerplate. */
  base.OnEnable();
  m_CommonUIElementsMask = (uint) SkySettingsUIElement.UpdateMode;

  /* Initialize all show atmosphere layers to false. */
  for (int i = 0; i < ExpanseCommon.kMaxAtmosphereLayers; i++) {
    m_showAtmosphereLayer[i] = new AnimBool(i == (int) m_atmosphereLayerSelect);
    m_showAtmosphereLayer[i].valueChanged.AddListener(Repaint);
  }

  /* Initialize all show celestial bodies to false. */
  for (int i = 0; i < ExpanseCommon.kMaxCelestialBodies; i++) {
    m_showCelestialBody[i] = new AnimBool(i == (int) m_celestialBodySelect);
    m_showCelestialBody[i].valueChanged.AddListener(Repaint);
  }

  /* Get the serialized properties from the Expanse class to
   * attach to the editor. */
  var o = new PropertyFetcher<Expanse>(serializedObject);

  /* Unpack all the serialized properties into our variables. */
  unpackSerializedProperties(o);
}

public override void OnInspectorGUI()
{
  /* Styles for the GUI. */
  UnityEngine.GUIStyle mainHeaderStyle = new UnityEngine.GUIStyle();
  mainHeaderStyle.fontSize = 18;
  mainHeaderStyle.fontStyle = FontStyle.Bold;
  mainHeaderStyle.normal.textColor = UnityEngine.Color.white;
  GUIStyle titleStyle = new GUIStyle(EditorStyles.foldoutHeader);
  titleStyle.fontSize = 14;
  titleStyle.margin = new RectOffset(0, 0, 5, 5);
  titleStyle.normal.textColor = UnityEngine.Color.white;
  GUIStyle subtitleStyle = new UnityEngine.GUIStyle(EditorStyles.foldoutHeader);
  subtitleStyle.fontSize = 12;
  subtitleStyle.normal.textColor = UnityEngine.Color.white;

  /***********************/
  /********* Sky *********/
  /***********************/
  EditorGUILayout.Space();
  EditorGUILayout.LabelField("Sky", mainHeaderStyle);
  EditorGUILayout.Space();

  planet(titleStyle, subtitleStyle);
  atmosphereLayer(titleStyle, subtitleStyle);
  celestialBody(titleStyle, subtitleStyle);
  nightSky(titleStyle, subtitleStyle);
  aerialPerspective(titleStyle, subtitleStyle);
  quality(titleStyle, subtitleStyle);
  // base.CommonSkySettingsGUI();


  /***********************/
  /******* Clouds ********/
  /***********************/
  EditorGUILayout.Space();
  EditorGUILayout.LabelField("Clouds", mainHeaderStyle);
  EditorGUILayout.Space();

  clouds(titleStyle, subtitleStyle);
}

/******************************************************************************/
/**************************** END MAIN FUNCTIONS ******************************/
/******************************************************************************/



/******************************************************************************/
/**************************** INDIVIDUAL ELEMENTS *****************************/
/******************************************************************************/

private void planet(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  planetFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(planetFoldout, "Planet", titleStyle);

  if (planetFoldout) {
    PropertyField(atmosphereThickness);
    PropertyField(planetRadius);
    PropertyField(planetOriginOffset);
    PropertyField(groundAlbedoTexture);
    PropertyField(groundTint);
    PropertyField(groundEmissionTexture);
    PropertyField(groundEmissionMultiplier);
    if (groundAlbedoTexture.value.objectReferenceValue != null
      || groundEmissionTexture.value.objectReferenceValue != null) {
      PropertyField(planetRotation);
    }
    EditorGUILayout.Space();
  }

  EditorGUILayout.EndFoldoutHeaderGroup();
}


private void atmosphereLayer(UnityEngine.GUIStyle titleStyle,
  UnityEngine.GUIStyle subtitleStyle) {
  atmosphereFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(atmosphereFoldout, "Atmosphere", titleStyle);

  if (atmosphereFoldout) {
    /* Set the atmosphere layer select. */
    m_atmosphereLayerSelect = (ExpanseCommon.AtmosphereLayer)
      EditorGUILayout.EnumPopup("Layer", m_atmosphereLayerSelect);
    int atmosphereSelectIndex = setEnumSelect(m_showAtmosphereLayer,
      (int) m_atmosphereLayerSelect);

    /* Display atmosphere params for it. */
    if (UnityEditor.EditorGUILayout.BeginFadeGroup(m_showAtmosphereLayer[atmosphereSelectIndex].faded))
    {
      PropertyField(layerEnabled[atmosphereSelectIndex], new UnityEngine.GUIContent("Enabled"));
      if (layerEnabled[atmosphereSelectIndex].value.boolValue) {
        PropertyField(layerCoefficientsA[atmosphereSelectIndex], new UnityEngine.GUIContent("Absorption Coefficients"));
        PropertyField(layerCoefficientsS[atmosphereSelectIndex], new UnityEngine.GUIContent("Scattering Coefficients"));
        PropertyField(layerDensity[atmosphereSelectIndex], new UnityEngine.GUIContent("Density"));
        /* Density distribution selection dropdown. */
        PropertyField(layerDensityDistribution[atmosphereSelectIndex], new UnityEngine.GUIContent("Density Distribution"));
        if ((ExpanseCommon.DensityDistribution) layerDensityDistribution[atmosphereSelectIndex].value.enumValueIndex == ExpanseCommon.DensityDistribution.Tent) {
          /* Only display height control if tent distribution is enabled. */
          PropertyField(layerHeight[atmosphereSelectIndex], new UnityEngine.GUIContent("Height"));
        }
        PropertyField(layerThickness[atmosphereSelectIndex], new UnityEngine.GUIContent("Thickness"));
        if ((ExpanseCommon.DensityDistribution) layerDensityDistribution[atmosphereSelectIndex].value.enumValueIndex == ExpanseCommon.DensityDistribution.ExponentialAttenuated) {
          PropertyField(layerDensityAttenuationPlayerOrigin[atmosphereSelectIndex], new UnityEngine.GUIContent("Attenuate From Camera Position"));
          if (!layerDensityAttenuationPlayerOrigin[atmosphereSelectIndex].value.boolValue) {
            PropertyField(layerDensityAttenuationOrigin[atmosphereSelectIndex], new UnityEngine.GUIContent("Attenuation Origin"));
          }
          /* Only display density attenuation parameters if we use density attenuation. */
          PropertyField(layerAttenuationDistance[atmosphereSelectIndex], new UnityEngine.GUIContent("Attenuation Distance"));
          // TODO: current integration strategy makes attenuation bias non-physical
          // PropertyField(layerAttenuationBias[atmosphereSelectIndex], new UnityEngine.GUIContent("Attenuation Bias"));
        }
        /* Phase function selection dropdown. */
        PropertyField(layerPhaseFunction[atmosphereSelectIndex], new UnityEngine.GUIContent("Phase Function"));
        if ((ExpanseCommon.PhaseFunction) layerPhaseFunction[atmosphereSelectIndex].value.enumValueIndex == ExpanseCommon.PhaseFunction.Mie) {
          /* Only display anisotropy control if Mie scattering is enabled. */
          PropertyField(layerAnisotropy[atmosphereSelectIndex], new UnityEngine.GUIContent("Anisotropy"));
        }
        PropertyField(layerTint[atmosphereSelectIndex], new UnityEngine.GUIContent("Tint"));
        PropertyField(layerMultipleScatteringMultiplier[atmosphereSelectIndex], new UnityEngine.GUIContent("Multiple Scattering Multiplier"));
      }
    }

    EditorGUILayout.EndFadeGroup();

    EditorGUILayout.Space();
  }

  EditorGUILayout.EndFoldoutHeaderGroup();
}


private void celestialBody(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  celestialBodyFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(celestialBodyFoldout, "Celestial Bodies", titleStyle);

  if (celestialBodyFoldout) {
    /* Set the celestial body select. */
    m_celestialBodySelect = (ExpanseCommon.CelestialBody) EditorGUILayout.EnumPopup("Body", m_celestialBodySelect);
    int bodySelectIndex = setEnumSelect(m_showCelestialBody, (int) m_celestialBodySelect);

    /* Display celestial body params for it. */
    if (UnityEditor.EditorGUILayout.BeginFadeGroup(m_showCelestialBody[bodySelectIndex].faded)) {
      PropertyField(bodyEnabled[bodySelectIndex], new UnityEngine.GUIContent("Enabled"));
      if (bodyEnabled[bodySelectIndex].value.boolValue) {
        PropertyField(bodyUseDateTime[bodySelectIndex], new UnityEngine.GUIContent("Date/Time Mode"));
        if (bodyUseDateTime[bodySelectIndex].value.boolValue) {
          PropertyField(bodyDateTime[bodySelectIndex], new UnityEngine.GUIContent("Date/Time"));
          PropertyField(bodyPlayerLatitudeLongitude[bodySelectIndex], new UnityEngine.GUIContent("Latitude/Longitude"));
        } else {
          PropertyField(bodyDirection[bodySelectIndex], new UnityEngine.GUIContent("Direction"));
        }
        PropertyField(bodyAngularRadius[bodySelectIndex], new UnityEngine.GUIContent("Angular Radius"));
        PropertyField(bodyDistance[bodySelectIndex], new UnityEngine.GUIContent("Distance"));

        PropertyField(bodyLightIntensity[bodySelectIndex], new UnityEngine.GUIContent("Light Intensity"));
        PropertyField(bodyUseTemperature[bodySelectIndex], new UnityEngine.GUIContent("Color Temperature"));
        if (bodyUseTemperature[bodySelectIndex].value.boolValue) {
          PropertyField(bodyLightColor[bodySelectIndex], new UnityEngine.GUIContent("Filter"));
          PropertyField(bodyLightTemperature[bodySelectIndex], new UnityEngine.GUIContent("Temperature"));
        } else {
          PropertyField(bodyLightColor[bodySelectIndex], new UnityEngine.GUIContent("Color"));
        }

        PropertyField(bodyReceivesLight[bodySelectIndex], new UnityEngine.GUIContent("Receives Light"));
        if (bodyReceivesLight[bodySelectIndex].value.boolValue) {
          PropertyField(bodyAlbedoTexture[bodySelectIndex], new UnityEngine.GUIContent("Albedo Texture"));
          if (bodyAlbedoTexture[bodySelectIndex].value.objectReferenceValue != null) {
            PropertyField(bodyAlbedoTextureRotation[bodySelectIndex], new UnityEngine.GUIContent("Albedo Texture Rotation"));
          }
          PropertyField(bodyAlbedoTint[bodySelectIndex], new UnityEngine.GUIContent("Albedo Tint"));
        }

        PropertyField(bodyEmissive[bodySelectIndex], new UnityEngine.GUIContent("Emissive"));
        if (bodyEmissive[bodySelectIndex].value.boolValue) {
          PropertyField(bodyLimbDarkening[bodySelectIndex], new UnityEngine.GUIContent("Limb Darkening"));
          PropertyField(bodyEmissionTexture[bodySelectIndex], new UnityEngine.GUIContent("Emission Texture"));
          if (bodyEmissionTexture[bodySelectIndex].value.objectReferenceValue != null) {
            PropertyField(bodyEmissionTextureRotation[bodySelectIndex], new UnityEngine.GUIContent("Emission Texture Rotation"));
          }
          PropertyField(bodyEmissionTint[bodySelectIndex], new UnityEngine.GUIContent("Emission Tint"));
          PropertyField(bodyEmissionMultiplier[bodySelectIndex], new UnityEngine.GUIContent("Emission Multiplier"));
        }
      }
    }

    EditorGUILayout.EndFadeGroup();
    EditorGUILayout.Space();
  }

  EditorGUILayout.EndFoldoutHeaderGroup();
}


private void nightSky(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  nightSkyFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(nightSkyFoldout, "Night Sky", titleStyle);
  if (nightSkyFoldout) {

    PropertyField(useProceduralNightSky, new UnityEngine.GUIContent("Procedural Mode"));
    EditorGUILayout.Space();

    /* Procedural sky controls. */
    if (useProceduralNightSky.value.boolValue) {
      /* End the first main foldout group. */
      EditorGUILayout.EndFoldoutHeaderGroup();
      /* Create an indented style for the "nested" foldout groups. */
      GUIStyle indented = new GUIStyle(subtitleStyle);
      indented.margin = new RectOffset(30, 0, 0, 0);

      /* Sky and stars. */
      nightSkySkyFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(nightSkySkyFoldout, "Sky", indented);
      EditorGUI.indentLevel++;
      if (nightSkySkyFoldout) {
        PropertyField(nightSkyRotation, new UnityEngine.GUIContent("Rotation"));
        PropertyField(nightSkyIntensity, new UnityEngine.GUIContent("Intensity"));
        PropertyField(nightSkyTint, new UnityEngine.GUIContent("Tint"));
        PropertyField(nightSkyScatterIntensity, new UnityEngine.GUIContent("Scatter Intensity"));
        PropertyField(nightSkyScatterTint, new UnityEngine.GUIContent("Scatter Tint"));
        PropertyField(nightSkyAmbientMultiplier, new UnityEngine.GUIContent("Ambient Multiplier"));
        EditorGUILayout.Space();
      }
      EditorGUI.indentLevel--;
      EditorGUILayout.EndFoldoutHeaderGroup();

      /* Light Pollution. */
      nightSkyLightPollutionFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(nightSkyLightPollutionFoldout, "Light Pollution", indented);
      EditorGUI.indentLevel++;
      if (nightSkyLightPollutionFoldout) {
        PropertyField(lightPollutionTint, new UnityEngine.GUIContent("Tint"));
        PropertyField(lightPollutionIntensity, new UnityEngine.GUIContent("Intensity"));
        EditorGUILayout.Space();
      }
      EditorGUI.indentLevel--;
      EditorGUILayout.EndFoldoutHeaderGroup();

      /* Stars. */
      nightSkyStarsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(nightSkyStarsFoldout, "Stars", indented);
      EditorGUI.indentLevel++;
      if (nightSkyStarsFoldout) {
        PropertyField(starTextureQuality, new UnityEngine.GUIContent("Texture Quality"));
        PropertyField(showStarSeeds, new UnityEngine.GUIContent("Show Seeds"));
        if (showStarSeeds.value.boolValue) {
          PropertyField(useHighDensityMode);
          PropertyField(starDensity, new UnityEngine.GUIContent("Density"));
          PropertyField(starDensitySeed, new UnityEngine.GUIContent("Density Seed"));
          PropertyField(starSizeRange, new UnityEngine.GUIContent("Size Range"));
          PropertyField(starSizeBias, new UnityEngine.GUIContent("Size Bias"));
          PropertyField(starSizeSeed, new UnityEngine.GUIContent("Size Seed"));
          PropertyField(starIntensityRange, new UnityEngine.GUIContent("Intensity Range"));
          PropertyField(starIntensityBias, new UnityEngine.GUIContent("Intensity Bias"));
          PropertyField(starIntensitySeed, new UnityEngine.GUIContent("Intensity Seed"));
          PropertyField(starTemperatureRange, new UnityEngine.GUIContent("Temperature Range"));
          PropertyField(starTemperatureBias, new UnityEngine.GUIContent("Temperature Bias"));
          PropertyField(starTemperatureSeed, new UnityEngine.GUIContent("Temperature Seed"));
          PropertyField(starTint, new UnityEngine.GUIContent("Tint"));
        } else {
          PropertyField(useHighDensityMode);
          PropertyField(starDensity, new UnityEngine.GUIContent("Density"));
          PropertyField(starSizeRange, new UnityEngine.GUIContent("Size Range"));
          PropertyField(starSizeBias, new UnityEngine.GUIContent("Size Bias"));
          PropertyField(starIntensityRange, new UnityEngine.GUIContent("Intensity Range"));
          PropertyField(starIntensityBias, new UnityEngine.GUIContent("Intensity Bias"));
          PropertyField(starTemperatureRange, new UnityEngine.GUIContent("Temperature Range"));
          PropertyField(starTemperatureBias, new UnityEngine.GUIContent("Temperature Bias"));
          PropertyField(starTint, new UnityEngine.GUIContent("Tint"));
        }
        EditorGUILayout.Space();
      }
      EditorGUI.indentLevel--;
      EditorGUILayout.EndFoldoutHeaderGroup();

      /* Twinkle. */
      nightSkyTwinkleFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(nightSkyTwinkleFoldout, "Star Twinkle", indented);
      EditorGUI.indentLevel++;
      if (nightSkyTwinkleFoldout) {
        PropertyField(useTwinkle);
        if (useTwinkle.value.boolValue) {
          PropertyField(twinkleThreshold, new UnityEngine.GUIContent("Threshold"));
          PropertyField(twinkleFrequencyRange, new UnityEngine.GUIContent("Frequency Range"));
          PropertyField(twinkleBias, new UnityEngine.GUIContent("Bias"));
          PropertyField(twinkleSmoothAmplitude, new UnityEngine.GUIContent("Smooth Intensity"));
          PropertyField(twinkleChaoticAmplitude, new UnityEngine.GUIContent("Chaotic Intensity"));
        }
        EditorGUILayout.Space();
      }
      EditorGUI.indentLevel--;
      EditorGUILayout.EndFoldoutHeaderGroup();

      /* Nebulae. */
      nightSkyNebulaeFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(nightSkyNebulaeFoldout, "Nebulae", indented);
      EditorGUI.indentLevel++;
      if (nightSkyNebulaeFoldout) {
        PropertyField(useProceduralNebulae, new UnityEngine.GUIContent("Procedural"));
        if (useProceduralNebulae.value.boolValue) {
          /* End the nebulae foldout group. */
          EditorGUILayout.EndFoldoutHeaderGroup();
          EditorGUILayout.Space();
          /* Create an indented style for the "nested" nebulae foldout groups. */
          GUIStyle nebulaeIndented = new GUIStyle(subtitleStyle);
          nebulaeIndented.margin = new RectOffset(60, 0, 0, 0);

          nightSkyNebulaeGeneralFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(nightSkyNebulaeGeneralFoldout, "General", nebulaeIndented);
          EditorGUI.indentLevel++;
          if (nightSkyNebulaeGeneralFoldout) {
            PropertyField(nebulaeTextureQuality, new UnityEngine.GUIContent("Texture Quality"));
            PropertyField(showNebulaeSeeds, new UnityEngine.GUIContent("Show Seeds"));
            PropertyField(nebulaOverallDefinition, new UnityEngine.GUIContent("Overall Definition"));
            PropertyField(nebulaOverallIntensity, new UnityEngine.GUIContent("Overall Intensity"));
            PropertyField(starNebulaFollowAmount, new UnityEngine.GUIContent("Star Follow Amount"));
            PropertyField(starNebulaFollowSpread, new UnityEngine.GUIContent("Star Follow Spread"));
            PropertyField(nebulaCoverageScale, new UnityEngine.GUIContent("Coverage Scale"));
            if (showNebulaeSeeds.value.boolValue) {
              PropertyField(nebulaCoverageSeed, new UnityEngine.GUIContent("Coverage Seed"));
            }
            PropertyField(nebulaTransmittanceRange, new UnityEngine.GUIContent("Transmittance Range"));
            PropertyField(nebulaTransmittanceScale, new UnityEngine.GUIContent("Transmittance Scale"));
            if (showNebulaeSeeds.value.boolValue) {
              PropertyField(nebulaTransmittanceSeedX, new UnityEngine.GUIContent("Transmittance X Seed"));
              PropertyField(nebulaTransmittanceSeedY, new UnityEngine.GUIContent("Transmittance Y Seed"));
              PropertyField(nebulaTransmittanceSeedZ, new UnityEngine.GUIContent("Transmittance Z Seed"));
            }
            EditorGUILayout.Space();
          }
          EditorGUI.indentLevel--;
          EditorGUILayout.EndFoldoutHeaderGroup();
          nightSkyNebulaeLayerEditorFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(nightSkyNebulaeLayerEditorFoldout, "Layer Editor", nebulaeIndented);
          EditorGUI.indentLevel++;
          if (nightSkyNebulaeLayerEditorFoldout) {
            EditorGUILayout.Space();
            GUIStyle nebulaeLayerDropdownStyle = new GUIStyle(EditorStyles.popup);
            nebulaeLayerDropdownStyle.margin = new RectOffset(38, 0, 0, 0);
            nebulaLayerDropdownSelection = EditorGUILayout.Popup("Nebula Layer", nebulaLayerDropdownSelection, nebulaLayerDropdownOptions, nebulaeLayerDropdownStyle);
            switch (nebulaLayerDropdownSelection) {
              case 0: {
                PropertyField(nebulaHazeBrightness, new UnityEngine.GUIContent("Haze Intensity"));
                PropertyField(nebulaHazeColor, new UnityEngine.GUIContent("Haze Color"));
                PropertyField(nebulaHazeScale, new UnityEngine.GUIContent("Haze Scale"));
                PropertyField(nebulaHazeScaleFactor, new UnityEngine.GUIContent("Haze Detail"));
                PropertyField(nebulaHazeDetailBalance, new UnityEngine.GUIContent("Haze Detail Balance"));
                PropertyField(nebulaHazeOctaves, new UnityEngine.GUIContent("Haze Octaves"));
                PropertyField(nebulaHazeBias, new UnityEngine.GUIContent("Haze Bias"));
                PropertyField(nebulaHazeStrength, new UnityEngine.GUIContent("Haze Strength"));
                PropertyField(nebulaHazeCoverage, new UnityEngine.GUIContent("Haze Coverage"));
                PropertyField(nebulaHazeSpread, new UnityEngine.GUIContent("Haze Spread"));
                if (showNebulaeSeeds.value.boolValue) {
                  PropertyField(nebulaHazeSeedX, new UnityEngine.GUIContent("Haze X Seed"));
                  PropertyField(nebulaHazeSeedY, new UnityEngine.GUIContent("Haze Y Seed"));
                  PropertyField(nebulaHazeSeedZ, new UnityEngine.GUIContent("Haze Z Seed"));
                }
                break;
              }
              case 1: {
                PropertyField(nebulaCloudBrightness, new UnityEngine.GUIContent("Cloud Intensity"));
                PropertyField(nebulaCloudColor, new UnityEngine.GUIContent("Cloud Color"));
                PropertyField(nebulaCloudScale, new UnityEngine.GUIContent("Cloud Scale"));
                PropertyField(nebulaCloudScaleFactor, new UnityEngine.GUIContent("Cloud Detail"));
                PropertyField(nebulaCloudDetailBalance, new UnityEngine.GUIContent("Cloud Detail Balance"));
                PropertyField(nebulaCloudOctaves, new UnityEngine.GUIContent("Cloud Octaves"));
                PropertyField(nebulaCloudBias, new UnityEngine.GUIContent("Cloud Bias"));
                PropertyField(nebulaCloudStrength, new UnityEngine.GUIContent("Cloud Strength"));
                PropertyField(nebulaCloudCoverage, new UnityEngine.GUIContent("Cloud Coverage"));
                PropertyField(nebulaCloudSpread, new UnityEngine.GUIContent("Cloud Spread"));
                if (showNebulaeSeeds.value.boolValue) {
                  PropertyField(nebulaCloudSeedX, new UnityEngine.GUIContent("Cloud X Seed"));
                  PropertyField(nebulaCloudSeedY, new UnityEngine.GUIContent("Cloud Y Seed"));
                  PropertyField(nebulaCloudSeedZ, new UnityEngine.GUIContent("Cloud Z Seed"));
                }
                break;
              }
              case 2: {
                PropertyField(nebulaCoarseStrandBrightness, new UnityEngine.GUIContent("Big Strand Intensity"));
                PropertyField(nebulaCoarseStrandColor, new UnityEngine.GUIContent("Big Strand Color"));
                PropertyField(nebulaCoarseStrandScale, new UnityEngine.GUIContent("Big Strand Scale"));
                PropertyField(nebulaCoarseStrandScaleFactor, new UnityEngine.GUIContent("Big Strand Detail"));
                PropertyField(nebulaCoarseStrandDetailBalance, new UnityEngine.GUIContent("Big Strand Detail Balance"));
                PropertyField(nebulaCoarseStrandOctaves, new UnityEngine.GUIContent("Big Strand Octaves"));
                PropertyField(nebulaCoarseStrandBias, new UnityEngine.GUIContent("Big Strand Bias"));
                PropertyField(nebulaCoarseStrandStrength, new UnityEngine.GUIContent("Big Strand Strength"));
                PropertyField(nebulaCoarseStrandDefinition, new UnityEngine.GUIContent("Big Strand Definition"));
                PropertyField(nebulaCoarseStrandCoverage, new UnityEngine.GUIContent("Big Strand Coverage"));
                PropertyField(nebulaCoarseStrandSpread, new UnityEngine.GUIContent("Big Strand Spread"));
                PropertyField(nebulaCoarseStrandWarp, new UnityEngine.GUIContent("Big Strand Warp"));
                PropertyField(nebulaCoarseStrandWarpScale, new UnityEngine.GUIContent("Big Strand Warp Scale"));
                if (showNebulaeSeeds.value.boolValue) {
                  PropertyField(nebulaCoarseStrandSeedX, new UnityEngine.GUIContent("Big Strand X Seed"));
                  PropertyField(nebulaCoarseStrandSeedY, new UnityEngine.GUIContent("Big Strand Y Seed"));
                  PropertyField(nebulaCoarseStrandSeedZ, new UnityEngine.GUIContent("Big Strand Z Seed"));
                  PropertyField(nebulaCoarseStrandWarpSeedX, new UnityEngine.GUIContent("Big Strand Warp X Seed"));
                  PropertyField(nebulaCoarseStrandWarpSeedY, new UnityEngine.GUIContent("Big Strand Warp Y Seed"));
                  PropertyField(nebulaCoarseStrandWarpSeedZ, new UnityEngine.GUIContent("Big Strand Warp Z Seed"));
                }
                break;
              }
              case 3: {
                PropertyField(nebulaFineStrandBrightness, new UnityEngine.GUIContent("Small Strand Intensity"));
                PropertyField(nebulaFineStrandColor, new UnityEngine.GUIContent("Small Strand Color"));
                PropertyField(nebulaFineStrandScale, new UnityEngine.GUIContent("Small Strand Scale"));
                PropertyField(nebulaFineStrandScaleFactor, new UnityEngine.GUIContent("Small Strand Detail"));
                PropertyField(nebulaFineStrandDetailBalance, new UnityEngine.GUIContent("Small Strand Detail Balance"));
                PropertyField(nebulaFineStrandOctaves, new UnityEngine.GUIContent("Small Strand Octaves"));
                PropertyField(nebulaFineStrandBias, new UnityEngine.GUIContent("Small Strand Bias"));
                PropertyField(nebulaFineStrandStrength, new UnityEngine.GUIContent("Small Strand Strength"));
                PropertyField(nebulaFineStrandDefinition, new UnityEngine.GUIContent("Small Strand Definition"));
                PropertyField(nebulaFineStrandCoverage, new UnityEngine.GUIContent("Small Strand Coverage"));
                PropertyField(nebulaFineStrandSpread, new UnityEngine.GUIContent("Small Strand Spread"));
                PropertyField(nebulaFineStrandWarp, new UnityEngine.GUIContent("Small Strand Warp"));
                PropertyField(nebulaFineStrandWarpScale, new UnityEngine.GUIContent("Small Strand Warp Scale"));
                if (showNebulaeSeeds.value.boolValue) {
                  PropertyField(nebulaFineStrandSeedX, new UnityEngine.GUIContent("Small Strand X Seed"));
                  PropertyField(nebulaFineStrandSeedY, new UnityEngine.GUIContent("Small Strand Y Seed"));
                  PropertyField(nebulaFineStrandSeedZ, new UnityEngine.GUIContent("Small Strand Z Seed"));
                  PropertyField(nebulaFineStrandWarpSeedX, new UnityEngine.GUIContent("Small Strand Warp X Seed"));
                  PropertyField(nebulaFineStrandWarpSeedY, new UnityEngine.GUIContent("Small Strand Warp Y Seed"));
                  PropertyField(nebulaFineStrandWarpSeedZ, new UnityEngine.GUIContent("Small Strand Warp Z Seed"));
                }
                break;
              }
              default: {
                /* Show an error. */
                EditorGUILayout.LabelField("ERROR: Invalid nebula layer selected. If you're a user, this error is not your fault.", subtitleStyle);
                break;
              }
            }
            EditorGUILayout.Space();
          }
          EditorGUI.indentLevel--;
          EditorGUILayout.EndFoldoutHeaderGroup();
        }
        else {
          /* Nebulae is a texture. */
          PropertyField(nebulaeTexture, new UnityEngine.GUIContent("Nebulae Texture"));
          PropertyField(nebulaOverallIntensity, new UnityEngine.GUIContent("Intensity"));
          PropertyField(starNebulaFollowAmount, new UnityEngine.GUIContent("Star Follow Amount"));
          PropertyField(starNebulaFollowSpread, new UnityEngine.GUIContent("Star Follow Spread"));
        }
        EditorGUILayout.Space();
      }
      EditorGUI.indentLevel--;
      EditorGUILayout.EndFoldoutHeaderGroup();

        // EditorGUILayout.Space();

    }

    /* Texture sky controls. */
    else {

      /* End the first main foldout group. */
      EditorGUILayout.EndFoldoutHeaderGroup();
      /* Create an indented style for the "nested" foldout groups. */
      GUIStyle indented = new GUIStyle(EditorStyles.foldoutHeader);
      indented.margin = new RectOffset(30, 0, 0, 0);

      /* Sky and stars. */
      nightSkySkyFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(nightSkySkyFoldout, "Sky and Stars", indented);
      EditorGUI.indentLevel++;
      if (nightSkySkyFoldout) {
        PropertyField(nightSkyTexture, new UnityEngine.GUIContent("Star Texture"));
        if (nightSkyTexture.value.objectReferenceValue != null) {
          PropertyField(nightSkyRotation, new UnityEngine.GUIContent("Rotation"));
        }
        PropertyField(nightSkyIntensity, new UnityEngine.GUIContent("Intensity"));
        PropertyField(nightSkyAmbientMultiplier, new UnityEngine.GUIContent("Ambient Multiplier"));
        PropertyField(nightSkyTint, new UnityEngine.GUIContent("Tint"));
        PropertyField(nightSkyScatterIntensity, new UnityEngine.GUIContent("Scatter Intensity"));
        PropertyField(nightSkyScatterTint, new UnityEngine.GUIContent("Scatter Tint"));
        EditorGUILayout.Space();
      }
      EditorGUI.indentLevel--;
      EditorGUILayout.EndFoldoutHeaderGroup();

      /* Twinkle. */
      nightSkyTwinkleFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(nightSkyTwinkleFoldout, "Star Twinkle", indented);
      EditorGUI.indentLevel++;
      if (nightSkyTwinkleFoldout) {
        PropertyField(useTwinkle, new UnityEngine.GUIContent("Star Twinkle"));
        if (useTwinkle.value.boolValue) {
          PropertyField(twinkleThreshold, new UnityEngine.GUIContent("Threshold"));
          PropertyField(twinkleFrequencyRange, new UnityEngine.GUIContent("Frequency Range"));
          PropertyField(twinkleBias, new UnityEngine.GUIContent("Bias"));
          PropertyField(twinkleSmoothAmplitude, new UnityEngine.GUIContent("Smooth Intensity"));
          PropertyField(twinkleChaoticAmplitude, new UnityEngine.GUIContent("Chaotic Intensity"));
        }
        EditorGUILayout.Space();
      }
      EditorGUI.indentLevel--;
      EditorGUILayout.EndFoldoutHeaderGroup();

      /* Light Pollution. */
      nightSkyLightPollutionFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(nightSkyLightPollutionFoldout, "Light Pollution", indented);
      EditorGUI.indentLevel++;
      if (nightSkyLightPollutionFoldout) {
        PropertyField(lightPollutionTint, new UnityEngine.GUIContent("Tint"));
        PropertyField(lightPollutionIntensity, new UnityEngine.GUIContent("Intensity"));
        EditorGUILayout.Space();
      }
      EditorGUI.indentLevel--;
      EditorGUILayout.EndFoldoutHeaderGroup();

    }

    EditorGUILayout.Space();
  }
  else {
    EditorGUILayout.EndFoldoutHeaderGroup();
  }
}


private void aerialPerspective(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  aerialPerspectiveFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(aerialPerspectiveFoldout, "Aerial Perspective", titleStyle);

  if (aerialPerspectiveFoldout) {
    PropertyField(aerialPerspectiveOcclusionPowerUniform, new UnityEngine.GUIContent("Uniform Occlusion Spread"));
    PropertyField(aerialPerspectiveOcclusionBiasUniform, new UnityEngine.GUIContent("Uniform Occlusion Bias"));
    PropertyField(aerialPerspectiveOcclusionPowerDirectional, new UnityEngine.GUIContent("Directional Occlusion Spread"));
    PropertyField(aerialPerspectiveOcclusionBiasDirectional, new UnityEngine.GUIContent("Directional Occlusion Bias"));
    PropertyField(aerialPerspectiveNightScatteringMultiplier, new UnityEngine.GUIContent("Night Scattering Multiplier"));
    EditorGUILayout.Space();
  }

  EditorGUILayout.EndFoldoutHeaderGroup();
}


private void quality(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  qualityFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(qualityFoldout, "Quality", titleStyle);

  if (qualityFoldout) {
    PropertyField(skyTextureQuality, new UnityEngine.GUIContent("Texture Quality"));
    PropertyField(numberOfTransmittanceSamples, new UnityEngine.GUIContent("Transmittance Samples"));
    PropertyField(numberOfSingleScatteringSamples, new UnityEngine.GUIContent("Single Scattering Samples"));
    PropertyField(numberOfMultipleScatteringSamples, new UnityEngine.GUIContent("Multiple Scattering Samples"));
    PropertyField(numberOfMultipleScatteringAccumulationSamples, new UnityEngine.GUIContent("Multiple Scattering Accumulation Samples"));
    PropertyField(numberOfAerialPerspectiveSamples, new UnityEngine.GUIContent("Aerial Perspective Samples"));
    PropertyField(useImportanceSampling, new UnityEngine.GUIContent("Importance Sampling"));
    PropertyField(aerialPerspectiveUseImportanceSampling, new UnityEngine.GUIContent("Aerial Perspective Importance Sampling"));
    PropertyField(aerialPerspectiveDepthSkew, new UnityEngine.GUIContent("Aerial Perspective Depth Skew"));
    PropertyField(useAntiAliasing, new UnityEngine.GUIContent("Anti-Aliasing"));
    PropertyField(useDither, new UnityEngine.GUIContent("Dithering"));
    EditorGUILayout.Space();
  }

  EditorGUILayout.EndFoldoutHeaderGroup();
}

private void clouds(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  m_cloudLayerSelect = (ExpanseCommon.CloudLayer) EditorGUILayout.EnumPopup("Layer", m_cloudLayerSelect);
  int layerSelectIndex = setEnumSelect(m_showCloudLayer, (int) m_cloudLayerSelect);

  /* Display celestial body params for it. */
  if (UnityEditor.EditorGUILayout.BeginFadeGroup(m_showCloudLayer[layerSelectIndex].faded)) {
    /* For some reason there's an indent here. Use -- to get rid of it. */

    PropertyField(cloudLayerEnabled[layerSelectIndex], new UnityEngine.GUIContent("Enabled"));
    if (cloudLayerEnabled[layerSelectIndex].value.boolValue) {
      EditorGUILayout.Space();
      cloudGeometry(titleStyle, subtitleStyle, layerSelectIndex);
      cloudNoise(titleStyle, subtitleStyle, layerSelectIndex);
      cloudMovement(titleStyle, subtitleStyle, layerSelectIndex);
      cloudLighting(titleStyle, subtitleStyle, layerSelectIndex);
      cloudSampling(titleStyle, subtitleStyle, layerSelectIndex);
    } else {
      EditorGUILayout.Space();
    }
  }

  EditorGUILayout.EndFadeGroup();
}

private void cloudGeometry(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle, int layerIndex) {
  cloudGeometryFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(cloudGeometryFoldout, "Geometry", titleStyle);

  if (cloudGeometryFoldout) {
    PropertyField(cloudGeometryType[layerIndex], new UnityEngine.GUIContent("Geometry Type"));

    if ((ExpanseCommon.CloudGeometryType) cloudGeometryType[layerIndex].value.enumValueIndex == ExpanseCommon.CloudGeometryType.BoxVolume) {
      PropertyField(cloudGeometryXExtent[layerIndex], new UnityEngine.GUIContent("X Extent"));
      PropertyField(cloudGeometryYExtent[layerIndex], new UnityEngine.GUIContent("Y Extent"));
      PropertyField(cloudGeometryZExtent[layerIndex], new UnityEngine.GUIContent("Z Extent"));
    } else if ((ExpanseCommon.CloudGeometryType) cloudGeometryType[layerIndex].value.enumValueIndex == ExpanseCommon.CloudGeometryType.Plane) {
      PropertyField(cloudGeometryXExtent[layerIndex], new UnityEngine.GUIContent("X Extent"));
      PropertyField(cloudGeometryZExtent[layerIndex], new UnityEngine.GUIContent("Z Extent"));
      PropertyField(cloudGeometryHeight[layerIndex], new UnityEngine.GUIContent("Height"));
    } else {
      PropertyField(cloudGeometryHeight[layerIndex], new UnityEngine.GUIContent("Height"));
    }

    EditorGUILayout.Space();
  }

  EditorGUILayout.EndFoldoutHeaderGroup();
}

private void cloudNoise(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle, int layerIndex) {
  cloudNoiseFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(cloudNoiseFoldout, "Noise", titleStyle);

  if (cloudNoiseFoldout) {
    /* Display the quality field, which applies to all noise textures. */
    PropertyField(cloudNoiseQuality[layerIndex], new UnityEngine.GUIContent("Quality"));
    EditorGUILayout.Space();

    /* Display the remap parameters. */
    PropertyField(cloudCoverageIntensity[layerIndex], new UnityEngine.GUIContent("Coverage"));
    PropertyField(cloudStructureIntensity[layerIndex], new UnityEngine.GUIContent("Structure"));
    PropertyField(cloudDetailIntensity[layerIndex], new UnityEngine.GUIContent("Detail"));
    PropertyField(cloudBaseWarpIntensity[layerIndex], new UnityEngine.GUIContent("Base Warp"));
    PropertyField(cloudDetailWarpIntensity[layerIndex], new UnityEngine.GUIContent("Detail Warp"));
    PropertyField(cloudHeightGradientBottom[layerIndex], new UnityEngine.GUIContent("Bottom Height Gradient"));
    PropertyField(cloudHeightGradientTop[layerIndex], new UnityEngine.GUIContent("Top Height Gradient"));
    EditorGUILayout.Space();

    m_cloudNoiseLayerSelect = (ExpanseCommon.CloudNoiseLayer) EditorGUILayout.EnumPopup("Noise Layer", m_cloudNoiseLayerSelect);
    int layerSelectIndex = setEnumSelect(m_showCloudNoiseLayer, (int) m_cloudNoiseLayerSelect);

    /* Display celestial body params for it. */
    if (UnityEditor.EditorGUILayout.BeginFadeGroup(m_showCloudNoiseLayer[layerSelectIndex].faded)) {
      switch (m_cloudNoiseLayerSelect) {

        case ExpanseCommon.CloudNoiseLayer.Coverage: {
          PropertyField(cloudCoverageNoiseProcedural[layerIndex], new UnityEngine.GUIContent("Procedural"));
          if (cloudCoverageNoiseProcedural[layerIndex].value.boolValue) {
            PropertyField(cloudCoverageNoiseType[layerIndex], new UnityEngine.GUIContent("Noise Type"));
            PropertyField(cloudCoverageGridScale[layerIndex], new UnityEngine.GUIContent("Scale"));
            PropertyField(cloudCoverageOctaves[layerIndex], new UnityEngine.GUIContent("Octaves"));
            PropertyField(cloudCoverageOctaveScale[layerIndex], new UnityEngine.GUIContent("Octave Scale"));
            PropertyField(cloudCoverageOctaveMultiplier[layerIndex], new UnityEngine.GUIContent("Octave Balance"));
          } else {
            PropertyField(cloudCoverageNoiseTexture[layerIndex], new UnityEngine.GUIContent("Noise Texture"));
          }
          PropertyField(cloudCoverageTile[layerIndex], new UnityEngine.GUIContent("Tile"));
          break;
        }

        case ExpanseCommon.CloudNoiseLayer.Base: {
          PropertyField(cloudBaseNoiseProcedural[layerIndex], new UnityEngine.GUIContent("Procedural"));
          if (cloudBaseNoiseProcedural[layerIndex].value.boolValue) {
            PropertyField(cloudBaseNoiseType[layerIndex], new UnityEngine.GUIContent("Noise Type"));
            PropertyField(cloudBaseGridScale[layerIndex], new UnityEngine.GUIContent("Scale"));
            PropertyField(cloudBaseOctaves[layerIndex], new UnityEngine.GUIContent("Octaves"));
            PropertyField(cloudBaseOctaveScale[layerIndex], new UnityEngine.GUIContent("Octave Scale"));
            PropertyField(cloudBaseOctaveMultiplier[layerIndex], new UnityEngine.GUIContent("Octave Balance"));
          } else {
            if ((ExpanseCommon.CloudGeometryType) cloudGeometryType[layerIndex].value.enumValueIndex == ExpanseCommon.CloudGeometryType.Plane) {
              PropertyField(cloudBaseNoiseTexture2D[layerIndex], new UnityEngine.GUIContent("Noise Texture"));
            } else if ((ExpanseCommon.CloudGeometryType) cloudGeometryType[layerIndex].value.enumValueIndex == ExpanseCommon.CloudGeometryType.BoxVolume) {
              PropertyField(cloudBaseNoiseTexture3D[layerIndex], new UnityEngine.GUIContent("Noise Texture"));
            }
          }
          PropertyField(cloudBaseTile[layerIndex], new UnityEngine.GUIContent("Tile"));
          break;
        }

        case ExpanseCommon.CloudNoiseLayer.Structure: {
          PropertyField(cloudStructureNoiseProcedural[layerIndex], new UnityEngine.GUIContent("Procedural"));
          if (cloudStructureNoiseProcedural[layerIndex].value.boolValue) {
            PropertyField(cloudStructureNoiseType[layerIndex], new UnityEngine.GUIContent("Noise Type"));
            PropertyField(cloudStructureGridScale[layerIndex], new UnityEngine.GUIContent("Scale"));
            PropertyField(cloudStructureOctaves[layerIndex], new UnityEngine.GUIContent("Octaves"));
            PropertyField(cloudStructureOctaveScale[layerIndex], new UnityEngine.GUIContent("Octave Scale"));
            PropertyField(cloudStructureOctaveMultiplier[layerIndex], new UnityEngine.GUIContent("Octave Balance"));
          } else {
            if ((ExpanseCommon.CloudGeometryType) cloudGeometryType[layerIndex].value.enumValueIndex == ExpanseCommon.CloudGeometryType.Plane) {
              PropertyField(cloudStructureNoiseTexture2D[layerIndex], new UnityEngine.GUIContent("Noise Texture"));
            } else if ((ExpanseCommon.CloudGeometryType) cloudGeometryType[layerIndex].value.enumValueIndex == ExpanseCommon.CloudGeometryType.BoxVolume) {
              PropertyField(cloudStructureNoiseTexture3D[layerIndex], new UnityEngine.GUIContent("Noise Texture"));
            }
          }
          PropertyField(cloudStructureTile[layerIndex], new UnityEngine.GUIContent("Tile"));
          break;
        }

        case ExpanseCommon.CloudNoiseLayer.Detail: {
          PropertyField(cloudDetailNoiseProcedural[layerIndex], new UnityEngine.GUIContent("Procedural"));
          if (cloudDetailNoiseProcedural[layerIndex].value.boolValue) {
            PropertyField(cloudDetailNoiseType[layerIndex], new UnityEngine.GUIContent("Noise Type"));
            PropertyField(cloudDetailGridScale[layerIndex], new UnityEngine.GUIContent("Scale"));
            PropertyField(cloudDetailOctaves[layerIndex], new UnityEngine.GUIContent("Octaves"));
            PropertyField(cloudDetailOctaveScale[layerIndex], new UnityEngine.GUIContent("Octave Scale"));
            PropertyField(cloudDetailOctaveMultiplier[layerIndex], new UnityEngine.GUIContent("Octave Balance"));
          } else {
            if ((ExpanseCommon.CloudGeometryType) cloudGeometryType[layerIndex].value.enumValueIndex == ExpanseCommon.CloudGeometryType.Plane) {
              PropertyField(cloudDetailNoiseTexture2D[layerIndex], new UnityEngine.GUIContent("Noise Texture"));
            } else if ((ExpanseCommon.CloudGeometryType) cloudGeometryType[layerIndex].value.enumValueIndex == ExpanseCommon.CloudGeometryType.BoxVolume) {
              PropertyField(cloudDetailNoiseTexture3D[layerIndex], new UnityEngine.GUIContent("Noise Texture"));
            }
          }
          PropertyField(cloudDetailTile[layerIndex], new UnityEngine.GUIContent("Tile"));
          break;
        }

        case ExpanseCommon.CloudNoiseLayer.BaseWarp: {
          PropertyField(cloudBaseWarpNoiseProcedural[layerIndex], new UnityEngine.GUIContent("Procedural"));
          if (cloudBaseWarpNoiseProcedural[layerIndex].value.boolValue) {
            PropertyField(cloudBaseWarpNoiseType[layerIndex], new UnityEngine.GUIContent("Noise Type"));
            PropertyField(cloudBaseWarpGridScale[layerIndex], new UnityEngine.GUIContent("Scale"));
            PropertyField(cloudBaseWarpOctaves[layerIndex], new UnityEngine.GUIContent("Octaves"));
            PropertyField(cloudBaseWarpOctaveScale[layerIndex], new UnityEngine.GUIContent("Octave Scale"));
            PropertyField(cloudBaseWarpOctaveMultiplier[layerIndex], new UnityEngine.GUIContent("Octave Balance"));
          } else {
            if ((ExpanseCommon.CloudGeometryType) cloudGeometryType[layerIndex].value.enumValueIndex == ExpanseCommon.CloudGeometryType.Plane) {
              PropertyField(cloudBaseWarpNoiseTexture2D[layerIndex], new UnityEngine.GUIContent("Noise Texture"));
            } else if ((ExpanseCommon.CloudGeometryType) cloudGeometryType[layerIndex].value.enumValueIndex == ExpanseCommon.CloudGeometryType.BoxVolume) {
              PropertyField(cloudBaseWarpNoiseTexture3D[layerIndex], new UnityEngine.GUIContent("Noise Texture"));
            }
          }
          PropertyField(cloudBaseWarpTile[layerIndex], new UnityEngine.GUIContent("Tile"));
          break;
        }

        case ExpanseCommon.CloudNoiseLayer.DetailWarp: {
          PropertyField(cloudDetailWarpNoiseProcedural[layerIndex], new UnityEngine.GUIContent("Procedural"));
          if (cloudDetailWarpNoiseProcedural[layerIndex].value.boolValue) {
            PropertyField(cloudDetailWarpNoiseType[layerIndex], new UnityEngine.GUIContent("Noise Type"));
            PropertyField(cloudDetailWarpGridScale[layerIndex], new UnityEngine.GUIContent("Scale"));
            PropertyField(cloudDetailWarpOctaves[layerIndex], new UnityEngine.GUIContent("Octaves"));
            PropertyField(cloudDetailWarpOctaveScale[layerIndex], new UnityEngine.GUIContent("Octave Scale"));
            PropertyField(cloudDetailWarpOctaveMultiplier[layerIndex], new UnityEngine.GUIContent("Octave Balance"));
          } else {
            if ((ExpanseCommon.CloudGeometryType) cloudGeometryType[layerIndex].value.enumValueIndex == ExpanseCommon.CloudGeometryType.Plane) {
              PropertyField(cloudDetailWarpNoiseTexture2D[layerIndex], new UnityEngine.GUIContent("Noise Texture"));
            } else if ((ExpanseCommon.CloudGeometryType) cloudGeometryType[layerIndex].value.enumValueIndex == ExpanseCommon.CloudGeometryType.BoxVolume) {
              PropertyField(cloudDetailWarpNoiseTexture3D[layerIndex], new UnityEngine.GUIContent("Noise Texture"));
            }
          }
          PropertyField(cloudDetailWarpTile[layerIndex], new UnityEngine.GUIContent("Tile"));
          break;
        }

        default: {
          break;
        }
      }
    }

    EditorGUILayout.EndFadeGroup();

    EditorGUILayout.Space();
  }

  EditorGUILayout.EndFoldoutHeaderGroup();
}

private void cloudMovement(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle, int layerIndex) {
  cloudMovementFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(cloudMovementFoldout, "Movement", titleStyle);

  if (cloudMovementFoldout) {
    EditorGUILayout.Space();
  }

  EditorGUILayout.EndFoldoutHeaderGroup();
}

private void cloudLighting(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle, int layerIndex) {
  cloudLightingFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(cloudLightingFoldout, "Lighting", titleStyle);

  if (cloudLightingFoldout) {
    if ((ExpanseCommon.CloudGeometryType) cloudGeometryType[layerIndex].value.enumValueIndex == ExpanseCommon.CloudGeometryType.BoxVolume) {
      /* 3D. TODO: better names */
      PropertyField(cloudVerticalProbabilityHeightRange[layerIndex], new UnityEngine.GUIContent("Vertical Probability Height Range"));
      PropertyField(cloudVerticalProbabilityStrength[layerIndex], new UnityEngine.GUIContent("Vertical Probability Strength"));
      PropertyField(cloudDepthProbabilityHeightRange[layerIndex], new UnityEngine.GUIContent("Depth Probability Height Range"));
      PropertyField(cloudDepthProbabilityStrengthRange[layerIndex], new UnityEngine.GUIContent("Depth Probability Strength Range"));
      PropertyField(cloudDepthProbabilityDensityMultiplier[layerIndex], new UnityEngine.GUIContent("Depth Probability Density Multiplier"));
      PropertyField(cloudDepthProbabilityBias[layerIndex], new UnityEngine.GUIContent("Depth Probability Bias"));
    } else {
      /* 2D. */
      PropertyField(cloudThickness[layerIndex], new UnityEngine.GUIContent("Apparent Thickness"));
    }

    /* 2D and 3D. */
    PropertyField(cloudDensity[layerIndex], new UnityEngine.GUIContent("Density"));
    PropertyField(cloudDensityAttenuationDistance[layerIndex], new UnityEngine.GUIContent("Density Attenuation Distance"));
    PropertyField(cloudDensityAttenuationBias[layerIndex], new UnityEngine.GUIContent("Density Attenuation Bias"));
    PropertyField(cloudAbsorptionCoefficients[layerIndex], new UnityEngine.GUIContent("Absorption Coefficients"));
    PropertyField(cloudScatteringCoefficients[layerIndex], new UnityEngine.GUIContent("Scattering Coefficients"));
    PropertyField(cloudMSAmount[layerIndex], new UnityEngine.GUIContent("Multiple Scattering Amount"));
    PropertyField(cloudMSBias[layerIndex], new UnityEngine.GUIContent("Multiple Scattering Bias"));
    PropertyField(cloudSilverIntensity[layerIndex], new UnityEngine.GUIContent("Silver Intensity"));
    PropertyField(cloudSilverSpread[layerIndex], new UnityEngine.GUIContent("Silver Spread"));
    PropertyField(cloudAnisotropy[layerIndex], new UnityEngine.GUIContent("Anisotropy"));

    EditorGUILayout.Space();
  }

  EditorGUILayout.EndFoldoutHeaderGroup();
}

private void cloudSampling(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle, int layerIndex) {
  cloudSamplingFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(cloudSamplingFoldout, "Sampling", titleStyle);

  if (cloudSamplingFoldout) {
    EditorGUILayout.Space();
  }

  EditorGUILayout.EndFoldoutHeaderGroup();
}

/******************************************************************************/
/************************** END INDIVIDUAL ELEMENTS ***************************/
/******************************************************************************/



/******************************************************************************/
/********************************** HELPERS ***********************************/
/******************************************************************************/

private void unpackSerializedProperties(PropertyFetcher<Expanse> o) {
  /***********************/
  /********* Sky *********/
  /***********************/
  /* Planet. */
  atmosphereThickness = Unpack(o.Find(x => x.atmosphereThickness));
  planetRadius = Unpack(o.Find(x => x.planetRadius));
  planetOriginOffset = Unpack(o.Find(x => x.planetOriginOffset));
  groundAlbedoTexture = Unpack(o.Find(x => x.groundAlbedoTexture));
  groundTint = Unpack(o.Find(x => x.groundTint));
  groundEmissionTexture = Unpack(o.Find(x => x.groundEmissionTexture));
  groundEmissionMultiplier = Unpack(o.Find(x => x.groundEmissionMultiplier));
  planetRotation = Unpack(o.Find(x => x.planetRotation));

  /* Atmosphere layers. */
  for (int i = 0; i < ExpanseCommon.kMaxAtmosphereLayers; i++) {
    layerEnabled[i] = Unpack(o.Find("layerEnabled" + i));
    layerCoefficientsA[i] = Unpack(o.Find("layerCoefficientsA" + i));
    layerCoefficientsS[i] = Unpack(o.Find("layerCoefficientsS" + i));
    layerDensityDistribution[i] = Unpack(o.Find("layerDensityDistribution" + i));
    layerHeight[i] = Unpack(o.Find("layerHeight" + i));
    layerThickness[i] = Unpack(o.Find("layerThickness" + i));
    layerPhaseFunction[i] = Unpack(o.Find("layerPhaseFunction" + i));
    layerAnisotropy[i] = Unpack(o.Find("layerAnisotropy" + i));
    layerDensity[i] = Unpack(o.Find("layerDensity" + i));
    layerDensityAttenuationPlayerOrigin[i] = Unpack(o.Find("layerDensityAttenuationPlayerOrigin" + i));
    layerDensityAttenuationOrigin[i] = Unpack(o.Find("layerDensityAttenuationOrigin" + i));
    layerAttenuationDistance[i] = Unpack(o.Find("layerAttenuationDistance" + i));
    layerAttenuationBias[i] = Unpack(o.Find("layerAttenuationBias" + i));
    layerTint[i] = Unpack(o.Find("layerTint" + i));
    layerMultipleScatteringMultiplier[i] = Unpack(o.Find("layerMultipleScatteringMultiplier" + i));
  }

  /* Celestial bodies. */
  for (int i = 0; i < ExpanseCommon.kMaxAtmosphereLayers; i++) {
    bodyEnabled[i] = Unpack(o.Find("bodyEnabled" + i));
    bodyUseDateTime[i] = Unpack(o.Find("bodyUseDateTime" + i));
    bodyDirection[i] = Unpack(o.Find("bodyDirection" + i));
    bodyDateTime[i] = Unpack(o.Find("bodyDateTime" + i));
    bodyPlayerLatitudeLongitude[i] = Unpack(o.Find("bodyPlayerLatitudeLongitude" + i));
    bodyAngularRadius[i] = Unpack(o.Find("bodyAngularRadius" + i));
    bodyDistance[i] = Unpack(o.Find("bodyDistance" + i));
    bodyReceivesLight[i] = Unpack(o.Find("bodyReceivesLight" + i));
    bodyAlbedoTexture[i] = Unpack(o.Find("bodyAlbedoTexture" + i));
    bodyAlbedoTextureRotation[i] = Unpack(o.Find("bodyAlbedoTextureRotation" + i));
    bodyAlbedoTint[i] = Unpack(o.Find("bodyAlbedoTint" + i));
    bodyEmissive[i] = Unpack(o.Find("bodyEmissive" + i));
    bodyUseTemperature[i] = Unpack(o.Find("bodyUseTemperature" + i));
    bodyLightIntensity[i] = Unpack(o.Find("bodyLightIntensity" + i));
    bodyLightColor[i] = Unpack(o.Find("bodyLightColor" + i));
    bodyLightTemperature[i] = Unpack(o.Find("bodyLightTemperature" + i));
    bodyLimbDarkening[i] = Unpack(o.Find("bodyLimbDarkening" + i));
    bodyEmissionTexture[i] = Unpack(o.Find("bodyEmissionTexture" + i));
    bodyEmissionTextureRotation[i] = Unpack(o.Find("bodyEmissionTextureRotation" + i));
    bodyEmissionTint[i] = Unpack(o.Find("bodyEmissionTint" + i));
    bodyEmissionMultiplier[i] = Unpack(o.Find("bodyEmissionMultiplier" + i));
  }

  /* Night Sky. */
  useProceduralNightSky = Unpack(o.Find(x => x.useProceduralNightSky));
  /* Procedural. */
  starTextureQuality = Unpack(o.Find(x => x.starTextureQuality));
  showStarSeeds = Unpack(o.Find(x => x.showStarSeeds));
  useHighDensityMode = Unpack(o.Find(x => x.useHighDensityMode));
  starDensity = Unpack(o.Find(x => x.starDensity));
  starDensitySeed = Unpack(o.Find(x => x.starDensitySeed));
  starSizeRange = Unpack(o.Find(x => x.starSizeRange));
  starSizeBias = Unpack(o.Find(x => x.starSizeBias));
  starSizeSeed = Unpack(o.Find(x => x.starSizeSeed));
  starIntensityRange = Unpack(o.Find(x => x.starIntensityRange));
  starIntensityBias = Unpack(o.Find(x => x.starIntensityBias));
  starIntensitySeed = Unpack(o.Find(x => x.starIntensitySeed));
  starTemperatureRange = Unpack(o.Find(x => x.starTemperatureRange));
  starTemperatureBias = Unpack(o.Find(x => x.starTemperatureBias));
  starTemperatureSeed = Unpack(o.Find(x => x.starTemperatureSeed));
  starTint = Unpack(o.Find(x => x.starTint));
  /* Nebulae. */
  useProceduralNebulae = Unpack(o.Find(x => x.useProceduralNebulae));
  nebulaeTextureQuality = Unpack(o.Find(x => x.nebulaeTextureQuality));
  /* Procedural nebulae. */
  nebulaOverallDefinition = Unpack(o.Find(x => x.nebulaOverallDefinition));
  nebulaOverallIntensity = Unpack(o.Find(x => x.nebulaOverallIntensity));
  nebulaCoverageScale = Unpack(o.Find(x => x.nebulaCoverageScale));
  nebulaHazeBrightness = Unpack(o.Find(x => x.nebulaHazeBrightness));
  nebulaHazeColor = Unpack(o.Find(x => x.nebulaHazeColor));
  nebulaHazeScale = Unpack(o.Find(x => x.nebulaHazeScale));
  nebulaHazeScaleFactor = Unpack(o.Find(x => x.nebulaHazeScaleFactor));
  nebulaHazeDetailBalance = Unpack(o.Find(x => x.nebulaHazeDetailBalance));
  nebulaHazeOctaves = Unpack(o.Find(x => x.nebulaHazeOctaves));
  nebulaHazeBias = Unpack(o.Find(x => x.nebulaHazeBias));
  nebulaHazeSpread = Unpack(o.Find(x => x.nebulaHazeSpread));
  nebulaHazeCoverage = Unpack(o.Find(x => x.nebulaHazeCoverage));
  nebulaHazeStrength = Unpack(o.Find(x => x.nebulaHazeStrength));
  nebulaCloudBrightness = Unpack(o.Find(x => x.nebulaCloudBrightness));
  nebulaCloudColor = Unpack(o.Find(x => x.nebulaCloudColor));
  nebulaCloudScale = Unpack(o.Find(x => x.nebulaCloudScale));
  nebulaCloudScaleFactor = Unpack(o.Find(x => x.nebulaCloudScaleFactor));
  nebulaCloudDetailBalance = Unpack(o.Find(x => x.nebulaCloudDetailBalance));
  nebulaCloudOctaves = Unpack(o.Find(x => x.nebulaCloudOctaves));
  nebulaCloudBias = Unpack(o.Find(x => x.nebulaCloudBias));
  nebulaCloudSpread = Unpack(o.Find(x => x.nebulaCloudSpread));
  nebulaCloudCoverage = Unpack(o.Find(x => x.nebulaCloudCoverage));
  nebulaCloudStrength = Unpack(o.Find(x => x.nebulaCloudStrength));
  nebulaCoarseStrandBrightness = Unpack(o.Find(x => x.nebulaCoarseStrandBrightness));
  nebulaCoarseStrandColor = Unpack(o.Find(x => x.nebulaCoarseStrandColor));
  nebulaCoarseStrandScale = Unpack(o.Find(x => x.nebulaCoarseStrandScale));
  nebulaCoarseStrandScaleFactor = Unpack(o.Find(x => x.nebulaCoarseStrandScaleFactor));
  nebulaCoarseStrandDetailBalance = Unpack(o.Find(x => x.nebulaCoarseStrandDetailBalance));
  nebulaCoarseStrandOctaves = Unpack(o.Find(x => x.nebulaCoarseStrandOctaves));
  nebulaCoarseStrandBias = Unpack(o.Find(x => x.nebulaCoarseStrandBias));
  nebulaCoarseStrandDefinition = Unpack(o.Find(x => x.nebulaCoarseStrandDefinition));
  nebulaCoarseStrandSpread = Unpack(o.Find(x => x.nebulaCoarseStrandSpread));
  nebulaCoarseStrandCoverage = Unpack(o.Find(x => x.nebulaCoarseStrandCoverage));
  nebulaCoarseStrandStrength = Unpack(o.Find(x => x.nebulaCoarseStrandStrength));
  nebulaCoarseStrandWarpScale = Unpack(o.Find(x => x.nebulaCoarseStrandWarpScale));
  nebulaCoarseStrandWarp = Unpack(o.Find(x => x.nebulaCoarseStrandWarp));
  nebulaFineStrandBrightness = Unpack(o.Find(x => x.nebulaFineStrandBrightness));
  nebulaFineStrandColor = Unpack(o.Find(x => x.nebulaFineStrandColor));
  nebulaFineStrandScale = Unpack(o.Find(x => x.nebulaFineStrandScale));
  nebulaFineStrandScaleFactor = Unpack(o.Find(x => x.nebulaFineStrandScaleFactor));
  nebulaFineStrandDetailBalance = Unpack(o.Find(x => x.nebulaFineStrandDetailBalance));
  nebulaFineStrandOctaves = Unpack(o.Find(x => x.nebulaFineStrandOctaves));
  nebulaFineStrandBias = Unpack(o.Find(x => x.nebulaFineStrandBias));
  nebulaFineStrandDefinition = Unpack(o.Find(x => x.nebulaFineStrandDefinition));
  nebulaFineStrandSpread = Unpack(o.Find(x => x.nebulaFineStrandSpread));
  nebulaFineStrandCoverage = Unpack(o.Find(x => x.nebulaFineStrandCoverage));
  nebulaFineStrandStrength = Unpack(o.Find(x => x.nebulaFineStrandStrength));
  nebulaFineStrandWarpScale = Unpack(o.Find(x => x.nebulaFineStrandWarpScale));
  nebulaFineStrandWarp = Unpack(o.Find(x => x.nebulaFineStrandWarp));
  nebulaTransmittanceRange = Unpack(o.Find(x => x.nebulaTransmittanceRange));
  nebulaTransmittanceScale = Unpack(o.Find(x => x.nebulaTransmittanceScale));
  starNebulaFollowAmount = Unpack(o.Find(x => x.starNebulaFollowAmount));
  starNebulaFollowSpread = Unpack(o.Find(x => x.starNebulaFollowSpread));
  nebulaCoverageSeed = Unpack(o.Find(x => x.nebulaCoverageSeed));
  nebulaHazeSeedX = Unpack(o.Find(x => x.nebulaHazeSeedX));
  nebulaHazeSeedY = Unpack(o.Find(x => x.nebulaHazeSeedY));
  nebulaHazeSeedZ = Unpack(o.Find(x => x.nebulaHazeSeedZ));
  nebulaCloudSeedX = Unpack(o.Find(x => x.nebulaCloudSeedX));
  nebulaCloudSeedY = Unpack(o.Find(x => x.nebulaCloudSeedY));
  nebulaCloudSeedZ = Unpack(o.Find(x => x.nebulaCloudSeedZ));
  nebulaCoarseStrandSeedX = Unpack(o.Find(x => x.nebulaCoarseStrandSeedX));
  nebulaCoarseStrandSeedY = Unpack(o.Find(x => x.nebulaCoarseStrandSeedY));
  nebulaCoarseStrandSeedZ = Unpack(o.Find(x => x.nebulaCoarseStrandSeedZ));
  nebulaCoarseStrandWarpSeedX = Unpack(o.Find(x => x.nebulaCoarseStrandWarpSeedX));
  nebulaCoarseStrandWarpSeedY = Unpack(o.Find(x => x.nebulaCoarseStrandWarpSeedY));
  nebulaCoarseStrandWarpSeedZ = Unpack(o.Find(x => x.nebulaCoarseStrandWarpSeedZ));
  nebulaFineStrandSeedX = Unpack(o.Find(x => x.nebulaFineStrandSeedX));
  nebulaFineStrandSeedY = Unpack(o.Find(x => x.nebulaFineStrandSeedY));
  nebulaFineStrandSeedZ = Unpack(o.Find(x => x.nebulaFineStrandSeedZ));
  nebulaFineStrandWarpSeedX = Unpack(o.Find(x => x.nebulaFineStrandWarpSeedX));
  nebulaFineStrandWarpSeedY = Unpack(o.Find(x => x.nebulaFineStrandWarpSeedY));
  nebulaFineStrandWarpSeedZ = Unpack(o.Find(x => x.nebulaFineStrandWarpSeedZ));
  nebulaTransmittanceSeedX = Unpack(o.Find(x => x.nebulaTransmittanceSeedX));
  nebulaTransmittanceSeedY = Unpack(o.Find(x => x.nebulaTransmittanceSeedY));
  nebulaTransmittanceSeedZ = Unpack(o.Find(x => x.nebulaTransmittanceSeedZ));
  showNebulaeSeeds = Unpack(o.Find(x => x.showNebulaeSeeds));

  /* Regular nebulae. */
  nebulaeTexture = Unpack(o.Find(x => x.nebulaeTexture));

  /* Regular. */
  lightPollutionTint = Unpack(o.Find(x => x.lightPollutionTint));
  lightPollutionIntensity = Unpack(o.Find(x => x.lightPollutionIntensity));
  nightSkyTexture = Unpack(o.Find(x => x.nightSkyTexture));
  nightSkyRotation = Unpack(o.Find(x => x.nightSkyRotation));
  nightSkyTint = Unpack(o.Find(x => x.nightSkyTint));
  nightSkyIntensity = Unpack(o.Find(x => x.nightSkyIntensity));
  nightSkyAmbientMultiplier = Unpack(o.Find(x => x.nightSkyAmbientMultiplier));
  nightSkyScatterTint = Unpack(o.Find(x => x.nightSkyScatterTint));
  nightSkyScatterIntensity = Unpack(o.Find(x => x.nightSkyScatterIntensity));
  useTwinkle = Unpack(o.Find(x => x.useTwinkle));
  twinkleThreshold = Unpack(o.Find(x => x.twinkleThreshold));
  twinkleFrequencyRange = Unpack(o.Find(x => x.twinkleFrequencyRange));
  twinkleBias = Unpack(o.Find(x => x.twinkleBias));
  twinkleSmoothAmplitude = Unpack(o.Find(x => x.twinkleSmoothAmplitude));
  twinkleChaoticAmplitude = Unpack(o.Find(x => x.twinkleChaoticAmplitude));

  /* Aerial perspective. */
  aerialPerspectiveOcclusionPowerUniform = Unpack(o.Find(x => x.aerialPerspectiveOcclusionPowerUniform));
  aerialPerspectiveOcclusionBiasUniform = Unpack(o.Find(x => x.aerialPerspectiveOcclusionBiasUniform));
  aerialPerspectiveOcclusionPowerDirectional = Unpack(o.Find(x => x.aerialPerspectiveOcclusionPowerDirectional));
  aerialPerspectiveOcclusionBiasDirectional = Unpack(o.Find(x => x.aerialPerspectiveOcclusionBiasDirectional));
  aerialPerspectiveNightScatteringMultiplier = Unpack(o.Find(x => x.aerialPerspectiveNightScatteringMultiplier));

  /* Quality. */
  skyTextureQuality = Unpack(o.Find(x => x.skyTextureQuality));
  numberOfTransmittanceSamples = Unpack(o.Find(x => x.numberOfTransmittanceSamples));
  numberOfAerialPerspectiveSamples = Unpack(o.Find(x => x.numberOfAerialPerspectiveSamples));
  numberOfSingleScatteringSamples = Unpack(o.Find(x => x.numberOfSingleScatteringSamples));
  numberOfMultipleScatteringSamples = Unpack(o.Find(x => x.numberOfMultipleScatteringSamples));
  numberOfMultipleScatteringAccumulationSamples = Unpack(o.Find(x => x.numberOfMultipleScatteringAccumulationSamples));
  useImportanceSampling = Unpack(o.Find(x => x.useImportanceSampling));
  aerialPerspectiveUseImportanceSampling = Unpack(o.Find(x => x.aerialPerspectiveUseImportanceSampling));
  aerialPerspectiveDepthSkew = Unpack(o.Find(x => x.aerialPerspectiveDepthSkew));
  useAntiAliasing = Unpack(o.Find(x => x.useAntiAliasing));
  useDither = Unpack(o.Find(x => x.useDither));

  /***********************/
  /******* Clouds ********/
  /***********************/
  for (int i = 0; i < ExpanseCommon.kMaxCloudLayers; i++) {
    /* General. */
    cloudLayerEnabled[i] = Unpack(o.Find("cloudLayerEnabled" + i));

    /* Geometry. */
    cloudGeometryType[i] = Unpack(o.Find("cloudGeometryType" + i));
    cloudGeometryXExtent[i] = Unpack(o.Find("cloudGeometryXExtent" + i));
    cloudGeometryYExtent[i] = Unpack(o.Find("cloudGeometryYExtent" + i));
    cloudGeometryZExtent[i] = Unpack(o.Find("cloudGeometryZExtent" + i));
    cloudGeometryHeight[i] = Unpack(o.Find("cloudGeometryHeight" + i));

    /* Noise generation. */
    cloudNoiseQuality[i] = Unpack(o.Find("cloudNoiseQuality" + i));
    /* Coverage. */
    cloudCoverageNoiseProcedural[i] = Unpack(o.Find("cloudCoverageNoiseProcedural" + i));
    cloudCoverageNoiseTexture[i] = Unpack(o.Find("cloudCoverageNoiseTexture" + i));
    cloudCoverageNoiseType[i] = Unpack(o.Find("cloudCoverageNoiseType" + i));
    cloudCoverageGridScale[i] = Unpack(o.Find("cloudCoverageGridScale" + i));
    cloudCoverageOctaves[i] = Unpack(o.Find("cloudCoverageOctaves" + i));
    cloudCoverageOctaveScale[i] = Unpack(o.Find("cloudCoverageOctaveScale" + i));
    cloudCoverageOctaveMultiplier[i] = Unpack(o.Find("cloudCoverageOctaveMultiplier" + i));
    cloudCoverageTile[i] = Unpack(o.Find("cloudCoverageTile" + i));
    cloudCoverageIntensity[i] = Unpack(o.Find("cloudCoverageIntensity" + i));
    /* Base. */
    cloudBaseNoiseProcedural[i] = Unpack(o.Find("cloudBaseNoiseProcedural" + i));
    cloudBaseNoiseTexture2D[i] = Unpack(o.Find("cloudBaseNoiseTexture2D" + i));
    cloudBaseNoiseTexture3D[i] = Unpack(o.Find("cloudBaseNoiseTexture3D" + i));
    cloudBaseNoiseType[i] = Unpack(o.Find("cloudBaseNoiseType" + i));
    cloudBaseGridScale[i] = Unpack(o.Find("cloudBaseGridScale" + i));
    cloudBaseOctaves[i] = Unpack(o.Find("cloudBaseOctaves" + i));
    cloudBaseOctaveScale[i] = Unpack(o.Find("cloudBaseOctaveScale" + i));
    cloudBaseOctaveMultiplier[i] = Unpack(o.Find("cloudBaseOctaveMultiplier" + i));
    cloudBaseTile[i] = Unpack(o.Find("cloudBaseTile" + i));
    /* Structure. */
    cloudStructureNoiseProcedural[i] = Unpack(o.Find("cloudStructureNoiseProcedural" + i));
    cloudStructureNoiseTexture2D[i] = Unpack(o.Find("cloudStructureNoiseTexture2D" + i));
    cloudStructureNoiseTexture3D[i] = Unpack(o.Find("cloudStructureNoiseTexture3D" + i));
    cloudStructureNoiseType[i] = Unpack(o.Find("cloudStructureNoiseType" + i));
    cloudStructureGridScale[i] = Unpack(o.Find("cloudStructureGridScale" + i));
    cloudStructureOctaves[i] = Unpack(o.Find("cloudStructureOctaves" + i));
    cloudStructureOctaveScale[i] = Unpack(o.Find("cloudStructureOctaveScale" + i));
    cloudStructureOctaveMultiplier[i] = Unpack(o.Find("cloudStructureOctaveMultiplier" + i));
    cloudStructureTile[i] = Unpack(o.Find("cloudStructureTile" + i));
    cloudStructureIntensity[i] = Unpack(o.Find("cloudStructureIntensity" + i));
    /* Detail. */
    cloudDetailNoiseProcedural[i] = Unpack(o.Find("cloudDetailNoiseProcedural" + i));
    cloudDetailNoiseTexture2D[i] = Unpack(o.Find("cloudDetailNoiseTexture2D" + i));
    cloudDetailNoiseTexture3D[i] = Unpack(o.Find("cloudDetailNoiseTexture3D" + i));
    cloudDetailNoiseType[i] = Unpack(o.Find("cloudDetailNoiseType" + i));
    cloudDetailGridScale[i] = Unpack(o.Find("cloudDetailGridScale" + i));
    cloudDetailOctaves[i] = Unpack(o.Find("cloudDetailOctaves" + i));
    cloudDetailOctaveScale[i] = Unpack(o.Find("cloudDetailOctaveScale" + i));
    cloudDetailOctaveMultiplier[i] = Unpack(o.Find("cloudDetailOctaveMultiplier" + i));
    cloudDetailTile[i] = Unpack(o.Find("cloudDetailTile" + i));
    cloudDetailIntensity[i] = Unpack(o.Find("cloudDetailIntensity" + i));
    /* Base Warp. */
    cloudBaseWarpNoiseProcedural[i] = Unpack(o.Find("cloudBaseWarpNoiseProcedural" + i));
    cloudBaseWarpNoiseTexture2D[i] = Unpack(o.Find("cloudBaseWarpNoiseTexture2D" + i));
    cloudBaseWarpNoiseTexture3D[i] = Unpack(o.Find("cloudBaseWarpNoiseTexture3D" + i));
    cloudBaseWarpNoiseType[i] = Unpack(o.Find("cloudBaseWarpNoiseType" + i));
    cloudBaseWarpGridScale[i] = Unpack(o.Find("cloudBaseWarpGridScale" + i));
    cloudBaseWarpOctaves[i] = Unpack(o.Find("cloudBaseWarpOctaves" + i));
    cloudBaseWarpOctaveScale[i] = Unpack(o.Find("cloudBaseWarpOctaveScale" + i));
    cloudBaseWarpOctaveMultiplier[i] = Unpack(o.Find("cloudBaseWarpOctaveMultiplier" + i));
    cloudBaseWarpTile[i] = Unpack(o.Find("cloudBaseWarpTile" + i));
    cloudBaseWarpIntensity[i] = Unpack(o.Find("cloudBaseWarpIntensity" + i));
    /* Detail Warp. */
    cloudDetailWarpNoiseProcedural[i] = Unpack(o.Find("cloudDetailWarpNoiseProcedural" + i));
    cloudDetailWarpNoiseTexture2D[i] = Unpack(o.Find("cloudDetailWarpNoiseTexture2D" + i));
    cloudDetailWarpNoiseTexture3D[i] = Unpack(o.Find("cloudDetailWarpNoiseTexture3D" + i));
    cloudDetailWarpNoiseType[i] = Unpack(o.Find("cloudDetailWarpNoiseType" + i));
    cloudDetailWarpGridScale[i] = Unpack(o.Find("cloudDetailWarpGridScale" + i));
    cloudDetailWarpOctaves[i] = Unpack(o.Find("cloudDetailWarpOctaves" + i));
    cloudDetailWarpOctaveScale[i] = Unpack(o.Find("cloudDetailWarpOctaveScale" + i));
    cloudDetailWarpOctaveMultiplier[i] = Unpack(o.Find("cloudDetailWarpOctaveMultiplier" + i));
    cloudDetailWarpTile[i] = Unpack(o.Find("cloudDetailWarpTile" + i));
    cloudDetailWarpIntensity[i] = Unpack(o.Find("cloudDetailWarpIntensity" + i));
    /* Height gradient. */
    cloudHeightGradientBottom[i] = Unpack(o.Find("cloudHeightGradientBottom" + i));
    cloudHeightGradientTop[i] = Unpack(o.Find("cloudHeightGradientTop" + i));

    /* Movement---sampling offsets primarily. TODO */

    /* Lighting. */
    /* 2D. */
    cloudThickness[i] = Unpack(o.Find("cloudThickness" + i));
    /* 3D. */
    cloudVerticalProbabilityHeightRange[i] = Unpack(o.Find("cloudVerticalProbabilityHeightRange" + i));
    cloudVerticalProbabilityStrength[i] = Unpack(o.Find("cloudVerticalProbabilityStrength" + i));
    cloudDepthProbabilityHeightRange[i] = Unpack(o.Find("cloudDepthProbabilityHeightRange" + i));
    cloudDepthProbabilityStrengthRange[i] = Unpack(o.Find("cloudDepthProbabilityStrengthRange" + i));
    cloudDepthProbabilityDensityMultiplier[i] = Unpack(o.Find("cloudDepthProbabilityDensityMultiplier" + i));
    cloudDepthProbabilityBias[i] = Unpack(o.Find("cloudDepthProbabilityBias" + i));
    /* 2D and 3D. */
    cloudDensity[i] = Unpack(o.Find("cloudDensity" + i));
    cloudDensityAttenuationDistance[i] = Unpack(o.Find("cloudDensityAttenuationDistance" + i));
    cloudDensityAttenuationBias[i] = Unpack(o.Find("cloudDensityAttenuationBias" + i));
    cloudAbsorptionCoefficients[i] = Unpack(o.Find("cloudAbsorptionCoefficients" + i));
    cloudScatteringCoefficients[i] = Unpack(o.Find("cloudScatteringCoefficients" + i));
    cloudMSAmount[i] = Unpack(o.Find("cloudMSAmount" + i));
    cloudMSBias[i] = Unpack(o.Find("cloudMSBias" + i));
    cloudSilverSpread[i] = Unpack(o.Find("cloudSilverSpread" + i));
    cloudSilverIntensity[i] = Unpack(o.Find("cloudSilverIntensity" + i));
    cloudAnisotropy[i] = Unpack(o.Find("cloudAnisotropy" + i));

    /* Sampling. TODO */

  }
}

private int setEnumSelect(AnimBool[] showEnum, int selected) {
  /* Initialize all show atmosphere layers to false. */
  for (int i = 0; i < showEnum.Length; i++) {
    showEnum[i] = new AnimBool(i == selected);
  }
  return selected;
}

/******************************************************************************/
/******************************** END HELPERS *********************************/
/******************************************************************************/

}
