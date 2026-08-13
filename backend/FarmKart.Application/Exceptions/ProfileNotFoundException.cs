using System;

namespace FarmKart.Application.Exceptions;

public class ProfileNotFoundException : Exception
{
    public ProfileNotFoundException() : base("Farmer profile not found.")
    {
    }

    public ProfileNotFoundException(string message) : base(message)
    {
    }
}
