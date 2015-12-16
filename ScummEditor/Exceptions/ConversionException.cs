namespace ScummEditor.Exceptions
{
    public class ConversionException:System.Exception
    {
        public ConversionException(){}

        public ConversionException(string message):base(message){}
        
    }   
    
    public class ImageDecodeException:System.Exception
    {
        public ImageDecodeException(){}

        public ImageDecodeException(string message) : base(message) { }
        
    }
}