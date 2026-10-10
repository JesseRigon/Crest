using Crest.DisplayManagement.Descriptors;
using Crest.Modules;

namespace Crest.ContentFields.Media;

[RequireFeatures("Crest.Media")]
public class MediaShapes : ShapeTableProvider
{
    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("HtmlField_Edit")
            .OnDisplaying(displaying =>
            {
                var editor = displaying.Shape;

                if (editor.Metadata.Type == "HtmlField_Edit__Wysiwyg")
                {
                    editor.Metadata.Wrappers.Add("Media_Wrapper__HtmlField");
                }

                if (editor.Metadata.Type == "HtmlField_Edit__Trumbowyg")
                {
                    editor.Metadata.Wrappers.Add("Media_Wrapper__HtmlField");
                }
            });

        return ValueTask.CompletedTask;
    }
}
