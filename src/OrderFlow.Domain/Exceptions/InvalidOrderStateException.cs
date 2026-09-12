namespace OrderFlow.Domain.Exceptions;

public sealed class InvalidOrderStateException : Exception
{
    public InvalidOrderStateException(string message)
        : base(message)
    {
    }
}