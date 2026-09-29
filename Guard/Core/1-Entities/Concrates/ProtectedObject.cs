using System.ComponentModel.DataAnnotations.Schema;

namespace Guard.Core.Entities
{
  [Table("ProtectedObjects", Schema = "public")]
  public partial class ProtectedObject : BaseEntity, IHasSubdivision
  {
    public Guid? SubdivisionId { get; set; }
    public string Name { get; set; }
    public string FullName { get; set; }
    public string LegalAddress { get; set; }
    public string Coordinates { get; set; }
    public string Phone { get; set; }
    public string Note { get; set; }
    public string ObjectId { get; set; }
    public string ImgPath { get; set; }
    public string ArmKey { get; set; }
    public string Barrier { get; set; }
    public string WorkingHours { get; set; } = string.Empty;

    public Subdivision Subdivision { get; set; }

    // --- Navigation ---
    /// <summary>
    /// Вхождения объекта в участки обслуживания (Многие-ко-Многим).
    /// </summary>
    public virtual ICollection<MaintenanceSectorObject> SectorObjects { get; set; } = new List<MaintenanceSectorObject>();

    /// <summary>
    /// Задачи регламентного обслуживания по объекту.
    /// </summary>
  }
}