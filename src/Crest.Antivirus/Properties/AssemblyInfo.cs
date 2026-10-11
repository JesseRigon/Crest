using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Crest.Tests")]

// The upload quarantine wraps the ClamAV daemon connection as an asynchronous scanner (docs/media.md § 2).
[assembly: InternalsVisibleTo("Crest.Media.Scanning")]
