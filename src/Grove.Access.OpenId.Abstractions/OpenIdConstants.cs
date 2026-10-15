namespace Grove.Access.OpenId;

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
        public const string Client = "Grove.Access.OpenId.Client";
        public const string Core = "Grove.Access.OpenId";
        public const string Management = "Grove.Access.OpenId.Management";
        public const string Server = "Grove.Access.OpenId.Server";
        public const string Validation = "Grove.Access.OpenId.Validation";
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
