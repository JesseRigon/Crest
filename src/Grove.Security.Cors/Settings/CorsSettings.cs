using System.ComponentModel.DataAnnotations;

namespace Grove.Security.Cors.Settings;

public class CorsSettings
{
    /// <summary>The settings group of the CORS policies.</summary>
    public const string GroupId = "cors";

    [Display(Name = "Policies", Description = "The CORS policies. A policy name is required and unique; at most one policy is the default one (the first policy otherwise).")]
    public IEnumerable<CorsPolicySetting> Policies { get; set; }
}
