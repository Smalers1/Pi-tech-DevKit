using TMPro;
using UnityEngine;

namespace Pitech.XR.Analytics
{
    /// <summary>One row of the two-tab readout: a left label + a right value. Put this on the template row
    /// GameObject and assign the two TMP labels; <see cref="SessionReadoutView"/> clones + fills it per line.
    /// In its own file (matching the class name) so Unity can attach it via Add Component.</summary>
    [AddComponentMenu("Pi tech/Analytics/Readout Row")]
    public sealed class ReadoutRow : MonoBehaviour
    {
        public TMP_Text left;
        public TMP_Text right;

        public void Set(string leftText, string rightText, Color color)
        {
            if (left) { left.text = leftText; left.color = color; }
            if (right) { right.text = rightText; right.color = color; }
        }
    }
}
