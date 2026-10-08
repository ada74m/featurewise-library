using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;

namespace Featurewise;

/// <summary>
/// Says whether the current invocation's caller may invoke a link, so that a feature offers only
/// links its caller may follow.
/// </summary>
/// <remarks>
/// <para>
/// A feature takes this as a dependency. It applies the same rules that <see cref="Features"/>
/// enforces when the link is invoked, so the links a feature offers are the links that will work.
/// </para>
/// <para>
/// Offering a link doesn't authorize invoking it later: the link may come back changed, or the
/// rules' answer may change, so <see cref="Features"/> checks again on every invocation.
/// </para>
/// </remarks>
public sealed class LinkPermissions
{
    // Whether each feature type has [AllowAnonymous], read once per type rather than on every check.
    private static readonly ConcurrentDictionary<Type, bool> AllowsAnonymousCallers = new();

    private readonly IAuthorizationService _authorization;
    private readonly Invocation _invocation;

    internal LinkPermissions(IAuthorizationService authorization, Invocation invocation)
    {
        _authorization = authorization;
        _invocation = invocation;
    }

    /// <summary>Says whether the caller may invoke <paramref name="link"/>.</summary>
    /// <param name="link">A link to a registered feature.</param>
    /// <returns>
    /// <see langword="true"/> if the link's feature has <see cref="AllowAnonymousAttribute"/>, or an
    /// authorization handler approves the link for the caller.
    /// </returns>
    public async Task<bool> MayInvoke(Link link)
    {
        if (AllowsAnonymousCallers.GetOrAdd(Features.FeatureTypeOf(link), featureType =>
                featureType.GetCustomAttributes(inherit: false).OfType<IAllowAnonymous>().Any()))
            return true;

        var result = await _authorization.AuthorizeAsync(_invocation.Caller, link, InvokeRequirement.Instance);
        return result.Succeeded;
    }
}
