namespace Grove.Regions;

public static class RegionsConstants
{
    public const string FeatureId = "Grove.Regions";

    public static class ContentTypes
    {
        /// <summary>A party-attached geo/regional profile. Never "Tenant" - see docs/regions.md.</summary>
        public const string RegionalProfile = "GroveRegionalProfile";
    }

    public static class Parts
    {
        public const string RegionalProfile = "GroveRegionalProfilePart";

        /// <summary>
        /// Attachable by DOWNSTREAM modules to whatever carries a context (Parties attaches
        /// it to Person and Organization). Grove never names those types.
        /// </summary>
        public const string RegionalProfileReference = "GroveRegionalProfileReferencePart";
    }

    public static class Routes
    {
        public const string Api = "api/grove/regions";
    }
}
