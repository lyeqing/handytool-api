namespace handytool_api.Contracts;

public sealed record SubcategoryResponse(long Id, string Name, string Description);
public sealed record CategoryResponse(long Id, string Name, string Description, IReadOnlyList<SubcategoryResponse> Subcategories);
public sealed record HomeUserResponse(long Id, string DisplayName);
public sealed record HomeResponse(HomeUserResponse? User, bool CanCreateDefinition,
    IReadOnlyList<CategoryResponse> Categories, IReadOnlyList<ObjectDefinitionResponse> Tools,
    PagedResponse<HomeRecordResponse>? Records);
public sealed record HomeRecordResponse(long Id, long ObjectDefinitionId, string ToolName,
    string? Title, string? Description, DateTime ModifiedDate);
