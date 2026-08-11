using System;

namespace FarmKart.Application.Exceptions;

public class RegistrationFailedException : Exception
{
    public RegistrationFailedException(string message) : base(message)
    {
    }
}
