using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Radish.Model;
using Radish.Repository;
using Radish.Server.Controllers;
using Radish.Service;
using System.Collections.Generic;
using System.Threading.Tasks;
using Radish.Model.ViewModels;
using Radish.Repository.User;
using Radish.Service.User;
using Xunit;

namespace Radish.Server.Tests.Controllers;

[TestSubject(typeof(UserController))]
public class UserControllerTest
{
    // [Fact]
    // public async Task GetUserList_ReturnsSampleUsers()
    // {
    //     var userController = new UserController(new UserService(new UserRepository()));
    //     var result = await userController.GetUserList();
    //     var okResult = Assert.IsType<OkObjectResult>(result);
    //     var users = Assert.IsAssignableFrom<List<UserVo>>(okResult.Value);
    //     Assert.NotNull(users);
    // }
}
