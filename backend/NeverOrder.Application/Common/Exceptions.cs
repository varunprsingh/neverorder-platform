using NeverOrder.Domain.Common;

namespace NeverOrder.Application.Common;

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string resource) : base($"{resource} was not found.")
    {
    }
}

public sealed class CheckoutValidationException : DomainException
{
    public CheckoutValidationException(string message) : base(message)
    {
    }
}
