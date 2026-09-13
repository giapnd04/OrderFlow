namespace OrderFlow.Domain.Exceptions;

public sealed class InsufficientStockException : Exception
{
    public InsufficientStockException(string message)
        : base(message)
    {
    }
}
