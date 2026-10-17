namespace Guard.Core.Entities;

public interface IHasSubdivision
{
  Guid? SubdivisionId { get; }
  Subdivision? Subdivision { get; }
}