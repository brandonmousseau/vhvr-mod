using System;
using System.Collections.Generic;

namespace ValheimVRMod.Utilities
{
    internal static class CollectionExtensions
    {
        // Preserve the iteration and null handling of Valve's Util.ForEach without
        // requiring the HUD to import the interaction-system namespace.
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            if (source == null) throw new ArgumentException("Argument cannot be null.", "source");
            foreach (T value in source) action(value);
        }
    }
}
