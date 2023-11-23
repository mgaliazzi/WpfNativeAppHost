using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace InteroperabilityTest
{
    // based on https://stackoverflow.com/q/30186930/426315
    public class HwndHostRioD : HwndHost
    {
        private readonly IntPtr _childHandle;
        private readonly Process _process;
        public HwndHostRioD()
        {

            _process = new Process
            {
                StartInfo = new ProcessStartInfo("RioD.exe")
                {
                    UseShellExecute = true,
                    //Arguments = $"\"{ARGUMENTS}\""

                }
            };

            _process.Start();
            _process.WaitForInputIdle(Timeout.Infinite);

            // The main window handle may be unavailable for a while, just wait for it
            while (_process.MainWindowHandle == IntPtr.Zero)
            {
                Thread.Yield();
            }
            // Note: This method may not reliably return the main window handle of calc.exe
            IntPtr mainWindowHandle = _process.MainWindowHandle;
            // We might need additional logic to wait for the main window to be ready
            // before attempting to host it.

            if (mainWindowHandle != IntPtr.Zero)
            {
                _childHandle = mainWindowHandle;
            }

        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            var childRef = new HandleRef();

            if (_childHandle != IntPtr.Zero)
            {
                var childStyle = (IntPtr)(Win32API.WindowStyles.WS_CHILD |
                                          // Child window should be have a thin-line border
                                          Win32API.WindowStyles.WS_BORDER |
                                          // the parent cannot draw over the child's area. this is needed to avoid refresh issues
                                          Win32API.WindowStyles.WS_CLIPCHILDREN |
                                          Win32API.WindowStyles.WS_VISIBLE |
                                          Win32API.WindowStyles.WS_MAXIMIZE);

                childRef = new HandleRef(this, _childHandle);
                Win32API.SetWindowLongPtr(childRef, Win32API.GWL_STYLE, childStyle);
                Win32API.SetParent(_childHandle, hwndParent.Handle);
            }

            return childRef;
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
        }
    }
}
