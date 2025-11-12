namespace Radish.Common;

public static class UtilConvert
{
    /// <summary>ObjToString</summary>
    /// <param name="thisValue"></param>
    /// <returns></returns>
    public static string ObjToString(this object thisValue)
    {
        if (thisValue is not null) return thisValue.ToString().Trim();
        return "";
    }
}