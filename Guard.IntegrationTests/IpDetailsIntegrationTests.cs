using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class IpDetailsIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory,
    IIpAddressService root, IIpAddressService user, Action<bool, string> check)
  {
    await using var db = await factory.CreateDbContextAsync();
    var own = await db.Subdivisions.SingleAsync(s => s.Name == "Own");
    var other = await db.Subdivisions.SingleAsync(s => s.Name == "Other");
    var inactive = new Subdivision { Name = "IP DTO archived", Path = "/99999/", Status = Status.Archived, CreatedBy = "test" };
    db.Add(inactive); await db.SaveChangesAsync();
    var ids = new List<Guid>();
    try {
      var id = await user.CreateAsync(new CreateIpAddressDto("198.51.100.127/25", "DTO", own.Id)); ids.Add(id);
      var common = await root.CreateAsync(new CreateIpAddressDto("2001:db8:face::/64", null, null)); ids.Add(common);
      var foreign = await root.CreateAsync(new CreateIpAddressDto("2001:db8:face:1::/64", null, other.Id)); ids.Add(foreign);
      var detail = (await user.GetDetailsAsync(id))!;
      var stored = await db.IpAddresses.AsNoTracking().SingleAsync(p => p.Id == id);
      check(detail.Address == "198.51.100.0/25" && detail.SubdivisionName == own.Name &&
        detail.Info.Id == id && detail.Info.InsertedDate == stored.InsertedDate && detail.Status == Status.Inserted,
        "IP detail DTO projects canonical fields, subdivision and server audit");
      check(await user.GetDetailsAsync(common) == null && await user.GetDetailsAsync(foreign) == null &&
        await user.GetDetailsAsync(Guid.NewGuid()) == null, "IP detail DTO hides common, foreign and missing records");
      check((await root.GetDetailsAsync(common))!.SubdivisionId == null, "Root detail DTO supports common IP");
      var options = await user.GetSubdivisionOptionsAsync();
      check(options.Any(s => s.Id == own.Id) && options.All(s => s.Id != other.Id && s.Id != inactive.Id),
        "IP options contain only active authorized subdivisions");
      var rootOptions = await root.GetSubdivisionOptionsAsync();
      check(rootOptions.Any(s => s.Id == other.Id) && rootOptions.All(s => s.Id != inactive.Id),
        "Root IP options include foreign active subdivisions and omit archive");
      await user.UpdateAsync(new EditIpAddressDto(id, "198.51.100.200/25", "Updated", own.Id));
      stored = await db.IpAddresses.AsNoTracking().SingleAsync(p => p.Id == id);
      check(stored.Address == "198.51.100.128/25" && stored.Description == "Updated" && stored.Status == Status.Modified &&
        stored.CreatedBy == "user" && stored.ModifiedBy == "user" && stored.InsertedDate == detail.Info.InsertedDate &&
        stored.LastModifiedDate.HasValue, "IP DTO update preserves creation audit and sets trusted modification audit");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => user.UpdateAsync(new EditIpAddressDto(id, stored.Address, null, other.Id)), check, "IP DTO cannot move to foreign subdivision");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => user.CreateAsync(new CreateIpAddressDto("198.51.100.1", null, null)), check, "IP DTO cannot create common IP as operator");
      await PostgresTests.ThrowsAsync<KeyNotFoundException>(() => user.UpdateAsync(new EditIpAddressDto(foreign, "198.51.100.1", null, own.Id)), check, "IP DTO update cannot capture foreign record");
      await PostgresTests.ThrowsAsync<EntityRuleException>(() => root.CreateAsync(new CreateIpAddressDto("198.51.100.1", null, inactive.Id)), check, "IP DTO cannot select archived subdivision");
      await PostgresTests.ThrowsAsync<ArgumentException>(() => user.CreateAsync(new CreateIpAddressDto("bad", null, own.Id)), check, "IP DTO validates address on server");
      await PostgresTests.ThrowsAsync<ArgumentException>(() => user.UpdateAsync(new EditIpAddressDto(id, stored.Address, new string('x', 1001), own.Id)), check, "IP DTO validates description on server");
      await PostgresTests.ThrowsAsync<ArgumentException>(() => user.CreateAsync(new CreateIpAddressDto("198.51.100.199/25", null, own.Id)), check, "IP DTO detects equivalent duplicate");
      foreach (var state in new[] { Status.Blocked, Status.Archived, Status.ArchivedBlocked, Status.Deleted }) {
        await db.IpAddresses.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, state));
        check((await user.GetDetailsAsync(id))!.Status == state, "IP details preserve non-editable status " + state);
        await PostgresTests.ThrowsAsync<EntityRuleException>(() => user.UpdateAsync(new EditIpAddressDto(id, stored.Address, null, own.Id)), check, "IP DTO rejects non-active update " + state);
      }
      var readonlyService = new IpAddressService(factory, new ReadOnlyAccess(), NullLogger<IpAddressService>.Instance);
      check(await readonlyService.GetDetailsAsync(common) != null, "read-only access can load detail DTO");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => readonlyService.UpdateAsync(new EditIpAddressDto(common, "198.51.100.1", null, null)), check, "IP DTO write requests management permission");
      using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => root.GetDetailsAsync(common, cancelled.Token), check, "IP detail propagates cancellation");
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => root.GetSubdivisionOptionsAsync(cancelled.Token), check, "IP options propagate cancellation");
      foreach (var details in new[] { true, false }) {
        var changed = new IpAddressService(factory, new ChangingAccess(), NullLogger<IpAddressService>.Instance);
        await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(async () => {
          if (details) await changed.GetDetailsAsync(common); else await changed.GetSubdivisionOptionsAsync();
        }, check, "IP DTO read rejects changed access scope");
      }
    }
    finally {
      await db.IpAddresses.Where(p => ids.Contains(p.Id)).ExecuteDeleteAsync();
      db.Remove(inactive); await db.SaveChangesAsync();
    }
  }

  private sealed class ReadOnlyAccess : IIpManagementAccessService
  {
    public Task<IpManagementScope> GetScopeAsync(bool write, CancellationToken ct = default) =>
      write ? throw new UnauthorizedAccessException() : Task.FromResult(new IpManagementScope("root", true, []));
  }
  private sealed class ChangingAccess : IIpManagementAccessService
  {
    private int calls;
    public Task<IpManagementScope> GetScopeAsync(bool write, CancellationToken ct = default) =>
      Task.FromResult(new IpManagementScope("root", ++calls == 1, []));
  }
}
