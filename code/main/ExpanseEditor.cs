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
SerializedDataParameter[] layerUseDensityAttenuation
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
SerializedDataParameter[] bodyDirection
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

/* Night Sky. TODO */
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
/* Nebulae. */
SerializedDataParameter useProceduralNebulae;
SerializedDataParameter nebulaeTextureQuality;
SerializedDataParameter nebulaeTexture;

/* Regular. */
SerializedDataParameter lightPollutionTint;
SerializedDataParameter lightPollutionIntensity;
SerializedDataParameter nightSkyTexture;
SerializedDataParameter nightSkyRotation;
SerializedDataParameter nightSkyTint;
SerializedDataParameter nightSkyIntensity;
SerializedDataParameter nightSkyScatterTint;
SerializedDataParameter nightSkyScatterIntensity;
SerializedDataParameter useTwinkle;
SerializedDataParameter twinkleThreshold;
SerializedDataParameter twinkleFrequencyRange;
SerializedDataParameter twinkleBias;
SerializedDataParameter twinkleSmoothAmplitude;
SerializedDataParameter twinkleChaoticAmplitude;

/* Aerial Perspective. */
SerializedDataParameter aerialPerspectiveTableDistances;
SerializedDataParameter aerialPerspectiveOcclusionPowerUniform;
SerializedDataParameter aerialPerspectiveOcclusionBiasUniform;
SerializedDataParameter aerialPerspectiveOcclusionPowerDirectional;
SerializedDataParameter aerialPerspectiveOcclusionBiasDirectional;

/* Quality parameters. */
SerializedDataParameter skyTextureQuality;
SerializedDataParameter numberOfTransmittanceSamples;
SerializedDataParameter numberOfLightPollutionSamples;
SerializedDataParameter numberOfSingleScatteringSamples;
SerializedDataParameter numberOfGroundIrradianceSamples;
SerializedDataParameter numberOfMultipleScatteringSamples;
SerializedDataParameter numberOfMultipleScatteringAccumulationSamples;
SerializedDataParameter useImportanceSampling;
SerializedDataParameter useAntiAliasing;
SerializedDataParameter ditherAmount;

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

    /* Phase function selection dropdown. */
    PropertyField(layerPhaseFunction[atmosphereSelectIndex], new UnityEngine.GUIContent("Layer Phase Function"));
    if ((ExpanseCommon.PhaseFunction) layerPhaseFunction[atmosphereSelectIndex].value.enumValueIndex == ExpanseCommon.PhaseFunction.Mie) {
      /* Only display anisotropy control if Mie scattering is enabled. */
      PropertyField(layerAnisotropy[atmosphereSelectIndex], new UnityEngine.GUIContent("Anisotropy"));
    }

    PropertyField(layerUseDensityAttenuation[atmosphereSelectIndex], new UnityEngine.GUIContent("Density Attenuation"));
    if (layerUseDensityAttenuation[atmosphereSelectIndex].value.boolValue) {
      /* Only display density attenuation parameters if we use density attenuation. */
      PropertyField(layerAttenuationDistance[atmosphereSelectIndex], new UnityEngine.GUIContent("Attenuation Distance"));
      PropertyField(layerAttenuationBias[atmosphereSelectIndex], new UnityEngine.GUIContent("Attenuation Bias"));
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
    PropertyField(bodyDirection[bodySelectIndex], new UnityEngine.GUIContent("Direction"));
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
    PropertyField(nightSkyRotation);
    PropertyField(nightSkyIntensity);
    PropertyField(nightSkyTint);
    PropertyField(nightSkyScatterIntensity);
    PropertyField(nightSkyScatterTint);

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Stars", subtitleStyle);
    PropertyField(starTextureQuality);
    PropertyField(showStarSeeds);
    if (showStarSeeds.value.boolValue) {
      PropertyField(useHighDensityMode);
      PropertyField(starDensity);
      PropertyField(starDensitySeed);
      PropertyField(starSizeRange);
      PropertyField(starSizeBias);
      PropertyField(starSizeSeed);
      PropertyField(starIntensityRange);
      PropertyField(starIntensityBias);
      PropertyField(starIntensitySeed);
      PropertyField(starTemperatureRange);
      PropertyField(starTemperatureBias);
      PropertyField(starTemperatureSeed);
    } else {
      PropertyField(useHighDensityMode);
      PropertyField(starDensity);
      PropertyField(starSizeRange);
      PropertyField(starSizeBias);
      PropertyField(starIntensityRange);
      PropertyField(starIntensityBias);
      PropertyField(starTemperatureRange);
      PropertyField(starTemperatureBias);
    }

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Twinkle Effect", subtitleStyle);
    PropertyField(useTwinkle);
    if (useTwinkle.value.boolValue) {
      PropertyField(twinkleThreshold);
      PropertyField(twinkleFrequencyRange);
      PropertyField(twinkleBias);
      PropertyField(twinkleSmoothAmplitude, new UnityEngine.GUIContent("Smooth Twinkle Intensity"));
      PropertyField(twinkleChaoticAmplitude, new UnityEngine.GUIContent("Chaotic Twinkle Intensity"));
    }

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Nebulae", subtitleStyle);
    PropertyField(useProceduralNebulae);
    if (useProceduralNebulae.value.boolValue) {
      /* Nebulae are procedural. */
      PropertyField(nebulaeTextureQuality);
    } else {
      /* Nebulae is a texture. */
      PropertyField(nebulaeTexture);
    }


  } else {
    /* Texture sky controls. */
    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Sky and Stars", subtitleStyle);
    PropertyField(nightSkyTexture);
    if (nightSkyTexture.value.objectReferenceValue != null) {
      PropertyField(nightSkyRotation);
    }
    PropertyField(nightSkyIntensity);
    PropertyField(nightSkyTint);
    PropertyField(nightSkyScatterIntensity);
    PropertyField(nightSkyScatterTint);

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Twinkle Effect", subtitleStyle);
    PropertyField(useTwinkle);
    if (useTwinkle.value.boolValue) {
      PropertyField(twinkleThreshold);
      PropertyField(twinkleFrequencyRange);
      PropertyField(twinkleBias);
      PropertyField(twinkleSmoothAmplitude, new UnityEngine.GUIContent("Smooth Twinkle Intensity"));
      PropertyField(twinkleChaoticAmplitude, new UnityEngine.GUIContent("Chaotic Twinkle Intensity"));
    }
  }

  EditorGUILayout.Space();
  EditorGUILayout.LabelField("Light Pollution", subtitleStyle);
  PropertyField(lightPollutionTint);
  PropertyField(lightPollutionIntensity);
}

