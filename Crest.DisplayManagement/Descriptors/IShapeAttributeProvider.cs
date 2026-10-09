using Crest.Environment.Extensions;

namespace Crest.DisplayManagement.Descriptors;

/// <summary>
/// Represents a marker interface for classes that have Shape methods tagged with <see cref="ShapeAttribute"/>.
/// </summary>
[FeatureTypeDiscovery(SkipExtension = true)]
public interface IShapeAttributeProvider;
