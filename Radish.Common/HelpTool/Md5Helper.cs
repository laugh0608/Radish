using System.Security.Cryptography;
using System.Text;

namespace Radish.Common.HelpTool;

public class Md5Helper
{
    /// <summary>
    /// 32 位 MD5 加密
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <returns>密文密码</returns>
    public static string Md5Encrypt32(string password = "")
    {
        string pwd = string.Empty;
        try
        {
            if (!string.IsNullOrEmpty(password) && !string.IsNullOrWhiteSpace(password))
            {
                // 实例化一个md5对像
                MD5 md5 = MD5.Create();
                // 加密后是一个字节类型的数组，这里要注意编码UTF8/Unicode等的选择　
                byte[] s = md5.ComputeHash(Encoding.UTF8.GetBytes(password));
                // 通过使用循环，将字节类型的数组转换为字符串，此字符串是常规字符格式化所得
                foreach (var item in s)
                {
                    // 将得到的字符串使用十六进制类型格式。格式后的字符是小写的字母，如果使用大写（X）则格式后的字符是大写字符 
                    pwd = string.Concat(pwd, item.ToString("X2"));
                }
            }
        }
        catch
        {
            throw new Exception($"错误的 Password 字符串:【{password}】");
        }

        return pwd;
    }
}