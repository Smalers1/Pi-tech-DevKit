using System;
using UnityEngine;

namespace Pitech.XR.Stats
{
    public enum StatOp { Add, Subtract, Multiply, Divide, Set }

    [Serializable]
    public class StatEffect
    {
        [Tooltip("Stat key to modify (must exist in StatsConfig).")]
        public string key;
        public StatOp op = StatOp.Add;
        public float value = 0;

        public float Apply(float current)
        {
            switch (op)
            {
                case StatOp.Add: return current + value;
                case StatOp.Subtract: return current - value;
                case StatOp.Multiply: return current * value;
                case StatOp.Divide: return Mathf.Approximately(value, 0f) ? current : current / value;
                case StatOp.Set: return value;
                default: return current;
            }
        }
    }
}
