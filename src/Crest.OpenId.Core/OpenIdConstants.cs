namespace Crest.OpenId;

public static class OpenIdConstants
{
    public static class Claims
    {
        public const string EntityType = "oc:entyp";
    }

    public static class EntityTypes
    {
        public const string Application = "application";
        public const string User = "user";
    }

    public static class Features
    {
        public const string Client = "Crest.OpenId.Client";
        public const string Core = "Crest.OpenId";
        public const string Management = "Crest.OpenId.Management";
        public const string Server = "Crest.OpenId.Server";
        public const string Validation = "Crest.OpenId.Validation";
    }

    public static class Prefixes
    {
        public const string Tenant = "oct:";
    }

    public static class Properties
    {
        public const string Roles = "Roles";
    }
}
