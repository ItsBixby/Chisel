using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPSTemplate.Entities;
public interface LivingActor
{
    public int MaxHP { get; }
    public int HP { get; protected set; }
    public bool IsDead { get; protected set; }
}
