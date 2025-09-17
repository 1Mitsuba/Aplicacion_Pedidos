using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Aplicacion_Pedidos.Models.Enums;

namespace Aplicacion_Pedidos.Filters
{
    public class AuthorizeRolesAttribute : TypeFilterAttribute
    {
        public AuthorizeRolesAttribute(params UserRole[] roles) : base(typeof(AuthorizeRolesFilter))
        {
            Arguments = new object[] { roles };
        }
    }

    public class AuthorizeRolesFilter : IAuthorizationFilter
    {
        private readonly UserRole[] _roles;

        public AuthorizeRolesFilter(UserRole[] roles)
        {
            _roles = roles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            if (!user.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new RedirectToActionResult("Login", "Auth", null);
                return;
            }

            if (_roles.Length > 0 && !_roles.Any(role => 
                user.IsInRole(role.ToString())))
            {
                context.Result = new ForbidResult();
            }
        }
    }
}