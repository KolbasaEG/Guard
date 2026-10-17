using Guard.Core.Entities;
using Guard.Core.Identity;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Contexts;

internal static class IpCatalogKeys
{
  internal const string Canonical = "CanonicalAddress";
  internal const string Source = "CanonicalSource";
  internal const string SourceConstraint = "CK_IpAddresses_CanonicalSource";
  internal const string SourceCheck = "(\"CanonicalAddress\" IS NULL AND \"CanonicalSource\" IS NULL) OR " +
    "(\"CanonicalAddress\" IS NOT NULL AND \"CanonicalSource\" IS NOT NULL AND \"CanonicalSource\" = \"Address\")";

  internal static string Get(string? address) =>
    IpAddressRule.TryParse(address, out var rule) ? rule!.CanonicalAddress : "";

  internal static void Prepare(ApplicationDbContext db)
  {
    foreach (var entry in db.ChangeTracker.Entries<IpAddress>().Where(e =>
      e.State == EntityState.Added || e.State == EntityState.Modified && e.Property(p => p.Address).IsModified)) {
      entry.Property(Canonical).CurrentValue = Get(entry.Entity.Address);
      entry.Property(Source).CurrentValue = entry.Entity.Address;
    }
  }
}
