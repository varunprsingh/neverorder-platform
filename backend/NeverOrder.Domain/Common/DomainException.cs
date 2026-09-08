namespace NeverOrder.Domain.Common;

/// <summary>Base for rule violations that callers are expected to handle, not bugs.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}
