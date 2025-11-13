using System.Reflection;
using Autofac;
using Autofac.Extras.DynamicProxy;
using Radish.IRepository;
using Radish.IService;
using Radish.Repository;
using Radish.Service;

namespace Radish.Extension.AutofacExtension;

public class AutofacModuleRegister: Autofac.Module
{
    protected override void Load(ContainerBuilder builder)
    {
        var basePath = AppContext.BaseDirectory;
        var serviceDllFile = Path.Combine(basePath, "Radish.Service.dll");
        var repositoryDllFile = Path.Combine(basePath, "Radish.Repository.dll");
        
        // 注册 AOP
        var aopTypes = new List<Type>(){typeof(ServiceAop)};
        builder.RegisterType<ServiceAop>();
        // 注册服务
        builder.RegisterGeneric(typeof(BaseService<,>)).As(typeof(IBaseService<,>))
            .InstancePerDependency() // 瞬态
            .EnableInterfaceInterceptors() // AOP
            .InterceptedBy(aopTypes.ToArray()); // AOP
        // 注册仓储
        builder.RegisterGeneric(typeof(BaseRepository<>)).As(typeof(IBaseRepository<>))
            .InstancePerDependency();

        // 获取 Service.dll 程序集服务，并注册
        var assemblesServices = Assembly.LoadFrom(serviceDllFile);
        builder.RegisterAssemblyTypes(assemblesServices)
            .AsImplementedInterfaces() // 接口
            .InstancePerDependency() // 瞬态
            .PropertiesAutowired() // 属性
            .EnableInterfaceInterceptors() // AOP
            .InterceptedBy(aopTypes.ToArray()); // AOP

        // 获取 Repository.dll 程序集服务，并注册
        var assemblesRepository = Assembly.LoadFrom(repositoryDllFile);
        builder.RegisterAssemblyTypes(assemblesRepository)
            .AsImplementedInterfaces()
            .PropertiesAutowired()
            .InstancePerDependency();
        
        // 注册事务模型
        // builder.RegisterType<UnitOfWorkManage>().As<IUnitOfWorkManage>()
        //     .AsImplementedInterfaces()
        //     .InstancePerLifetimeScope()
        //     .PropertiesAutowired();
    }
}