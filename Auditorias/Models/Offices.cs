using System;
using System.Collections.Generic;

namespace Auditorias.Models;

public partial class Offices
{
    public int Id { get; set; }

    public string Name { get; set; }

    public virtual ICollection<Audits> Audits { get; set; } = new List<Audits>();
}