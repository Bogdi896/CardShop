namespace CardShop.Application.Common.Exceptions;

public class CartException : Exception
{
    public CartException(string message) : base(message)
    {
    }
}