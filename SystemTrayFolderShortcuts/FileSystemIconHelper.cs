using System.Runtime.InteropServices;

namespace SystemTrayFolderShortcuts
{
	internal static class FileSystemIconHelper
	{
		public static Bitmap? GetFileIcon(string filepath)
		{
			return Icon.ExtractAssociatedIcon(filepath)?.ToBitmap();
		}

		private static Bitmap? defaultFolderIcon = null;
		public static Bitmap? GetFolderIcon()
		{
			if (defaultFolderIcon == null)
			{
				defaultFolderIcon = GetDefaultFolderIcon(false);
			}

			return defaultFolderIcon;
		}

		#region Ugly windows stuff
		// Struct for SHGetStockIconInfo
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct SHSTOCKICONINFO
		{
			public uint cbSize;
			public IntPtr hIcon;
			public int iSysIconIndex;
			public int iIcon;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			public string szPath;
		}

		// Flags for SHGetStockIconInfo
		[Flags]
		private enum SHGSI : uint
		{
			ICONLOCATION = 0,
			ICON = 0x000000100,
			SYSICONINDEX = 0x000004000,
			LARGEICON = 0x000000000,
			SMALLICON = 0x000000001,
			SHELLICONSIZE = 0x000000004
		}

		// Stock icon IDs
		private enum SHSTOCKICONID : uint
		{
			FOLDER = 3, // Default folder icon
			FOLDEROPEN = 4
		}

		// Import SHGetStockIconInfo from Shell32.dll
		[DllImport("Shell32.dll", SetLastError = false)]
		private static extern int SHGetStockIconInfo(
			SHSTOCKICONID siid,
			SHGSI uFlags,
			ref SHSTOCKICONINFO psii);


		private static Bitmap? GetDefaultFolderIcon(bool large)
		{
			SHSTOCKICONINFO info = new SHSTOCKICONINFO();
			info.cbSize = (uint)Marshal.SizeOf(typeof(SHSTOCKICONINFO));

			SHGSI flags = SHGSI.ICON | (large ? SHGSI.LARGEICON : SHGSI.SMALLICON);

			int hr = SHGetStockIconInfo(SHSTOCKICONID.FOLDER, flags, ref info);
			if (hr != 0)
				throw Marshal.GetExceptionForHR(hr) ?? new Exception("GetDefaultFolderIcon() didn't work and couldn't get the proper error message, though luck!");

			// Create a managed Icon from the handle
			Icon icon = (Icon)Icon.FromHandle(info.hIcon).Clone();

			// Destroy the original icon handle to prevent leaks
			DestroyIcon(info.hIcon);

			return icon.ToBitmap();
		}

		[DllImport("User32.dll", SetLastError = true)]
		private static extern bool DestroyIcon(IntPtr hIcon);
		#endregion
	}
}
