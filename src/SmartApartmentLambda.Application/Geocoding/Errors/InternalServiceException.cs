namespace SmartApartmentLambda.Application.Geocoding;

public sealed class InternalServiceException : Exception
{
    public InternalServiceException(string message)
        : base(message)
    {
    }

    public InternalServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
