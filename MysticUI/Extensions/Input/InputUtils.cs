// Core here is based on Myra UI: https://github.com/rds1983/Myra
using MysticUI.Controls;
using Stride.Core.Mathematics;
using System.Runtime.CompilerServices;

namespace MysticUI.Extensions.Input
{
    internal static class InputUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CommonTouchCheck(this Control control) => control.Visible && control.IsActive && control.Enabled && control.ContainsTouch;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CommonMouseCheck(this Control control) => control.Visible && control.IsActive && control.Enabled && control.ContainsMouse;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FallsThrough(this Control w, Point p) =>
            w is Panel or SplitPane or ContentControl &&
            w.Background == Control.DefaultBackground &&
            !(w is ScrollViewer viewer &&
            (viewer.horizontalScrollingOn && viewer.horizontalScrollbarFrame.Contains(p) ||
             viewer.verticalScrollingOn && viewer.verticalScrollbarFrame.Contains(p)));

        public static bool ProcessTouchDown(this IEnumerable<Control> controls)
        {
            foreach (var control in controls)
            {
                if (control.CommonTouchCheck())
                {
                    // Since OnTouchDown may reset Desktop, we need to save break condition before calling it
                    bool doBreak = (control.Desktop != null && !control.FallsThrough(control.Desktop.TouchPosition));
                    bool inputHandled = control.OnTouchDown();
                    if (doBreak || inputHandled)
                    {
                        return true;
                    }
                }

                if (control.IsModal) return true;
            }
            return false;
        }

        public static void ProcessTouchUp(this IEnumerable<Control> controls)
        {
            foreach (var control in controls)
            {
                if (control.IsTouching)
                {
                    bool doBreak = control.Desktop != null && !control.FallsThrough(control.Desktop.TouchPosition);
                    bool inputHandled = control.OnTouchUp();
                    if (doBreak || inputHandled)
                    {
                        return;
                    }
                }
                if (control.IsModal) return;
            }
        }

        public static void ProcessDoubleClick(this IEnumerable<Control> controls)
        {
            foreach (var control in controls)
            {
                if (control.CommonTouchCheck())
                {
                    control.OnDoubleClick();
                    if (control.Desktop != null && !control.FallsThrough(control.Desktop.TouchPosition))
                    {
                        break;
                    }
                }

                if (control.IsModal)
                    break;
            }
        }

        public static void ProcessMouseMovement(this IEnumerable<Control> controls)
        {
            foreach (Control control in controls)
            {
                if (!control.ContainsMouse && control.IsMouseOver)
                {
                    control.IsMouseOver = false;
                }
            }

            foreach (Control control in controls)
            {
                if (control.CommonMouseCheck())
                {
                    control.IsMouseOver = true;
                    if (control.Desktop != null && !control.FallsThrough(control.Desktop.MousePosition))
                        break;
                }

                if (control.IsModal) break;
            }
        }

        public static void ProcessTouchMovement(this IEnumerable<Control> controls)
        {
            foreach (var control in controls)
            {
                if (!control.ContainsTouch && control.IsTouching)
                {
                    control.OnTouchLeft();
                }
            }

            foreach (var control in controls)
            {
                if (control.CommonTouchCheck())
                {
                    if (control.ContainsTouch)
                    {
                        if (control.IsTouching)
                        {
                            control.OnTouchMoved();
                        }
                        else
                        {
                            control.OnTouchEntered();
                        }

                        if (control.Desktop != null && !control.FallsThrough(control.Desktop.MousePosition))
                            break;
                    }
                }

                if (control.IsModal) break;
            }
        }
    }
}