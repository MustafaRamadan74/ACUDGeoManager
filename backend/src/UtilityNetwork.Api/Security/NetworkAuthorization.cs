using System.Security.Claims;

namespace UtilityNetwork.Api.Security;

public static class NetworkAuthorization
{
    public static bool CanAccessNetwork(ClaimsPrincipal user, Guid networkId)
    {
        if (user.IsInRole("ADMIN"))
        {
            return true;
        }

        var networkClaims = user.FindAll("network_id");
        var networkIdStr = networkId.ToString();

        return networkClaims.Any(c => string.Equals(c.Value, networkIdStr, StringComparison.OrdinalIgnoreCase));
    }
}
