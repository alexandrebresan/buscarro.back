namespace Buscarro.Application.Exceptions;
// Infrastructure translates uniqueness only after the failed transaction has rolled back.
public sealed class UniqueConstraintException(Exception inner) : Exception("A persistence uniqueness constraint was violated.", inner);
