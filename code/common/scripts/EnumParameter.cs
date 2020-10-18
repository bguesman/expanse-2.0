using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.Rendering;
using UnityEditor;

namespace ExpanseCommonNamespace {
  /* Generic enum parameter class. */
  [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
  public class EnumParameter<T> : VolumeParameter<T>
  {
      public override T value
      {
          get => m_Value;
          set => m_Value = value;
      }

      public EnumParameter(T value, bool overrideState = false)
          : base(value, overrideState)
      {

      }
  }

  /* Unfortunately, C# does not have the notion of generic Enums.
   * The best we can do is guarantee that T is convertible.
   * This is very, very jank, and if I think of a better way to
   * handle this, then I will implement that. */
  public class EnumParameterDrawer<T> : VolumeParameterDrawer where T : struct, IConvertible
  {

    public override bool OnGUI(SerializedDataParameter parameter, GUIContent title)
    {
      if (!typeof(T).IsEnum) {
        throw new ArgumentException("T must be an enumerated type");
      }

      var value = parameter.value;

      if (value.propertyType != SerializedPropertyType.Enum)
          return false;

      var o = parameter.GetObjectRef<EnumParameter<T>>();
      int v = Convert.ToInt32(EditorGUILayout.EnumPopup(Enum.Parse(typeof(T), value.enumValueIndex.ToString(), true) as System.Enum));
      value.enumValueIndex = (int) v;
      return true;
    }
  }

}
