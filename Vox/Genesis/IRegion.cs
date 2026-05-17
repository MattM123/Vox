using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vox.Genesis
{
    public interface IRegion
    {
        Rectangle GetBounds();
        bool Equals(object? o);
        string ToString();
        int GetHashCode();
    }
}
