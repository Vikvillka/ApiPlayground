using ApiPlayground.Api.Contracts;
using ApiPlayground.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiPlayground.Api;

[Route("api/auth")]
public sealed class AuthController(
    RegisterAccountUseCase register,
    SignInUseCase signIn,
    SignOutUseCase signOut,
    CurrentUserUseCase currentUser) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] AuthCredentialsRequest? request, CancellationToken cancellationToken)
    {
        var result = await register.ExecuteAsync(request?.Email, request?.Password, cancellationToken);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, new { id = result.UserId })
            : BadRequestProblem(result.Error ?? "The account could not be created.");
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AuthCredentialsRequest? request, CancellationToken cancellationToken)
    {
        var result = await signIn.ExecuteAsync(request?.Email, request?.Password, cancellationToken);
        if (result.Succeeded)
        {
            return Ok(new { id = result.UserId, email = result.Email });
        }

        return result.Error == SignInUseCase.InvalidCredentialsMessage
            ? UnauthorizedProblem(result.Error)
            : BadRequestProblem(result.Error ?? "Enter an email and password.");
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await signOut.ExecuteAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var session = await currentUser.ExecuteAsync(cancellationToken);
        return session is null
            ? UnauthorizedProblem("Authentication is required.")
            : Ok(new { id = session.UserId, email = session.Email });
    }
}
