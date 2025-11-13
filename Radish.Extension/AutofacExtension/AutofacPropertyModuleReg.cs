using System.Reflection;
using Autofac;
using Microsoft.AspNetCore.Mvc;

namespace Radish.Extension.AutofacExtension
{
    public class AutofacPropertyModuleReg : Autofac.Module
    {
        private readonly Assembly _controllersAssembly;

        public AutofacPropertyModuleReg(Assembly controllersAssembly)
        {
            _controllersAssembly = controllersAssembly ?? throw new ArgumentNullException(nameof(controllersAssembly));
        }

        protected override void Load(ContainerBuilder builder)
        {
            // 记得要启动服务注册，把这一行放在 Program.cs 中:
            // builder.Services.Replace(ServiceDescriptor.Transient<IControllerActivator, ServiceBasedControllerActivator>());
            var controllerBaseType = typeof(ControllerBase);
            builder.RegisterAssemblyTypes(_controllersAssembly)
                .Where(t => controllerBaseType.IsAssignableFrom(t) && t != controllerBaseType)
                .PropertiesAutowired(); // 属性注册
        }
    }
}
