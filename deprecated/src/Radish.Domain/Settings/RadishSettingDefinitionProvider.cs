using Volo.Abp.Settings;

namespace Radish.Settings;

public class RadishSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(RadishSettings.MySetting1));
    }
}
