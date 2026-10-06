using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : Controller
{
    private readonly IUserRepository userRepository;
    private readonly IMapper mapper;
    private readonly LinkGenerator linkGenerator;
    
    // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
    public UsersController(IUserRepository userRepository, IMapper mapper, LinkGenerator linkGenerator)
    {
        this.userRepository = userRepository;
        this.mapper = mapper;
        this.linkGenerator = linkGenerator;
    }

    [HttpGet("{userId}", Name = nameof(GetUserById))]
    [HttpHead("{userId}")]
    [Produces("application/json", "application/xml")]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {
        var user = userRepository.FindById(userId);
        if (user is null)
            return NotFound();
        if (HttpMethods.IsHead(Request.Method))
        {
            Response.ContentType = "application/json; charset=utf-8";
            return Ok();
        }        
        return Ok(mapper.Map<UserDto>(user));
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

    [HttpPatch("{userId}")]
    public IActionResult PartiallyUpdateUser([FromRoute] Guid userId, [FromBody] JsonPatchDocument<PostUserDto>? patchDocument)
    {
        var userEntity = userRepository.FindById(userId);
        
        if (patchDocument is null)
            return BadRequest();
        if (userId == Guid.Empty || userEntity == null)
            return NotFound();
        
        var userUpdates = new PostUserDto();
        patchDocument.ApplyTo(userUpdates, ModelState);
        
        if (!TryValidateModel(userUpdates))
        {
            ModelState.AddModelError("Login", "Invalid login");
            return UnprocessableEntity(ModelState);
        }

        var mappedUser = mapper.Map(userUpdates, userEntity);
        userRepository.Update(mappedUser);
        return NoContent();
    }

    [HttpDelete("{userId}")]
    public IActionResult DeleteUser([FromRoute] Guid userId)
    {
        if (userId != Guid.Empty && userRepository.FindById(userId) is not null)
        {
            userRepository.Delete(userId);
            return NoContent();
        }
        return NotFound();
    }

    [HttpGet]
    public IActionResult GetUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        if (pageSize > 20)
            pageSize = 20;
        if (pageNumber < 1)
            pageNumber = 1;
        if (pageSize < 1)
            pageSize = 1;
        var page = userRepository.GetPage(pageNumber, pageSize);
        var previousPage = page.HasPrevious 
            ? linkGenerator.GetUriByRouteValues(HttpContext, "", new { pageNumber = pageNumber - 1, pageSize = pageSize }) 
            : null;
        var nextPage = page.HasNext 
            ? linkGenerator.GetUriByRouteValues(HttpContext, "", new { pageNumber = pageNumber + 1, pageSize = pageSize }) 
            : null;
        var paginationHeader = new
        {
            previousPageLink = previousPage,
            nextPageLink = nextPage,
            totalCount = page.TotalCount,
            pageSize = pageSize,
            currentPage = pageNumber,
            totalPages = page.TotalPages,
        };
        Response.Headers.Add("X-Pagination", JsonConvert.SerializeObject(paginationHeader));
        return Ok(page);
    }

    [HttpOptions]
    public IActionResult GetUsersOptions()
    {
        Response.Headers.Add("Allow", "GET, POST, OPTIONS");
        return Ok();
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