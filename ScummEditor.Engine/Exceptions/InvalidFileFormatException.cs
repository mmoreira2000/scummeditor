namespace ScummEditor.Exceptions
{
    public class InvalidFileFormatException:System.Exception
    {
        public InvalidFileFormatException(){}

        public InvalidFileFormatException(string message) : base(message) { }
    }
}