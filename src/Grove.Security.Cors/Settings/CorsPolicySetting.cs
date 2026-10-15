using System.ComponentModel.DataAnnotations;

namespace Grove.Security.Cors.Settings;

public class CorsPolicySetting
{
    [Display(Name = "Policy name", Description = "The name of the policy.", GroupName = "Details", Order = 0)]
    [Required]
    public string Name { get; set; }

    [Display(Name = "Allow any origin", Description = "Allows requests from any origin. This will bypass any origin that is configured below.", GroupName = "Origins", Order = 3)]
    public bool AllowAnyOrigin { get; set; }

    [Display(Name = "Allowed origins", Description = "One origin per line.", GroupName = "Origins", Order = 4)]
    public string[] AllowedOrigins { get; set; } = [];

    [Display(Name = "Allow any header", Description = "Allows requests with any header. This will bypass any headers that are configured below.", GroupName = "Headers", Order = 5)]
    public bool AllowAnyHeader { get; set; }

    [Display(Name = "Allowed headers", Description = "One header per line.", GroupName = "Headers", Order = 6)]
    public string[] AllowedHeaders { get; set; } = [];

    [Display(Name = "Allow any method", Description = "Allows requests with any method. This will bypass any methods that are configured below.", GroupName = "Methods", Order = 7)]
    public bool AllowAnyMethod { get; set; }

    [Display(Name = "Allowed methods", Description = "One method (POST, PUT, DELETE, GET, etc) per line.", GroupName = "Methods", Order = 8)]
    public string[] AllowedMethods { get; set; } = [];

    [Display(Name = "Allow credentials", Description = "Allows credentials to be transported in requests. This setting is not allowed in combination with Allow Any Origin.", GroupName = "Credentials", Order = 2)]
    public bool AllowCredentials { get; set; }

    [Display(Name = "Set as default policy", GroupName = "Details", Order = 1)]
    public bool IsDefaultPolicy { get; set; }

    [Display(Name = "Exposed headers", Description = "Sets response header 'Access-Control-Expose-Headers'. One header per line.", GroupName = "Exposed headers", Order = 9)]
    public string[] ExposedHeaders { get; set; } = [];
}
