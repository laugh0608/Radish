namespace Radish.Common.HttpContextTool;

public interface ICurrentUserAccessor
{
    CurrentUser Current { get; }
}
