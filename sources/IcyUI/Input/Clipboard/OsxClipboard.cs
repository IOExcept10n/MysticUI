// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

#pragma warning disable
namespace Icy.Input.Clipboard
{
    /// <summary>
    /// Represents a default implementation of the <see langword="OS X"/> clipboard.
    /// </summary>
    [SupportedOSPlatform("osx")]
    internal class OsxClipboard : IClipboard
    {
        private static readonly IntPtr nsString = objc_getClass("NSString");
        private static readonly IntPtr nsPasteboard = objc_getClass("NSPasteboard");
        private static readonly IntPtr nsStringPboardType;
        private static readonly IntPtr utfTextType;
        private static readonly IntPtr generalPasteboard;

        static OsxClipboard()
        {
            utfTextType = objc_msgSend(objc_msgSend(nsString, sel_registerName("alloc")), sel_registerName("initWithUTF8String:"), "public.utf8-plain-text");
            nsStringPboardType = objc_msgSend(objc_msgSend(nsString, sel_registerName("alloc")), sel_registerName("initWithUTF8String:"), "NSStringPboardType");

            generalPasteboard = objc_msgSend(nsPasteboard, sel_registerName("generalPasteboard"));
        }

        /// <inheritdoc/>
        [DisallowNull]
        public string? Text
        {
            get
            {
                var ptr = objc_msgSend(generalPasteboard, sel_registerName("stringForType:"), nsStringPboardType);
                var charArray = objc_msgSend(ptr, sel_registerName("UTF8String"));
                return Marshal.PtrToStringAnsi(charArray);
            }

            set
            {
                IntPtr str = IntPtr.Zero;
                try
                {
                    str = objc_msgSend(objc_msgSend(nsString, sel_registerName("alloc")), sel_registerName("initWithUTF8String:"), value);
                    objc_msgSend(generalPasteboard, sel_registerName("clearContents"));
                    objc_msgSend(generalPasteboard, sel_registerName("setString:forType:"), str, utfTextType);
                }
                finally
                {
                    if (str != IntPtr.Zero)
                    {
                        objc_msgSend(str, sel_registerName("release"));
                    }
                }
            }
        }

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit", CharSet = CharSet.Unicode)]
        private static extern IntPtr objc_getClass(string className);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit", CharSet = CharSet.Unicode)]
        private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, string arg1);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit", CharSet = CharSet.Unicode)]
        private static extern IntPtr sel_registerName(string selectorName);
    }
}
