using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ExpanseCommonNamespace;

[ExecuteInEditMode]
public class ExpanseLightControl : MonoBehaviour
{

  public int bodyIndex;

  // Start is called before the first frame update
  void Start() {}

  // Update is called once per fram3
  void Update() {
    /* Get the light. */
    UnityEngine.Light light = gameObject.GetComponent(typeof(UnityEngine.Light)) as UnityEngine.Light;

    /* Get the sky. */
    GameObject skyFogVolume = GameObject.FindWithTag("ExpanseSkyAndFogVolume");
    UnityEngine.Rendering.Volume volume = skyFogVolume.GetComponent<UnityEngine.Rendering.Volume>();
    Expanse sky;
    if( !volume.profile.TryGet<Expanse>( out sky ) ) {
      Debug.Log("Expanse Light Control: failed to get sky. Is your sky volume tagged as ExpanseSkyAndFogVolume?");
      return;
    }

    int i = bodyIndex;
    Vector3 bodyDirection = ((Vector3Parameter) sky.GetType().GetField("bodyDirection" + i).GetValue(sky)).value;
    gameObject.transform.localRotation = Quaternion.Euler(bodyDirection.x,
                                      bodyDirection.y,
                                      bodyDirection.z);

    /* Compute and set light color. */
    bool useTemperature = ((BoolParameter) sky.GetType().GetField("bodyUseTemperature" + i).GetValue(sky)).value;
    float lightIntensity = ((FloatParameter) sky.GetType().GetField("bodyLightIntensity" + i).GetValue(sky)).value;
    Vector4 lightColor = ((ColorParameter) sky.GetType().GetField("bodyLightColor" + i).GetValue(sky)).value;
    if (useTemperature) {
      float temperature = ((FloatParameter) sky.GetType().GetField("bodyLightTemperature" + i).GetValue(sky)).value;
      Vector4 temperatureColor = ExpanseCommon.blackbodyTempToColor(temperature);
      light.color = (new Vector4(temperatureColor.x * lightColor.x,
        temperatureColor.y * lightColor.y,
        temperatureColor.z * lightColor.z,
        temperatureColor.w * lightColor.w));
    } else {
      light.color = lightColor;
    }

    Vector3 transmittance = ExpanseCommon.bodyTransmittances[bodyIndex];
    Vector4 transmittanceV4 = new Vector4(transmittance.x, transmittance.y, transmittance.z, 1);
    light.color = light.color * transmittanceV4;

    light.intensity = lightIntensity;
  }
}
