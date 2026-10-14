namespace CardShop.Application.Common.Exceptions;

public class OrderException : Exception
{
    public OrderException(string message) : base(message)
    {
    }
}