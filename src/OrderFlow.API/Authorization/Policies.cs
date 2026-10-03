using Microsoft.AspNetCore.Authorization;
using OrderFlow.Domain.Enums;

namespace OrderFlow.API.Authorization;

/// <summary>
/// The whole role-to-capability matrix lives here and nowhere else; controllers only name a
/// policy. Administrator is a member of every policy. See ADR-005 for the rationale.
/// </summary>
public static class Policies
{
    /// <summary>Administrator only.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>Sales + Administrator: customers, product catalog, payments, order confirmation.</summary>
    public const string Sales = "Sales";

    /// <summary>Warehouse + Administrator: stock movements and fulfilment (ship / deliver).</summary>
    public const string Warehouse = "Warehouse";

    /// <summary>Any internal role: Sales, Warehouse or Administrator.</summary>
    public const string Staff = "Staff";

    /// <summary>Customer, Sales or Administrator: placing and cancelling orders (customers: own orders only).</summary>
    public const string CustomerOrSales = "CustomerOrSales";

    /// <summary>Any authenticated user, including customers.</summary>
    public const string Authenticated = "Authenticated";

    public static AuthorizationBuilder AddOrderFlowPolicies(this AuthorizationBuilder builder)
    {
        return builder
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AdminOnly, p => p.RequireAuthenticatedUser().RequireRole(Role(UserRole.Administrator)))
            .AddPolicy(Sales, p => p.RequireAuthenticatedUser()
                .RequireRole(Role(UserRole.Sales), Role(UserRole.Administrator)))
            .AddPolicy(Warehouse, p => p.RequireAuthenticatedUser()
                .RequireRole(Role(UserRole.Warehouse), Role(UserRole.Administrator)))
            .AddPolicy(Staff, p => p.RequireAuthenticatedUser()
                .RequireRole(Role(UserRole.Sales), Role(UserRole.Warehouse), Role(UserRole.Administrator)))
            .AddPolicy(CustomerOrSales, p => p.RequireAuthenticatedUser()
                .RequireRole(Role(UserRole.Customer), Role(UserRole.Sales), Role(UserRole.Administrator)))
            .AddPolicy(Authenticated, p => p.RequireAuthenticatedUser());
    }

    private static string Role(UserRole role) => role.ToString();
}
