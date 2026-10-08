using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

public class AccountPolicyConfiguration : IEntityTypeConfiguration<AccountPolicy>
{
  public void Configure(EntityTypeBuilder<AccountPolicy> builder)
  {
    builder.ToTable("AccountPolicies", table => {
      table.HasCheckConstraint("CK_AccountPolicies_Id", "\"Id\" = 1");
      table.HasCheckConstraint("CK_AccountPolicies_Ranges", "\"PasswordDays\" BETWEEN 1 AND 3650 AND \"InactivityDays\" BETWEEN 1 AND 3650 AND \"MinimumLength\" BETWEEN 6 AND 100 AND \"UniqueCharacters\" BETWEEN 1 AND \"MinimumLength\"");
    });
    builder.HasKey(p => p.Id);
    builder.Property(p => p.Id).ValueGeneratedNever();
    builder.Property(p => p.Version).IsConcurrencyToken();
  }
}
