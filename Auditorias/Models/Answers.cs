using System;
using System.Collections.Generic;

namespace Auditorias.Models;

public partial class Answers
{
    public int Id { get; set; }

    public int Score { get; set; }

    public int IdAudit { get; set; }

    public int IdQuestion { get; set; }

    public virtual Audits IdAuditNavigation { get; set; }

    public virtual Questions IdQuestionNavigation { get; set; }
}