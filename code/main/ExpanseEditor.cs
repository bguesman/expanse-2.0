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
/* Which atmosphere layer is currently shown for editing. */
ExpanseCommon.CelestialBody m_celestialBodySelect = ExpanseCommon.CelestialBody.Body0;

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
SerializedDataParameter numberOfGroundIrradianceSamples;
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
/* Lighting. TODO */
/* TODO: density control goes here. */

/* Noise generation. TODO */

/* Movement---sampling offsets primarily. TODO */

/* Geometry. TODO */

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
  mainHeaderStyle.normal.textColor = UnityEngine.Color.white;
  UnityEngine.GUIStyle titleStyle = new UnityEngine.GUIStyle();
  titleStyle.fontSize = 14;
  titleStyle.normal.textColor = UnityEngine.Color.white;
  UnityEngine.GUIStyle subtitleStyle = new UnityEngine.GUIStyle();
  subtitleStyle.fontSize = 12;
  subtitleStyle.normal.textColor = UnityEngine.Color.white;

  /***********************/
  /********* Sky *********/
  /***********************/
  EditorGUILayout.Space();
  EditorGUILayout.LabelField("Sky", mainHeaderStyle);
  EditorGUILayout.Space();

  /* Planet. */
  EditorGUILayout.Space();
  planet(titleStyle, subtitleStyle);
  EditorGUILayout.Space();

  /* Atmosphere layers. */
  EditorGUILayout.Space();
  atmosphereLayer(titleStyle, subtitleStyle);
  EditorGUILayout.Space();

  /* Celestial bodies. */
  EditorGUILayout.Space();
  celestialBody(titleStyle, subtitleStyle);
  EditorGUILayout.Space();

  /* Night Sky. */
  EditorGUILayout.Space();
  nightSky(titleStyle, subtitleStyle);
  EditorGUILayout.Space();

  /* Night Sky. */
  EditorGUILayout.Space();
  aerialPerspective(titleStyle, subtitleStyle);
  EditorGUILayout.Space();

  /* Quality. */
  EditorGUILayout.Space();
  quality(titleStyle, subtitleStyle);
  base.CommonSkySettingsGUI();
  EditorGUILayout.Space();

  EditorGUILayout.Space();
  EditorGUILayout.Space();

  /***********************/
  /******* Clouds ********/
  /***********************/
  EditorGUILayout.Space();
  EditorGUILayout.LabelField("Clouds", mainHeaderStyle);
  EditorGUILayout.Space();

  /* Lighting. */
  /* TODO: density control goes here. */
  EditorGUILayout.Space();
  cloudLighting(titleStyle, subtitleStyle);
  EditorGUILayout.Space();

  /* Noise generation. */
  EditorGUILayout.Space();
  cloudNoise(titleStyle, subtitleStyle);
  EditorGUILayout.Space();

  /* Movement---sampling offsets primarily. */
  EditorGUILayout.Space();
  cloudMovement(titleStyle, subtitleStyle);
  EditorGUILayout.Space();

  /* Geometry. */
  EditorGUILayout.Space();
  cloudGeometry(titleStyle, subtitleStyle);
  EditorGUILayout.Space();

  /* Sampling. */
  /* TODO: debug goes here. */
  EditorGUILayout.Space();
  cloudSampling(titleStyle, subtitleStyle);
  EditorGUILayout.Space();
}

/******************************************************************************/
/**************************** END MAIN FUNCTIONS ******************************/
/******************************************************************************/



/******************************************************************************/
/**************************** INDIVIDUAL ELEMENTS *****************************/
/******************************************************************************/

private void planet(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  /* Planet. */
  EditorGUILayout.LabelField("Planet", titleStyle);
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
}

