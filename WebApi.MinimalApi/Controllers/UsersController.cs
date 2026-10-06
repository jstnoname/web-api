using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : Controller
{
    private readonly IUserRepository userRepository;
    private readonly IMapper mapper;
    
    // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
    public UsersController(IUserRepository userRepository, IMapper mapper)
    {
        this.userRepository = userRepository;
        this.mapper = mapper;
    }

    [HttpGet("{userId}", Name = nameof(GetUserById))]
    [Produces("application/json", "application/xml")]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {
        var user = userRepository.FindById(userId);
        return user == null ? NotFound() : Ok(mapper.Map<UserDto>(user));
    }

    [HttpPost]
    [Produces("application/json", "application/xml")]
    public IActionResult CreateUser([FromBody] CreatedUserDto? user)
    {
        if (user == null)
            return BadRequest();
        if (!ModelState.IsValid || !user.Login.All(char.IsLetterOrDigit))
        {
            ModelState.AddModelError("Login", "Invalid login");
            return UnprocessableEntity(ModelState);
        }
        
        var userEntity = mapper.Map<UserEntity>(user);
        var insertedUser = userRepository.Insert(userEntity);
        return CreatedAtRoute(
            nameof(GetUserById),
            new { userId = insertedUser.Id },
            insertedUser.Id);
    }

    [HttpPut("{userId}")]
    [Produces("application/json", "application/xml")]
    public IActionResult UpdateUser([FromRoute] Guid userId, [FromBody] PostUserDto? user)
    {
        if (user == null || userId == Guid.Empty) 
            return BadRequest();
        if (!ModelState.IsValid)
        {
            ModelState.AddModelError("Login", "Invalid login");
            return UnprocessableEntity(ModelState);
        }

        var userRepoEntity = userRepository.FindById(userId) ?? new UserEntity(userId);
        var mappedUser = mapper.Map(user, userRepoEntity);
        userRepository.UpdateOrInsert(mappedUser, out var isInserted);
        if (isInserted)
            return CreatedAtRoute(
                nameof(GetUserById),
                new { userId = mappedUser.Id },
                mappedUser.Id);
        return NoContent();
    }
}

public class CreatedUserDto
{
    [Required]
    public string Login { get; set; }
    [DefaultValue("John")]
    public string FirstName { get; set; }
    [DefaultValue("Doe")]
    public string LastName { get; set; }
}

public class PostUserDto
{
    [Required]
    [RegularExpression("^[0-9\\p{L}]*$", ErrorMessage = "Login should contain only letters or digits")]
    public string Login { get; set; }
    [Required]
    public string FirstName { get; set; }
    [Required]
    public string LastName { get; set; }
}