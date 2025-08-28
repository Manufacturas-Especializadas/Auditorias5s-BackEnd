using System;
using System.Collections.Generic;

namespace Auditorias.Models;

public partial class Forms
{
    public int Id { get; set; }

    public string Name { get; set; }

    public virtual ICollection<Audits> Audits { get; set; } = new List<Audits>();

    public virtual ICollection<Sections> Sections { get; set; } = new List<Sections>();
}