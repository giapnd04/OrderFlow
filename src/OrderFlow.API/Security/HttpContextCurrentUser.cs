using System.Security.Claims;
using OrderFlow.Application.Abstractions.Security;
using OrderFlow.Domain.Enums;
using OrderFlow.Infrastructure.Authentication;

namespace OrderFlow.API.Security;

/// <summary>Reads the caller's identity from the validated JWT claims on the current request.</summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public int? UserId => ParseInt(Principal?.FindFirstValue("sub"));

    public UserRole? Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirstValue(JwtTokenGenerator.RoleClaim), out var role) && Enum.IsDefined(role)
            ? role
            : null;

    public int? CustomerId => ParseInt(Principal?.FindFirstValue(JwtTokenGenerator.CustomerIdClaim));

    private static int? ParseInt(string? value) => int.TryParse(value, out var parsed) ? parsed : null;
}
