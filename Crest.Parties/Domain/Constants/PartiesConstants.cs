namespace Crest.Parties.Constants;

public static class PartiesConstants
{
    public const string FeatureId = "Crest.Parties";

    public static class Routes
    {
        /// <summary>All Parties: the Parties menu's own page, doubling as the management page.</summary>
        public const string AllPartiesAdmin = "/Parties";

        /// <summary>One page per tenant-kind party type, keyed by the descriptor's slug.</summary>
        public const string PartyTypeAdminTemplate = "/Parties/{typeKey}";
        public static string PartyTypeAdmin(string typeKey) => $"/Parties/{typeKey}";

        public const string Api = "api/crest/parties";
        public const string Types = "types";
    }

    /// <summary>Parties' own party types: the two base parties.</summary>
    public static class PartyTypeKeys
    {
        public const string Contacts = "contacts";
        public const string Organizations = "organizations";
    }

    public static class ContentTypes
    {
        public const string Person = "Person";
        public const string Organization = "Organization";

        /// <summary>Bag-contained element types: they exist only inside a party's
        /// ContactPoints / Addresses bags, never standalone.</summary>
        public const string ContactPoint = "ContactPoint";
        public const string Address = "Address";
        public const string OrgPosition = "OrgPosition";

        /// <summary>
        /// A place the TENANT operates from - warehouse, store, office, dock. Holds
        /// physical fact only (addresses, labelled points, floor area, storeys); tax
        /// apportionment is a part the tax module attaches (plans/regions-and-locations.md).
        /// </summary>
        public const string Location = "Location";

        /// <summary>Bag-contained in a Location's Points bag: a register, a dock, an entrance, a storey - each with its own geo stack.</summary>
        public const string LocationPoint = "LocationPoint";
    }

    /// <summary>Named BagParts every party type carries.</summary>
    public static class Bags
    {
        public const string ContactPoints = "ContactPoints";
        public const string Addresses = "Addresses";

        /// <summary>Person only: the organizations a person holds positions in.</summary>
        public const string Positions = "Positions";

        /// <summary>Location only: its labelled points.</summary>
        public const string Points = "Points";
    }
}
