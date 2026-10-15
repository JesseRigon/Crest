using Grove.Environment.Extensions;
using Grove.Modules;

namespace ModuleSample;

public class FeatureIndependentStartup : StartupBase;

[Feature("Sample1")]
public class Sample1Startup : StartupBase;

[Feature("Sample2")]
[FeatureTypeDiscovery(SkipExtension = true)]
public class SkippedDependentType;
