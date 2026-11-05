namespace SSGICore
{
    public struct SSGICoreSettings
    {
        public int downsample;
        public int rayCount;
        public int maxSteps;
        public int bounces;
        public int filterIterations;
        public int maxAge;
        public bool temporal;
    }

    public static class SSGICoreQuality
    {
        public static SSGICoreSettings Resolve(SSGICoreVolume v)
        {
            switch (v.quality.value)
            {
                case SSGICoreQualityPreset.Low:    return Preset(SSGICoreQualityPreset.Low);
                case SSGICoreQualityPreset.Medium: return Preset(SSGICoreQualityPreset.Medium);
                case SSGICoreQualityPreset.High:   return Preset(SSGICoreQualityPreset.High);
                default:
                    return new SSGICoreSettings
                    {
                        downsample = (int)v.resolution.value,
                        rayCount = v.rayCount.value,
                        maxSteps = v.maxSteps.value,
                        bounces = v.bounces.value,
                        filterIterations = v.filterIterations.value,
                        maxAge = v.temporalFrames.value,
                        temporal = v.temporal.value,
                    };
            }
        }

        public static SSGICoreSettings Preset(SSGICoreQualityPreset preset)
        {
            switch (preset)
            {
                case SSGICoreQualityPreset.Low:
                    return new SSGICoreSettings { downsample = 4, rayCount = 1, maxSteps = 16, bounces = 1, filterIterations = 2, maxAge = 16, temporal = true };
                case SSGICoreQualityPreset.High:
                    return new SSGICoreSettings { downsample = 2, rayCount = 2, maxSteps = 32, bounces = 3, filterIterations = 4, maxAge = 32, temporal = true };
                default:
                    return new SSGICoreSettings { downsample = 2, rayCount = 1, maxSteps = 24, bounces = 2, filterIterations = 3, maxAge = 24, temporal = true };
            }
        }
    }
}
