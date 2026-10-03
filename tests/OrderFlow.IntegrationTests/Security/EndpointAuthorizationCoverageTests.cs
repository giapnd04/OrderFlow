using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using OrderFlow.API.Authorization;
using OrderFlow.API.Controllers;

namespace OrderFlow.IntegrationTests.Security;

/// <summary>
/// Regression net for "an endpoint was added and nobody decided who may call it". The app is
/// secure by default (fallback policy), but a new action should still state its policy
/// explicitly - and only the auth controller may ever be anonymous.
/// </summary>
public sealed class EndpointAuthorizationCoverageTests
{
    private static IReadOnlyList<(Type Controller, MethodInfo Action)> AllActions() =>
        typeof(OrdersController).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
                .Select(action => (controller, action)))
            .ToList();

    private static string Name((Type Controller, MethodInfo Action) a) => $"{a.Controller.Name}.{a.Action.Name}";

    [Fact]
    public void ThereAreActionsToCheck()
    {
        Assert.True(AllActions().Count >= 30, $"expected 30+ actions, found {AllActions().Count}");
    }

    [Fact]
    public void EveryActionStatesItsAuthorization()
    {
        var undecided = AllActions()
            .Where(a => !HasAuthorize(a) && !IsAnonymous(a))
            .Select(Name)
            .ToList();

        Assert.True(undecided.Count == 0, "Actions with no [Authorize]/[AllowAnonymous]: " + string.Join(", ", undecided));
    }

    [Fact]
    public void OnlyTheAuthControllerIsAnonymous()
    {
        var publicActions = AllActions().Where(IsAnonymous).Select(Name).ToList();

        Assert.NotEmpty(publicActions);
        Assert.All(publicActions, name => Assert.StartsWith("AuthController.", name));
    }

    [Fact]
    public void EveryAuthorizeNamesAPolicyThatExists()
    {
        var known = typeof(Policies)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

        var offenders = AllActions()
            .SelectMany(a => a.Action.GetCustomAttributes<AuthorizeAttribute>().Select(attr => (Name: Name(a), attr)))
            .Where(x => string.IsNullOrEmpty(x.attr.Policy) || !known.Contains(x.attr.Policy!) || !string.IsNullOrEmpty(x.attr.Roles))
            .Select(x => x.Name)
            .ToList();

        Assert.True(offenders.Count == 0, "Bare/unknown [Authorize] (use a named policy): " + string.Join(", ", offenders));
    }

    [Fact]
    public void NoControllerOpensItselfUpAtClassLevel()
    {
        var classLevelAnonymous = AllActions()
            .Select(a => a.Controller)
            .Distinct()
            .Where(c => c.GetCustomAttributes<AllowAnonymousAttribute>().Any() && c != typeof(AuthController))
            .Select(c => c.Name);

        Assert.Empty(classLevelAnonymous);
    }

    private static bool HasAuthorize((Type Controller, MethodInfo Action) a)
        => a.Action.GetCustomAttributes<AuthorizeAttribute>().Any() || a.Controller.GetCustomAttributes<AuthorizeAttribute>().Any();

    private static bool IsAnonymous((Type Controller, MethodInfo Action) a)
        => a.Action.GetCustomAttributes<AllowAnonymousAttribute>().Any() || a.Controller.GetCustomAttributes<AllowAnonymousAttribute>().Any();
}
