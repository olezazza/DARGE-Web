using System.Security.Claims;
using Darge.Web.Data;
using Darge.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Darge.Web.Controllers;


public class AccountController : Controller
{
    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }


    [HttpPost]
    public async Task<IActionResult> Register(
        string username,
        string password
    )
    {
        if (
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password)
        )
        {
            ViewBag.Error =
                "Username and password are required.";

            return View();
        }


        User? user =
            UserRepository.Register(
                username,
                password
            );


        if (user == null)
        {
            ViewBag.Error =
                "That username already exists.";

            return View();
        }


        await SignUserIn(user);


        return RedirectToAction(
            "Index",
            "Dashboard"
        );
    }


    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }


    [HttpPost]
    public async Task<IActionResult> Login(
        string username,
        string password
    )
    {
        User? user =
            UserRepository.Login(
                username,
                password
            );


        if (user == null)
        {
            ViewBag.Error =
                "Incorrect username or password.";

            return View();
        }


        await SignUserIn(user);


        return RedirectToAction(
            "Index",
            "Dashboard"
        );
    }


    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults
                .AuthenticationScheme
        );


        return RedirectToAction(
            "Index",
            "Home"
        );
    }


    private async Task SignUserIn(
        User user
    )
    {
        List<Claim> claims =
            new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.Id.ToString()
                ),

                new Claim(
                    ClaimTypes.Name,
                    user.Username
                )
            };


        ClaimsIdentity identity =
            new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults
                    .AuthenticationScheme
            );


        ClaimsPrincipal principal =
            new ClaimsPrincipal(identity);


        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults
                .AuthenticationScheme,
            principal
        );
    }
}