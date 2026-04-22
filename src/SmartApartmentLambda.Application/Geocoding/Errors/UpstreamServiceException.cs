namespace SmartApartmentLambda.Application.Geocoding.Errors;

public sealed class UpstreamServiceException : Exception
{
    public UpstreamServiceException(string message)
        : base(message)
    {
    }

    public UpstreamServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
