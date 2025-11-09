using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Radish.Model;
using Radish.Repository;
using Radish.Server.Controllers;
using Radish.Service;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Radish.Server.Tests.Controllers;

[TestSubject(typeof(UserController))]
public class UserControllerTest
{
    [Fact]
    public async Task GetUserList_ReturnsSampleUsers()
    {
        // 直接串联示例仓储与服务，确保 UserController 的演示接口可用。
        var userController = new UserController(new UserService(new UserRepository()));

        var result = await userController.GetUserList();
        var okResult = Assert.IsType<OkObjectResult>(result);
        var users = Assert.IsAssignableFrom<List<UserVo>>(okResult.Value);

        // 文档示例依赖于至少返回两个演示用户。
        Assert.True(users.Count >= 2);
    }
}
