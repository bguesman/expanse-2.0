using System;
using System.Globalization;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.Rendering;
using UnityEditor;

namespace ExpanseCommonNamespace {

  /* Generic enum parameter class. */
  [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
  public class DateTimeParameter : VolumeParameter<string>
  {
      public override string value
      {
          get => m_Value;
          set => m_Value = value;
      }

      /* Getter and setters that work with date times to make it easier to
       * animate the date time. */
      public DateTime getDateTime() {
        return stringToDateTime(m_Value);
      }

      public void setDateTime(DateTime dateTime) {
        m_Value = dateTimeToString(dateTime);
      }

      /* Utility to convert any valid date time to string. */
      public static string dateTimeToString(DateTime d) {
        return d.ToString("O");
      }

      /* Utility to convert any valid string to date time. */
      public static DateTime stringToDateTime(string s) {
        return DateTime.ParseExact(s, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
      }

      public DateTimeParameter(string value, bool overrideState = false)
          : base(value, overrideState)
      {

      }
  }

  [VolumeParameterDrawer(typeof(DateTimeParameter))]
  public class DateTimeParameterDrawer : VolumeParameterDrawer
  {
    Vector2 scrollPos;

    public override bool OnGUI(SerializedDataParameter parameter, GUIContent title)
    {
      var value = parameter.value;

      string dateTimeString = value.stringValue;
      DateTime dateTime = DateTimeParameter.stringToDateTime(dateTimeString);

      GUILayoutOption[] options = {GUILayout.ExpandWidth(false)};

      try {

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        EditorGUILayout.LabelField(title);
        int year = EditorGUILayout.IntField("Year", dateTime.Year, options);
        int month = EditorGUILayout.IntField("Month", dateTime.Month, options);
        int day = EditorGUILayout.IntField("Day", dateTime.Day, options);
        int hour = EditorGUILayout.IntField("Hour", dateTime.Hour, options);
        int minute = EditorGUILayout.IntField("Minute", dateTime.Minute, options);
        int second = EditorGUILayout.IntField("Second", dateTime.Second, options);
        int millisecond = EditorGUILayout.IntField("Millisecond", dateTime.Millisecond, options);
        EditorGUILayout.EndScrollView();

        DateTime newDateTime = new DateTime(year, month, day, hour, minute, second, millisecond);
        value.stringValue = DateTimeParameter.dateTimeToString(newDateTime);

      } catch(ArgumentOutOfRangeException) {
        /* Invalid time and date. Don't change the value.
         * Print a warning. */
         UnityEngine.Debug.LogWarning("Warning: date time out of range.");
      }

      return true;
    }
  }

}
