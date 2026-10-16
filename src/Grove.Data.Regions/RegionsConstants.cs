namespace Grove.Data.Regions;

public static class RegionsConstants
{
    public const string FeatureId = "Grove.Data.Regions";

    public static class ContentTypes
    {
        /// <summary>A party-attached geo/regional profile. Never "Tenant" - see docs/regions.md.</summary>
        public const string RegionalProfile = "GroveRegionalProfile";
    }

    public static class FieldSets
    {
        public const string RegionalProfile = "RegionalProfile";

        /// <summary>
        /// Attachable by DOWNSTREAM modules to whatever carries a context (Parties attaches
        /// it to Person and Organization). Grove never names those types.
        /// </summary>
        public const string RegionalProfileReference = "RegionalProfileReference";
    }

    public static class Routes
    {
        public const string Api = "api/grove/regions";
    }
}
