namespace OrderFlow.Application.Exceptions;

/// <summary>The caller is authenticated but not allowed to do this. Maps to HTTP 403.</summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message)
        : base(message)
    {
    }
}
