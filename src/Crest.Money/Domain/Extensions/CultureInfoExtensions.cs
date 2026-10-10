// Adapted from OrchardCore.Commerce's MoneyDataType library
// (https://github.com/OrchardCMS/OrchardCore.Commerce, MIT License,
// Copyright (c) 2018 Crest).
﻿namespace System.Globalization;

public static class CultureInfoExtensions
{
    public static RegionInfo TryGetRegionInfo(this CultureInfo cultureInfo)
    {
        try
        {
            return new RegionInfo(cultureInfo.Name);
        }
        catch
        {
            return null;
        }
    }
}
