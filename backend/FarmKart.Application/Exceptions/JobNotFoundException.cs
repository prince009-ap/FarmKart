namespace FarmKart.Application.Exceptions;

public sealed class JobNotFoundException : Exception
{
    public JobNotFoundException() : base("Job not found.") { }
}
