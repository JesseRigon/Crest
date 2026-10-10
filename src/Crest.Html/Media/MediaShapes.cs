using Crest.DisplayManagement.Descriptors;
using Crest.Modules;

namespace Crest.Html.Media;

[RequireFeatures("Crest.Media")]
public class MediaShapes : ShapeTableProvider
{
    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("HtmlBodyPart_Edit")
            .OnDisplaying(displaying =>
            {
                var editor = displaying.Shape;

                if (editor.Metadata.Type == "HtmlBodyPart_Edit__Wysiwyg")
                {
                    editor.Metadata.Wrappers.Add("Media_Wrapper__HtmlBodyPart");
                }

                if (editor.Metadata.Type == "HtmlBodyPart_Edit__Trumbowyg")
                {
                    editor.Metadata.Wrappers.Add("Media_Wrapper__HtmlBodyPart");
                }
            });

        return ValueTask.CompletedTask;
    }
}
