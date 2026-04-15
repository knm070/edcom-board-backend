using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edcom.Domain.Entities.Common;

public abstract class ModelBase<TId> where TId : struct
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public TId Id { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}
