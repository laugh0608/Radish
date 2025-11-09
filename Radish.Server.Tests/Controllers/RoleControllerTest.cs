using JetBrains.Annotations;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Repository;
using Radish.Server.Controllers;
using Radish.Service;
using Xunit;

namespace Radish.Server.Tests.Controllers;

[TestSubject(typeof(RoleController))]
public class RoleControllerTest
{

    [Fact]
    public void GetRoleList_Test()
    {
        var roleController = new RoleController(new BaseServices<RoleVo>(new BaseRepository<RoleVo>()));
        var result = roleController.GetRoleList();
        Assert.NotNull(result);
    }
}