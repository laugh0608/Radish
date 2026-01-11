using System.Linq.Expressions;

namespace Radish.Common;

/// <summary>表达式扩展方法</summary>
public static class ExpressionExtensions
{
    /// <summary>
    /// 连接表达式与运算（AND）
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <param name="one">原表达式</param>
    /// <param name="another">新的表达式</param>
    /// <returns>组合后的表达式</returns>
    public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> one, Expression<Func<T, bool>> another)
    {
        // 创建新参数
        var newParameter = Expression.Parameter(typeof(T), "parameter");

        var parameterReplacer = new ParameterReplaceVisitor(newParameter);
        var left = parameterReplacer.Visit(one.Body);
        var right = parameterReplacer.Visit(another.Body);
        var body = Expression.AndAlso(left, right);

        return Expression.Lambda<Func<T, bool>>(body, newParameter);
    }

    /// <summary>
    /// 连接表达式或运算（OR）
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <param name="one">原表达式</param>
    /// <param name="another">新表达式</param>
    /// <returns>组合后的表达式</returns>
    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> one, Expression<Func<T, bool>> another)
    {
        // 创建新参数
        var newParameter = Expression.Parameter(typeof(T), "parameter");

        var parameterReplacer = new ParameterReplaceVisitor(newParameter);
        var left = parameterReplacer.Visit(one.Body);
        var right = parameterReplacer.Visit(another.Body);
        var body = Expression.OrElse(left, right);

        return Expression.Lambda<Func<T, bool>>(body, newParameter);
    }

    /// <summary>
    /// 参数替换访问器（用于统一表达式树的参数）
    /// </summary>
    private class ParameterReplaceVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;

        public ParameterReplaceVisitor(ParameterExpression paramExpr)
        {
            _parameter = paramExpr;
        }

        protected override Expression VisitParameter(ParameterExpression p)
        {
            return p.Type == _parameter.Type ? _parameter : p;
        }
    }
}
