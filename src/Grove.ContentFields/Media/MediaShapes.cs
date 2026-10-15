using Grove.DisplayManagement.Descriptors;
using Grove.Modules;

namespace Grove.ContentFields.Media;

[RequireFeatures("Grove.Media")]
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
