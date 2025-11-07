using Volo.Abp.Reflection;

namespace ComToHtri.Permissions;

public class ComToHtriPermissions
{
    public const string GroupName = "ComToHtri";

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(ComToHtriPermissions));
    }
}
