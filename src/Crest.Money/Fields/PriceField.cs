// Adapted from OrchardCore.Commerce's ContentFields module
// (https://github.com/OrchardCMS/OrchardCore.Commerce, MIT License,
// Copyright (c) 2018 Crest).
// The stock-razor display/edit drivers are deliberately NOT absorbed: module
// blazor-wasm UIs edit the field through the content APIs.
using Crest.Money;
using Crest.ContentManagement;

namespace Crest.Money.Fields;

public class PriceField : ContentField
{
    public Amount Amount { get; set; }
}
