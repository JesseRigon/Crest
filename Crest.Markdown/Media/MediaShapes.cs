using Crest.DisplayManagement.Descriptors;
using Crest.Modules;

namespace Crest.Markdown.Media;

[RequireFeatures("Crest.Media")]
public class MediaShapes : ShapeTableProvider
{
    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("MarkdownBodyPart_Edit")
            .OnDisplaying(displaying =>
            {
                var editor = displaying.Shape;

                if (editor.Metadata.Type == "MarkdownBodyPart_Edit__Wysiwyg")
                {
                    editor.Metadata.Wrappers.Add("Media_Wrapper__MarkdownBodyPart");
                }
            });

        builder.Describe("MarkdownField_Edit")
            .OnDisplaying(displaying =>
            {
                var editor = displaying.Shape;

                if (editor.Metadata.Type == "MarkdownField_Edit__Wysiwyg")
                {
                    editor.Metadata.Wrappers.Add("Media_Wrapper__MarkdownBodyPart");
                }
            });

        return ValueTask.CompletedTask;
    }
}
