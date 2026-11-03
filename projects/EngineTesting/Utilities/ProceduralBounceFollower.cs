using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace EngineTesting.Utilities
{
    public class ProceduralBounceFollower
    {
        float xp;
        float y, yd;
        float k1, k2, k3;
        float T_crit;

        public ProceduralBounceFollower(float convergence, float acceleration, float anticipation, float x0)
        {
            k1 = acceleration / (MathF.PI*convergence);
            k2 = 1/ (2*MathF.PI*convergence*(2*MathF.PI*convergence));
            k3 = anticipation * acceleration / (2 * MathF.PI * convergence);

            T_crit = 0.8f * (MathF.Sqrt(4*k2 + k1*k1)-k1);

            xp = x0;
            y = x0;
            yd = 0f;
        }

        public float Update(float delta, float x)
        {
            float xd = (x - xp) / delta;
            xp = x;
            int iterations = (int)MathF.Ceiling(delta/T_crit);
            delta = delta / iterations;
            for(int i = 0; i < iterations; i++)
            {
                y = y + delta * yd;
                yd = yd + delta * (x + k3 * xd - y - k1 * yd) / k2;
            }

            return y;
        }
    }
}
