namespace ApiPlayground.Application.Outbound;

// Raised when the selected collection does not exist for the current owner. Another user's collection is reported
// the same way as a missing one, so the caller can return not found without revealing that it exists.
public sealed class CollectionNotFoundException : Exception
{
    public CollectionNotFoundException()
        : base("The selected collection was not found.")
    {
    }
}
