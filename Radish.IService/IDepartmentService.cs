namespace Radish.IService;

public interface IDepartmentService
{
    /// <summary>测试使用同事务</summary>
    /// <returns></returns>
    Task<bool> TestTranPropagationDepartment();
}