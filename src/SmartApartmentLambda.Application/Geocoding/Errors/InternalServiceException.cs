namespace SmartApartmentLambda.Application.Geocoding.Errors;

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
