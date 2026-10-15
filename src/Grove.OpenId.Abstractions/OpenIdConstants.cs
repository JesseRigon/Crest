namespace Grove.OpenId;

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
        public const string Client = "Grove.OpenId.Client";
        public const string Core = "Grove.OpenId";
        public const string Management = "Grove.OpenId.Management";
        public const string Server = "Grove.OpenId.Server";
        public const string Validation = "Grove.OpenId.Validation";
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
