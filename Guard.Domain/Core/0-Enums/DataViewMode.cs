namespace Guard.Core.Enums
{
  public enum DataViewMode
  {
    Active = 1,   // Актуальные (Inserted, Modified, Blocked)
    Archived = 2, // Архивные (Archived, ArchivedBlocked)
    Deleted = 3   // Удаленные (Deleted)
  }
}
