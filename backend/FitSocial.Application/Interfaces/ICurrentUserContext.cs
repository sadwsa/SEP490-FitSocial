using System;

namespace FitSocial.Application.Interfaces;

public interface ICurrentUserContext
{
    Guid? UserId { get; }
    string? Role { get; }
    string? IpAddress { get; }
}
