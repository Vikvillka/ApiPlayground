using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ApiPlayground.Api;

// Base for application controllers. Validation and application-failure responses use RFC 9457 Problem Details
// (application/problem+json) with safe `detail` text. Guard rejections, bodyless binding/not-found responses,
// and rich tracer outcome bodies are intentionally not produced through these helpers.
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult BadRequestProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest);

    protected IActionResult ServiceUnavailableProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status503ServiceUnavailable);
}
