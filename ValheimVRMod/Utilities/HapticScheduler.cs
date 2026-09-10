using System;
using System.Collections.Generic;
using System.Linq;

namespace ValheimVRMod.Utilities
{
    // Ticked on Unity's main thread. One entry owns each repeating effect and its delayed stop.
    internal sealed class HapticScheduler
    {
        private sealed class Effect
        {
            public float Intensity, Duration;
            public double Interval, NextPlayback;
            public double StopAt = double.PositiveInfinity;
        }

        private readonly Dictionary<string, Effect> effects = new Dictionary<string, Effect>();
        private readonly Action<string, float, float> play;
        private readonly Action<string> stop;

        public HapticScheduler(Action<string, float, float> play, Action<string> stop)
        {
            this.play = play;
            this.stop = stop;
        }

        public void Start(string name, float intensity, float duration, double interval, double delay, double now)
        {
            if (!effects.TryGetValue(name, out var effect))
            {
                effect = new Effect { NextPlayback = now + Math.Max(0, delay) };
                effects.Add(name, effect);
            }
            effect.Intensity = intensity;
            effect.Duration = duration;
            effect.Interval = interval > 0 ? interval : 1;
            effect.StopAt = double.PositiveInfinity;
        }

        public void Stop(string name, string[] callbacks = null)
        {
            if (!effects.Remove(name)) return;
            stop(name);
            if (callbacks == null) return;
            foreach (var callback in callbacks) play(callback, 1, 1);
        }

        public void StopAfter(string name, double delay, double now)
        {
            if (effects.TryGetValue(name, out var effect))
                effect.StopAt = Math.Min(effect.StopAt, now + Math.Max(0, delay));
        }

        public void StopAll(string[] exceptions = null)
        {
            foreach (var name in effects.Keys.ToArray())
                if (exceptions == null || !exceptions.Contains(name)) Stop(name);
        }

        public void Tick(double now)
        {
            foreach (var entry in effects.ToArray())
            {
                var effect = entry.Value;
                if (now >= effect.StopAt)
                {
                    Stop(entry.Key);
                }
                else if (now >= effect.NextPlayback)
                {
                    // Never replay a backlog after a pause or a slow frame.
                    effect.NextPlayback = now + effect.Interval;
                    play(entry.Key, effect.Intensity, effect.Duration);
                }
            }
        }
    }
}
