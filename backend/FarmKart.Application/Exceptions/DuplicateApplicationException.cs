using System;

namespace FarmKart.Application.Exceptions;

public class DuplicateApplicationException : Exception
{
    public DuplicateApplicationException() : base("You have already applied to this job.")
    {
    }

    public DuplicateApplicationException(string message) : base(message)
    {
    }
}
