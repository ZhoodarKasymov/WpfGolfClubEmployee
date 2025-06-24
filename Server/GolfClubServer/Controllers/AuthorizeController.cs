using GolfClubServer.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GolfClubServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthorizeController : ControllerBase
{
    private readonly UnitOfWork _unitOfWork;

    public AuthorizeController(UnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IActionResult> GetUserRole([FromQuery] string login, [FromQuery] string password)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            return BadRequest("Login and password are required.");
        }

        var user = await _unitOfWork.UserRepository
            .GetAll()
            .FirstOrDefaultAsync(u => u.Username == login && u.Password == password);

        if (user == null)
        {
            return Unauthorized("Invalid login or password.");
        }

        return Ok(user);
    }
}