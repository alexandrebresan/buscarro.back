namespace Buscarro.Application.Exceptions;

public sealed class TransactionCommitOutcomeUnknownException : Exception
{
    public TransactionCommitOutcomeUnknownException(Exception innerException)
        : base(
            "The transaction commit outcome is unknown. Reconcile persisted state using the operation idempotency key before retrying.",
            innerException)
    {
    }
}