private void aerialPerspective(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Aerial Perspective", titleStyle);
  PropertyField(aerialPerspectiveTableDistances, new UnityEngine.GUIContent("LOD Distances"));
  PropertyField(aerialPerspectiveOcclusionPowerUniform, new UnityEngine.GUIContent("Uniform Occlusion Spread"));
  PropertyField(aerialPerspectiveOcclusionBiasUniform, new UnityEngine.GUIContent("Uniform Occlusion Bias"));
  PropertyField(aerialPerspectiveOcclusionPowerDirectional, new UnityEngine.GUIContent("Directional Occlusion Spread"));
  PropertyField(aerialPerspectiveOcclusionBiasDirectional, new UnityEngine.GUIContent("Directional Occlusion Bias"));
}


private void quality(UnityEngine.GUIStyle titleStyle, UnityEngine.GUIStyle subtitleStyle) {
  EditorGUILayout.LabelField("Quality", titleStyle);
  /* Texture quality selection dropdown. */
  PropertyField(skyTextureQuality, new UnityEngine.GUIContent("Texture Quality"));
  PropertyField(numberOfTransmittanceSamples, new UnityEngine.GUIContent("Transmittance Samples"));
  PropertyField(numberOfLightPollutionSamples, new UnityEngine.GUIContent("Light Pollution Samples"));
  PropertyField(numberOfSingleScatteringSamples, new UnityEngine.GUIContent("Single Scattering Samples"));
  PropertyField(numberOfGroundIrradianceSamples, new UnityEngine.GUIContent("Ground Irradiance Samples"));
  PropertyField(numberOfMultipleScatteringSamples, new UnityEngine.GUIContent("Multiple Scattering Samples"));
  PropertyField(numberOfMultipleScatteringAccumulationSamples, new UnityEngine.GUIContent("Multiple Scattering Accumulation Samples"));
  PropertyField(useImportanceSampling, new UnityEngine.GUIContent("Importance Sampling"));
  PropertyField(useAntiAliasing, new UnityEngine.GUIContent("Anti-Aliasing"));
  PropertyField(ditherAmount);
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
    layerUseDensityAttenuation[i] = Unpack(o.Find("layerUseDensityAttenuation" + i));
    layerAttenuationDistance[i] = Unpack(o.Find("layerAttenuationDistance" + i));
    layerAttenuationBias[i] = Unpack(o.Find("layerAttenuationBias" + i));
    layerTint[i] = Unpack(o.Find("layerTint" + i));
    layerMultipleScatteringMultiplier[i] = Unpack(o.Find("layerMultipleScatteringMultiplier" + i));
  }

  /* Celestial bodies. */
  for (int i = 0; i < ExpanseCommon.kMaxAtmosphereLayers; i++) {
    bodyEnabled[i] = Unpack(o.Find("bodyEnabled" + i));
    bodyDirection[i] = Unpack(o.Find("bodyDirection" + i));
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

  /* Night Sky. TODO */
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
  /* Nebulae. */
  useProceduralNebulae = Unpack(o.Find(x => x.useProceduralNebulae));
  nebulaeTextureQuality = Unpack(o.Find(x => x.nebulaeTextureQuality));
  nebulaeTexture = Unpack(o.Find(x => x.nebulaeTexture));

  /* Regular. */
  lightPollutionTint = Unpack(o.Find(x => x.lightPollutionTint));
  lightPollutionIntensity = Unpack(o.Find(x => x.lightPollutionIntensity));
  nightSkyTexture = Unpack(o.Find(x => x.nightSkyTexture));
  nightSkyRotation = Unpack(o.Find(x => x.nightSkyRotation));
  nightSkyTint = Unpack(o.Find(x => x.nightSkyTint));
  nightSkyIntensity = Unpack(o.Find(x => x.nightSkyIntensity));
  nightSkyScatterTint = Unpack(o.Find(x => x.nightSkyScatterTint));
  nightSkyScatterIntensity = Unpack(o.Find(x => x.nightSkyScatterIntensity));
  useTwinkle = Unpack(o.Find(x => x.useTwinkle));
  twinkleThreshold = Unpack(o.Find(x => x.twinkleThreshold));
  twinkleFrequencyRange = Unpack(o.Find(x => x.twinkleFrequencyRange));
  twinkleBias = Unpack(o.Find(x => x.twinkleBias));
  twinkleSmoothAmplitude = Unpack(o.Find(x => x.twinkleSmoothAmplitude));
  twinkleChaoticAmplitude = Unpack(o.Find(x => x.twinkleChaoticAmplitude));

  /* Aerial perspective. */
  aerialPerspectiveTableDistances = Unpack(o.Find(x => x.aerialPerspectiveTableDistances));
  aerialPerspectiveOcclusionPowerUniform = Unpack(o.Find(x => x.aerialPerspectiveOcclusionPowerUniform));
  aerialPerspectiveOcclusionBiasUniform = Unpack(o.Find(x => x.aerialPerspectiveOcclusionBiasUniform));
  aerialPerspectiveOcclusionPowerDirectional = Unpack(o.Find(x => x.aerialPerspectiveOcclusionPowerDirectional));
  aerialPerspectiveOcclusionBiasDirectional = Unpack(o.Find(x => x.aerialPerspectiveOcclusionBiasDirectional));

  /* Quality. */
  skyTextureQuality = Unpack(o.Find(x => x.skyTextureQuality));
  numberOfTransmittanceSamples = Unpack(o.Find(x => x.numberOfTransmittanceSamples));
  numberOfLightPollutionSamples = Unpack(o.Find(x => x.numberOfLightPollutionSamples));
  numberOfSingleScatteringSamples = Unpack(o.Find(x => x.numberOfSingleScatteringSamples));
  numberOfGroundIrradianceSamples = Unpack(o.Find(x => x.numberOfGroundIrradianceSamples));
  numberOfMultipleScatteringSamples = Unpack(o.Find(x => x.numberOfMultipleScatteringSamples));
  numberOfMultipleScatteringAccumulationSamples = Unpack(o.Find(x => x.numberOfMultipleScatteringAccumulationSamples));
  useImportanceSampling = Unpack(o.Find(x => x.useImportanceSampling));
  useAntiAliasing = Unpack(o.Find(x => x.useAntiAliasing));
  ditherAmount = Unpack(o.Find(x => x.ditherAmount));

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
