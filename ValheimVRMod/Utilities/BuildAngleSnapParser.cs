using System.Collections.Generic;
using System.Globalization;

namespace ValheimVRMod.Utilities
{
    internal static class BuildAngleSnapParser
    {
        internal static bool TryParse(string value, out float[] angles)
        {
            angles = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var parsed = new List<float>();
            foreach (var token in value.Split(','))
            {
                if (!float.TryParse(token.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var angle)
                    || float.IsNaN(angle) || float.IsInfinity(angle) || angle <= 0 || angle > 360)
                    return false;
                parsed.Add(angle);
            }
            angles = parsed.ToArray();
            return true;
        }
    }
}
