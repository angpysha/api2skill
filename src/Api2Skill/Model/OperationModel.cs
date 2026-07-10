using System.Net.Http;

namespace Api2Skill.Model;

/// <summary>
/// One API endpoint+method the skill can invoke. <see cref="Method"/> reuses the BCL
/// <see cref="System.Net.Http.HttpMethod"/> type — it's what Microsoft.OpenApi itself keys
/// OpenApiPathItem.Operations by, and it's the exact type the dispatcher's own HttpClient
/// call needs, so no separate method enum is introduced.
/// </summary>
public sealed record OperationModel(
    string OperationId,
    HttpMethod Method,
    string PathTemplate,
    string? Summary,
    string? Description,
    IReadOnlyList<ParameterModel> Parameters,
    RequestBodyModel? RequestBody,
    IReadOnlyList<string> SecuritySchemeIds,
    IReadOnlyList<ResponseModel> Responses);
