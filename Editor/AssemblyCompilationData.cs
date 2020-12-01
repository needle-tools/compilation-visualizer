using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Needle.CompilationVisualizer
{
    [Serializable]
    public class AssemblyCompilationData : ISerializationCallbackReceiver
    {
        private static string format = "HH:mm:ss.fff";
        public override string ToString() {
            return assembly + ": " + (EndTime - StartTime) + " (from " + StartTime.ToString(format, CultureInfo.CurrentCulture) + " to " + EndTime.ToString(format, CultureInfo.CurrentCulture) + ")";
        }
                    
        public string assembly;
        public SerializableDateTime startTime;
        public SerializableDateTime endTime;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
                    
        public void OnBeforeSerialize()
        {
            startTime = StartTime;
            endTime = EndTime;
        }

        public void OnAfterDeserialize()
        {
            StartTime = startTime;
            EndTime = endTime;
        }
    }
    
    [Serializable]
    public struct SerializableDateTime
    {
        private static string format = "MM-dd-yyyy HH:mm:ss.fff";
        public string utc;
                
        public DateTime DateTime {
            get
            {
                if (DateTime.TryParseExact(utc, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime result))
                    return result;
                        
                return DateTime.Now;
            }
            set => utc = value.ToString(format, CultureInfo.InvariantCulture);
        }

        public static implicit operator SerializableDateTime(DateTime dateTime) {
            var sd = new SerializableDateTime { DateTime = dateTime };
            return sd;
        }
                
        public static implicit operator DateTime(SerializableDateTime dateTime) {
            return dateTime.DateTime;
        }
    }
}