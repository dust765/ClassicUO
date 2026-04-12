#region license

// Copyright (c) 2021, andreakarasho
// All rights reserved.

#endregion

using ClassicUO.Game.Managers;
using Microsoft.Xna.Framework;
using System.Linq;

namespace ClassicUO.Game.UI.Gumps
{
    internal partial class CounterBarGump
    {
        private class DraggableGump : Gump
        {
            public DraggableGump() : base(0, 0)
            {
                CanMove = true;
            }

            protected override void OnDragEnd(int x, int y)
            {
                if (UIManager.MouseOverControl == this || UIManager.MouseOverControl?.RootParent == this)
                {
                    Children.FirstOrDefault()?.InvokeDragEnd(new Point(x, y));
                }

                base.OnDragEnd(x, y);
            }
        }
    }
}
