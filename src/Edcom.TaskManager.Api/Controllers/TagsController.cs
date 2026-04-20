using Edcom.TaskManager.Api.Controllers.Common;
using Edcom.TaskManager.Application.Services.Tag;
using Edcom.TaskManager.Application.Services.Tag.Contracts;

namespace Edcom.TaskManager.Api.Controllers;

[ApiController]
[Route("api/spaces/{spaceId:long}/tags")]
public class TagsController(ITagService tagService) : AuthorizedController
{
    /// <summary>Get all tags for a space.</summary>
    [HttpGet]
    public async Task<IResult> GetAllAsync(long spaceId, CancellationToken ct = default)
    {
        var result = await tagService.GetAllBySpaceAsync(spaceId, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Get tag by id.</summary>
    [HttpGet("{id:long}")]
    public async Task<IResult> GetByIdAsync(long spaceId, long id, CancellationToken ct = default)
    {
        var result = await tagService.GetByIdAsync(id, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Create a tag. Requires OrgManager role.</summary>
    [HttpPost]
    public async Task<IResult> AddAsync(long spaceId, [FromBody] CreateTagRequest request, CancellationToken ct = default)
    {
        var result = await tagService.AddAsync(spaceId, request, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Update tag. Requires OrgManager role.</summary>
    [HttpPut("{id:long}")]
    public async Task<IResult> UpdateAsync(long spaceId, long id, [FromBody] UpdateTagRequest request, CancellationToken ct = default)
    {
        var result = await tagService.UpdateAsync(id, request, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Delete tag (soft delete). Requires OrgManager role.</summary>
    [HttpDelete("{id:long}")]
    public async Task<IResult> DeleteAsync(long spaceId, long id, CancellationToken ct = default)
    {
        var result = await tagService.DeleteAsync(id, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }
}
