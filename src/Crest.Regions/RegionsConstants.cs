namespace Crest.Regions;

public static class RegionsConstants
{
    public const string FeatureId = "Crest.Regions";

    public static class ContentTypes
    {
        /// <summary>A party-attached geo/regional profile. Never "Tenant" - see docs/regions.md.</summary>
        public const string RegionalProfile = "CrestRegionalProfile";
    }

    public static class Parts
    {
        public const string RegionalProfile = "CrestRegionalProfilePart";

        /// <summary>
        /// Attachable by DOWNSTREAM modules to whatever carries a context (Parties attaches
        /// it to Person and Organization). Crest never names those types.
        /// </summary>
        public const string RegionalProfileReference = "CrestRegionalProfileReferencePart";
    }

    public static class Routes
    {
        public const string Api = "api/crest/regions";
    }
}
