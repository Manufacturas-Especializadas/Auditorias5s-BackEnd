﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace Auditorias.Models;

public partial class Audits
{
    public int Id { get; set; }

    public int IdForm { get; set; }

    public int? IdOffices { get; set; }

    public int? IdPeripheralArea { get; set; }

    public int? IdProductionLines { get; set; }

    public DateTime Date { get; set; }

    public string Responsible { get; set; }

    public string Description { get; set; }

    public string PhotoUrl { get; set; }

    public virtual ICollection<Answers> Answers { get; set; } = new List<Answers>();

    public virtual Forms IdFormNavigation { get; set; }

    public virtual PeripheralArea IdPeripheralAreaNavigation { get; set; }

    public virtual ProductionLines IdProductionLinesNavigation { get; set; }
}