private void atmosphereLayer(UnityEngine.GUIStyle titleStyle,
  UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Atmosphere", titleStyle);
  m_atmosphereLayerSelect = (ExpanseCommon.AtmosphereLayer)
    EditorGUILayout.EnumPopup("Layer", m_atmosphereLayerSelect);

  /* Set the atmosphere layer select. */
  int atmosphereSelectIndex = setEnumSelect(m_showAtmosphereLayer,
    (int) m_atmosphereLayerSelect);

  /* Display atmosphere params for it. */
  if (UnityEditor.EditorGUILayout.BeginFadeGroup(m_showAtmosphereLayer[atmosphereSelectIndex].faded))
  {

    PropertyField(layerEnabled[atmosphereSelectIndex], new UnityEngine.GUIContent("Enabled"));
    PropertyField(layerCoefficientsA[atmosphereSelectIndex], new UnityEngine.GUIContent("Absorption Coefficients"));
    PropertyField(layerCoefficientsS[atmosphereSelectIndex], new UnityEngine.GUIContent("Scattering Coefficients"));
    PropertyField(layerDensity[atmosphereSelectIndex], new UnityEngine.GUIContent("Density"));

    /* Density distribution selection dropdown. */
    PropertyField(layerDensityDistribution[atmosphereSelectIndex], new UnityEngine.GUIContent("Layer Density Distribution"));
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
    PropertyField(layerPhaseFunction[atmosphereSelectIndex], new UnityEngine.GUIContent("Layer Phase Function"));
    if ((ExpanseCommon.PhaseFunction) layerPhaseFunction[atmosphereSelectIndex].value.enumValueIndex == ExpanseCommon.PhaseFunction.Mie) {
      /* Only display anisotropy control if Mie scattering is enabled. */
      PropertyField(layerAnisotropy[atmosphereSelectIndex], new UnityEngine.GUIContent("Anisotropy"));
    }

    PropertyField(layerTint[atmosphereSelectIndex], new UnityEngine.GUIContent("Tint"));
    PropertyField(layerMultipleScatteringMultiplier[atmosphereSelectIndex], new UnityEngine.GUIContent("Multiple Scattering Multiplier"));
  }

  UnityEditor.EditorGUILayout.EndFadeGroup();
}

