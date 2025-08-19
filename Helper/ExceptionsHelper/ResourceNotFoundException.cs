namespace DynamicForm.Helper.ExceptionsHelper;

public sealed class DuplicateResourceException : Exception
{
    public DuplicateResourceException(string message) : base(message) { }
}
