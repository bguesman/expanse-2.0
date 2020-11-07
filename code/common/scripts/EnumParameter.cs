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
}
