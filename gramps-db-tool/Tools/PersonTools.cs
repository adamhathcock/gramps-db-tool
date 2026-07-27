using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class PersonTools(GrampsRepository repository)
{
    [McpServerTool(Name = "search_people", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Search Gramps people by given name, surname, or Gramps ID. Returns read-only summary records.")]
    public Task<PageDto<PersonSearchResultDto>> SearchPeople(
        [Description("Search text. Use an empty string to return the first people by surname.")]
        string query,
        [Description("Maximum number of people to return, from 1 to 500.")]
        int limit = 20,
        [Description("Number of matching people to skip before returning results. Must not be negative.")]
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        return repository.SearchPeopleAsync(query, limit, offset, cancellationToken);
    }

    [McpServerTool(Name = "get_person", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Get one Gramps person by handle or Gramps ID. Identity, relationship, event, and fact fields are read-only.")]
    public async Task<PersonDto?> GetPerson(
        [Description("Gramps person handle. Required if grampsId is not supplied.")]
        string? handle = null,
        [Description("User-visible Gramps person ID. Required if handle is not supplied.")]
        string? grampsId = null,
        CancellationToken cancellationToken = default)
    {
        var person = await repository.GetPersonAsync(handle, grampsId, cancellationToken);
        return person;
    }
}