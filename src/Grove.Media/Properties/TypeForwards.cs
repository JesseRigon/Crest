using System.Runtime.CompilerServices;
using Grove.Media.Processing;

// These image-processing enums moved to Grove.Media.Abstractions so the public image
// processing engine contract can reference them without creating an assembly reference cycle.
// The namespace is unchanged, so source consumers are unaffected; these forwarders preserve
// binary compatibility for code compiled against Grove.Media.
[assembly: TypeForwardedTo(typeof(ResizeMode))]
[assembly: TypeForwardedTo(typeof(Format))]