private void celestialBody(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Celestial Bodies", titleStyle);
  m_celestialBodySelect = (ExpanseCommon.CelestialBody) EditorGUILayout.EnumPopup("Body", m_celestialBodySelect);

  /* Set the celestial body select. */
  int bodySelectIndex = setEnumSelect(m_showCelestialBody, (int) m_celestialBodySelect);

  /* Display celestial body params for it. */
  if (UnityEditor.EditorGUILayout.BeginFadeGroup(m_showCelestialBody[bodySelectIndex].faded))
  {
    PropertyField(bodyEnabled[bodySelectIndex], new UnityEngine.GUIContent("Enabled"));
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

  UnityEditor.EditorGUILayout.EndFadeGroup();
}

private void nightSky(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Night Sky", titleStyle);
  PropertyField(useProceduralNightSky, new UnityEngine.GUIContent("Procedural Mode"));
  if (useProceduralNightSky.value.boolValue) {
    /* Procedural sky controls. */
    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Sky", subtitleStyle);
    PropertyField(nightSkyRotation, new UnityEngine.GUIContent("Rotation"));
    PropertyField(nightSkyIntensity, new UnityEngine.GUIContent("Intensity"));
    PropertyField(nightSkyTint, new UnityEngine.GUIContent("Tint"));
    PropertyField(nightSkyScatterIntensity, new UnityEngine.GUIContent("Scatter Intensity"));
    PropertyField(nightSkyScatterTint, new UnityEngine.GUIContent("Scatter Tint"));
    PropertyField(nightSkyAmbientMultiplier, new UnityEngine.GUIContent("Ambient Multiplier"));

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Light Pollution", subtitleStyle);
    PropertyField(lightPollutionTint, new UnityEngine.GUIContent("Tint"));
    PropertyField(lightPollutionIntensity, new UnityEngine.GUIContent("Intensity"));

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Stars", titleStyle);
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
    EditorGUILayout.LabelField("Star Twinkle", subtitleStyle);
    PropertyField(useTwinkle);
    if (useTwinkle.value.boolValue) {
      PropertyField(twinkleThreshold, new UnityEngine.GUIContent("Threshold"));
      PropertyField(twinkleFrequencyRange, new UnityEngine.GUIContent("Frequency Range"));
      PropertyField(twinkleBias, new UnityEngine.GUIContent("Bias"));
      PropertyField(twinkleSmoothAmplitude, new UnityEngine.GUIContent("Smooth Intensity"));
      PropertyField(twinkleChaoticAmplitude, new UnityEngine.GUIContent("Chaotic Intensity"));
    }


    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Nebulae", titleStyle);
    PropertyField(useProceduralNebulae, new UnityEngine.GUIContent("Procedural"));
    if (useProceduralNebulae.value.boolValue) {
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
      nebulaLayerDropdownSelection = EditorGUILayout.Popup("Nebula Layer", nebulaLayerDropdownSelection, nebulaLayerDropdownOptions);
      switch (nebulaLayerDropdownSelection) {
        case 0: {
          PropertyField(nebulaHazeBrightness, new UnityEngine.GUIContent("Intensity"));
          PropertyField(nebulaHazeColor, new UnityEngine.GUIContent("Color"));
          PropertyField(nebulaHazeScale, new UnityEngine.GUIContent("Scale"));
          PropertyField(nebulaHazeScaleFactor, new UnityEngine.GUIContent("Detail"));
          PropertyField(nebulaHazeDetailBalance, new UnityEngine.GUIContent("Detail Balance"));
          PropertyField(nebulaHazeOctaves, new UnityEngine.GUIContent("Octaves"));
          PropertyField(nebulaHazeBias, new UnityEngine.GUIContent("Bias"));
          PropertyField(nebulaHazeStrength, new UnityEngine.GUIContent("Strength"));
          PropertyField(nebulaHazeCoverage, new UnityEngine.GUIContent("Coverage"));
          PropertyField(nebulaHazeSpread, new UnityEngine.GUIContent("Spread"));
          if (showNebulaeSeeds.value.boolValue) {
            PropertyField(nebulaHazeSeedX, new UnityEngine.GUIContent("Haze X Seed"));
            PropertyField(nebulaHazeSeedY, new UnityEngine.GUIContent("Haze Y Seed"));
            PropertyField(nebulaHazeSeedZ, new UnityEngine.GUIContent("Haze Z Seed"));
          }
          break;
        }
        case 1: {
          PropertyField(nebulaCloudBrightness, new UnityEngine.GUIContent("Intensity"));
          PropertyField(nebulaCloudColor, new UnityEngine.GUIContent("Color"));
          PropertyField(nebulaCloudScale, new UnityEngine.GUIContent("Scale"));
          PropertyField(nebulaCloudScaleFactor, new UnityEngine.GUIContent("Detail"));
          PropertyField(nebulaCloudDetailBalance, new UnityEngine.GUIContent("Detail Balance"));
          PropertyField(nebulaCloudOctaves, new UnityEngine.GUIContent("Octaves"));
          PropertyField(nebulaCloudBias, new UnityEngine.GUIContent("Bias"));
          PropertyField(nebulaCloudStrength, new UnityEngine.GUIContent("Strength"));
          PropertyField(nebulaCloudCoverage, new UnityEngine.GUIContent("Coverage"));
          PropertyField(nebulaCloudSpread, new UnityEngine.GUIContent("Spread"));
          if (showNebulaeSeeds.value.boolValue) {
            PropertyField(nebulaCloudSeedX, new UnityEngine.GUIContent("Cloud X Seed"));
            PropertyField(nebulaCloudSeedY, new UnityEngine.GUIContent("Cloud Y Seed"));
            PropertyField(nebulaCloudSeedZ, new UnityEngine.GUIContent("Cloud Z Seed"));
          }
          break;
        }
        case 2: {
          PropertyField(nebulaCoarseStrandBrightness, new UnityEngine.GUIContent("Intensity"));
          PropertyField(nebulaCoarseStrandColor, new UnityEngine.GUIContent("Color"));
          PropertyField(nebulaCoarseStrandScale, new UnityEngine.GUIContent("Scale"));
          PropertyField(nebulaCoarseStrandScaleFactor, new UnityEngine.GUIContent("Detail"));
          PropertyField(nebulaCoarseStrandDetailBalance, new UnityEngine.GUIContent("Detail Balance"));
          PropertyField(nebulaCoarseStrandOctaves, new UnityEngine.GUIContent("Octaves"));
          PropertyField(nebulaCoarseStrandBias, new UnityEngine.GUIContent("Bias"));
          PropertyField(nebulaCoarseStrandStrength, new UnityEngine.GUIContent("Strength"));
          PropertyField(nebulaCoarseStrandDefinition, new UnityEngine.GUIContent("Definition"));
          PropertyField(nebulaCoarseStrandCoverage, new UnityEngine.GUIContent("Coverage"));
          PropertyField(nebulaCoarseStrandSpread, new UnityEngine.GUIContent("Spread"));
          PropertyField(nebulaCoarseStrandWarp, new UnityEngine.GUIContent("Warp"));
          PropertyField(nebulaCoarseStrandWarpScale, new UnityEngine.GUIContent("Warp Scale"));
          if (showNebulaeSeeds.value.boolValue) {
            PropertyField(nebulaCoarseStrandSeedX, new UnityEngine.GUIContent("Coarse Strand X Seed"));
            PropertyField(nebulaCoarseStrandSeedY, new UnityEngine.GUIContent("Coarse Strand Y Seed"));
            PropertyField(nebulaCoarseStrandSeedZ, new UnityEngine.GUIContent("Coarse Strand Z Seed"));
            PropertyField(nebulaCoarseStrandWarpSeedX, new UnityEngine.GUIContent("Coarse Strand Warp X Seed"));
            PropertyField(nebulaCoarseStrandWarpSeedY, new UnityEngine.GUIContent("Coarse Strand Warp Y Seed"));
            PropertyField(nebulaCoarseStrandWarpSeedZ, new UnityEngine.GUIContent("Coarse Strand Warp Z Seed"));
          }
          break;
        }
        case 3: {
          PropertyField(nebulaFineStrandBrightness, new UnityEngine.GUIContent("Intensity"));
          PropertyField(nebulaFineStrandColor, new UnityEngine.GUIContent("Color"));
          PropertyField(nebulaFineStrandScale, new UnityEngine.GUIContent("Scale"));
          PropertyField(nebulaFineStrandScaleFactor, new UnityEngine.GUIContent("Detail"));
          PropertyField(nebulaFineStrandDetailBalance, new UnityEngine.GUIContent("Detail Balance"));
          PropertyField(nebulaFineStrandOctaves, new UnityEngine.GUIContent("Octaves"));
          PropertyField(nebulaFineStrandBias, new UnityEngine.GUIContent("Bias"));
          PropertyField(nebulaFineStrandStrength, new UnityEngine.GUIContent("Strength"));
          PropertyField(nebulaFineStrandDefinition, new UnityEngine.GUIContent("Definition"));
          PropertyField(nebulaFineStrandCoverage, new UnityEngine.GUIContent("Coverage"));
          PropertyField(nebulaFineStrandSpread, new UnityEngine.GUIContent("Spread"));
          PropertyField(nebulaFineStrandWarp, new UnityEngine.GUIContent("Warp"));
          PropertyField(nebulaFineStrandWarpScale, new UnityEngine.GUIContent("Warp Scale"));
          if (showNebulaeSeeds.value.boolValue) {
            PropertyField(nebulaFineStrandSeedX, new UnityEngine.GUIContent("Fine Strand X Seed"));
            PropertyField(nebulaFineStrandSeedY, new UnityEngine.GUIContent("Fine Strand Y Seed"));
            PropertyField(nebulaFineStrandSeedZ, new UnityEngine.GUIContent("Fine Strand Z Seed"));
            PropertyField(nebulaFineStrandWarpSeedX, new UnityEngine.GUIContent("Fine Strand Warp X Seed"));
            PropertyField(nebulaFineStrandWarpSeedY, new UnityEngine.GUIContent("Fine Strand Warp Y Seed"));
            PropertyField(nebulaFineStrandWarpSeedZ, new UnityEngine.GUIContent("Fine Strand Warp Z Seed"));
          }
          break;
        }
        default: {
          /* Show an error. */
          EditorGUILayout.LabelField("ERROR: Invalid nebula layer selected. If you're a user, this error is not your fault.", subtitleStyle);
          break;
        }
      }
    } else {
      /* Nebulae is a texture. */
      PropertyField(nebulaeTexture);
      PropertyField(nebulaOverallIntensity, new UnityEngine.GUIContent("Intensity"));
      PropertyField(starNebulaFollowAmount, new UnityEngine.GUIContent("Star Follow Amount"));
      PropertyField(starNebulaFollowSpread, new UnityEngine.GUIContent("Star Follow Spread"));
    }
  } else {
    /* Texture sky controls. */
    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Sky and Stars", subtitleStyle);
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
    EditorGUILayout.LabelField("Star Twinkle", subtitleStyle);
    PropertyField(useTwinkle, new UnityEngine.GUIContent("Star Twinkle"));
    if (useTwinkle.value.boolValue) {
      PropertyField(twinkleThreshold, new UnityEngine.GUIContent("Threshold"));
      PropertyField(twinkleFrequencyRange, new UnityEngine.GUIContent("Frequency Range"));
      PropertyField(twinkleBias, new UnityEngine.GUIContent("Bias"));
      PropertyField(twinkleSmoothAmplitude, new UnityEngine.GUIContent("Smooth Intensity"));
      PropertyField(twinkleChaoticAmplitude, new UnityEngine.GUIContent("Chaotic Intensity"));
    }

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Light Pollution", subtitleStyle);
    PropertyField(lightPollutionTint, new UnityEngine.GUIContent("Tint"));
    PropertyField(lightPollutionIntensity, new UnityEngine.GUIContent("Intensity"));
  }
}

private void aerialPerspective(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Aerial Perspective", titleStyle);
  PropertyField(aerialPerspectiveOcclusionPowerUniform, new UnityEngine.GUIContent("Uniform Occlusion Spread"));
  PropertyField(aerialPerspectiveOcclusionBiasUniform, new UnityEngine.GUIContent("Uniform Occlusion Bias"));
  PropertyField(aerialPerspectiveOcclusionPowerDirectional, new UnityEngine.GUIContent("Directional Occlusion Spread"));
  PropertyField(aerialPerspectiveOcclusionBiasDirectional, new UnityEngine.GUIContent("Directional Occlusion Bias"));
  PropertyField(aerialPerspectiveNightScatteringMultiplier, new UnityEngine.GUIContent("Night Scattering Multiplier"));
}


private void quality(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Quality", titleStyle);
  /* Texture quality selection dropdown. */
  PropertyField(skyTextureQuality, new UnityEngine.GUIContent("Texture Quality"));
  PropertyField(numberOfTransmittanceSamples, new UnityEngine.GUIContent("Transmittance Samples"));
  PropertyField(numberOfSingleScatteringSamples, new UnityEngine.GUIContent("Single Scattering Samples"));
  PropertyField(numberOfMultipleScatteringSamples, new UnityEngine.GUIContent("Multiple Scattering Samples"));
  PropertyField(numberOfMultipleScatteringAccumulationSamples, new UnityEngine.GUIContent("Multiple Scattering Accumulation Samples"));
  PropertyField(numberOfAerialPerspectiveSamples, new UnityEngine.GUIContent("Aerial Perspective Samples"));
  PropertyField(numberOfGroundIrradianceSamples, new UnityEngine.GUIContent("Ground Irradiance Samples"));
  PropertyField(useImportanceSampling, new UnityEngine.GUIContent("Importance Sampling"));
  PropertyField(aerialPerspectiveUseImportanceSampling, new UnityEngine.GUIContent("Aerial Perspective Importance Sampling"));
  PropertyField(aerialPerspectiveDepthSkew, new UnityEngine.GUIContent("Aerial Perspective Depth Skew"));
  PropertyField(useAntiAliasing, new UnityEngine.GUIContent("Anti-Aliasing"));
  PropertyField(useDither, new UnityEngine.GUIContent("Dither"));
}

private void cloudLighting(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Lighting", titleStyle);
}

private void cloudNoise(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Noise", titleStyle);
}

private void cloudMovement(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Movement", titleStyle);
}

private void cloudGeometry(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Geometry", titleStyle);
}

private void cloudSampling(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Sampling", titleStyle);
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
  numberOfGroundIrradianceSamples = Unpack(o.Find(x => x.numberOfGroundIrradianceSamples));
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
  /* Lighting. TODO */
  /* TODO: density control goes here. */

  /* Noise generation. TODO */

  /* Movement---sampling offsets primarily. TODO */

  /* Geometry. TODO */

  /* Sampling. TODO */
  /* TODO: debug goes here. */
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
