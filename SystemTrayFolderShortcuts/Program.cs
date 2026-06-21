using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace SystemTrayFolderShortcuts
{
    internal class Program
    {
        [STAThread]
        static void Main()
        {
			// To customize application configuration such as set high DPI settings or default font,
			// see https://aka.ms/applicationconfiguration.
			ApplicationConfiguration.Initialize();
            Application.Run(new MyApplicationContext());
        }
	}

    internal class MyApplicationContext : ApplicationContext
    {
		private NotifyIcon? mainSysTrayIcon;
		private ToolStripTextBox MaxFolderDepthToolStripTextBox = new();

		private ToolStripSeparator mainContextMenuFolderSeparator = new ToolStripSeparator() { Visible = false };

		private bool? isRegisteredToLaunchOnStartup = null;

		public MyApplicationContext()
		{
			Application.ApplicationExit += new EventHandler(OnApplicationExit);

			MaxFolderDepthToolStripTextBox.TextBox.KeyDown += MaxFolderDepth_KeyDown;
			MaxFolderDepthToolStripTextBox.TextBox.LostFocus += MaxFolderDepth_LostFocus;

			Init();
		}

		private void Init()
		{
			mainContextMenuFolderSeparator.Visible = false;

			MaxFolderDepthToolStripTextBox.AcceptsReturn = true;
			MaxFolderDepthToolStripTextBox.Text = "";
			MaxFolderDepthToolStripTextBox.TextBox.PlaceholderText = "Max Folder Depth: "+ Properties.Settings.Default.MaxFolderDepth;
			Size size = TextRenderer.MeasureText(MaxFolderDepthToolStripTextBox.TextBox.PlaceholderText, MaxFolderDepthToolStripTextBox.TextBox.Font);
			MaxFolderDepthToolStripTextBox.TextBox.Width = size.Width + 10;
			SetMaxFolderDepthBackgroundColor(false);

			mainSysTrayIcon = new NotifyIcon
			{
				Text = "System Tray Folder Shortcuts",
				Icon = Properties.Resources.MainIcon,
				Visible = true,
				ContextMenuStrip = new ContextMenuStrip
				{
					RenderMode = ToolStripRenderMode.Professional,
					Renderer = Properties.Settings.Default.UseDarkMode ? new DarkRenderer() : null,
					Items =
					{
						mainContextMenuFolderSeparator,
						new ToolStripMenuItem("Add Folder", null, new EventHandler(OnAddFolderRequested)),
						new ToolStripMenuItem("Settings", null)
						{
							DropDownItems = 
							{
								MaxFolderDepthToolStripTextBox,
								new ToolStripMenuItem("Dark Mode", null, new EventHandler(OnToggleDarkModeChanged))
								{
									CheckOnClick = true,
									Checked = Properties.Settings.Default.UseDarkMode
								},
								new ToolStripMenuItem("Launch on Startup", null, new EventHandler(OnToggleLaunchOnStartupChanged))
								{
									Checked = IsRegisteredToLaunchOnStartup()
								}
							}
						},
						new ToolStripMenuItem("Exit", null, new EventHandler(OnExitRequested))
					}
				}
			};

			try
			{
				List<string> folders = DeserializeFoldersSetting();

				foreach (var path in folders)
				{
					AddFolderToMainContextMenu(path);
				}
			}
			catch (Exception ex)
			{
				var result = MessageBox.Show($"There was an error reading you settings.\n{ex.ToString()}\n\nSorry.\nClearing your setting might help fix the issue. Do you want to clear your settings?", "ERROR", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
				if (result == DialogResult.Yes)
				{
					if (IsRegisteredToLaunchOnStartup())
					{
						OnToggleLaunchOnStartupChanged(null, EventArgs.Empty);
					}
					Properties.Settings.Default.Reset();
					Properties.Settings.Default.Save();
				}
			}
		}

		private void OnExitRequested(object? sender, EventArgs e)
		{
			Application.Exit();
		}

		List<string> DeserializeFoldersSetting()
		{
			return Properties.Settings.Default.Folders.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
		}

		string SerializeFoldersSetting(List<string> folders)
		{
			return string.Join(",", folders);
		}

		private void OnApplicationExit(object? sender, EventArgs e)
		{
			Cleanup(true);
		}

		private void Cleanup(bool exitingApplication = false)
		{
			if (mainSysTrayIcon != null)
			{
				RemoveIcon(mainSysTrayIcon, false);
				mainSysTrayIcon = null;
			}
		}

		private void OnAddFolderRequested(object? sender, EventArgs e)
		{
			string selectedPath;
			using (var fbd = new FolderBrowserDialog())
			{
				var result = fbd.ShowDialog();
				if (result != DialogResult.OK)
				{
					return;
				}

				selectedPath = fbd.SelectedPath;
			}

			AddFolderToSettings(selectedPath);

			AddFolderToMainContextMenu(selectedPath);
		}

		private void AddFolderToSettings(string path)
		{
			List<string> folders = DeserializeFoldersSetting();
			folders.Add(path);
			Properties.Settings.Default.Folders = SerializeFoldersSetting(folders);
			Properties.Settings.Default.Save();
		}

		private void OnOpenFileOrFolderRequested(object? sender, EventArgs e)
		{
			var menuItem = sender as ToolStripMenuItem;
			var path = menuItem?.Tag as string;
			if (string.IsNullOrEmpty(path))
			{
				return;
			}

			Process.Start("explorer", path);
		}

		private void OnRemoveFolderRequested(object? sender, EventArgs e)
		{
			var menuItem = sender as ToolStripMenuItem;
			var path = menuItem?.Tag as string;
			if (path == null)
			{
				return;
			}

			var result = MessageBox.Show($"Do you want to remove the folder \"{path}\"?", "Remove Folder", MessageBoxButtons.YesNo);

			if (result == DialogResult.No)
			{
				return;
			}

			RemoveFolderFromSettings(path);

			RemoveFolderFromMainContextMenu(path);
		}

		private void RemoveIcon(NotifyIcon icon, bool disposeIcon)
		{
			icon.Visible = false;
			if (disposeIcon)
			{
				icon.Icon?.Dispose();
			}
			icon.Icon = null;
			icon.Container?.Remove(icon);
			icon.Dispose();
		}

		private void RemoveFolderFromSettings(string path)
		{
			List<string> folders = DeserializeFoldersSetting();

			folders.Remove(path);

			Properties.Settings.Default.Folders = SerializeFoldersSetting(folders);

			Properties.Settings.Default.Save();
		}

		private ToolStripMenuItem CreateFolderMenuItem(DirectoryInfo folder, int folderDepth = 1)
		{
			ToolStripMenuItem menuItem = new ToolStripMenuItem(folder.Name, FileSystemIconHelper.GetFolderIcon());
			CreateSubMenuForFolder(folder, menuItem, folderDepth);
			return menuItem;
		}

		private void CreateSubMenuForFolder(DirectoryInfo di, ToolStripMenuItem parent, int folderDepth)
		{
			if (folderDepth >= Properties.Settings.Default.MaxFolderDepth)
			{
				parent.DropDownItems.Add(new ToolStripMenuItem("-- MAX Folder Depth Reached --")
				{
					Enabled = false
				});
				return;
			}

			bool hasContent = false;
			foreach (var folder in di.EnumerateDirectories())
			{
				parent.DropDownItems.Add(CreateFolderMenuItem(folder, folderDepth + 1));
				hasContent = true;
			}
			
			foreach (var file in di.EnumerateFiles())
			{
				parent.DropDownItems.Add(CreateFileMenuItem(file));
				hasContent = true;
			}

			if (!hasContent)
			{
				parent.DropDownItems.Add(new ToolStripMenuItem("-- EMPTY --")
				{
					Enabled = false
				});
			}
		}

		private ToolStripMenuItem CreateFileMenuItem(FileInfo file)
		{
			string fileName = file.Name;
			foreach (string? ext in Properties.Settings.Default.ExtensionsToStrip)
			{
				if (!string.IsNullOrEmpty(ext))
				{
					if (fileName.EndsWith(ext))
					{
						fileName = fileName.Substring(0, fileName.LastIndexOf(ext));
					}
				}
			}
			
			return new ToolStripMenuItem(fileName, FileSystemIconHelper.GetFileIcon(file.FullName), new EventHandler(OnOpenFileOrFolderRequested))
			{
				Tag = file.FullName
			};
		}

		private void OnToggleLaunchOnStartupChanged(object? sender, EventArgs e)
		{
			ToolStripMenuItem? tsmi = sender as ToolStripMenuItem;
			if (tsmi != null)
			{
				bool enable = !isRegisteredToLaunchOnStartup.HasValue || !isRegisteredToLaunchOnStartup.Value;
				RegisteredToLaunchOnStartup(enable);

				tsmi.Checked = isRegisteredToLaunchOnStartup.HasValue && isRegisteredToLaunchOnStartup.Value;
			}
		}

		private void RegisteredToLaunchOnStartup(bool enable)
		{
			isRegisteredToLaunchOnStartup = false;
			using (RegistryKey? key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
			{
				if (key != null && Application.ProductName != null)
				{
					if (enable)
					{
						key.SetValue(Application.ProductName, $"\"{Application.ExecutablePath}\"");
					}
					else
					{
						key.DeleteValue(Application.ProductName, false);
					}
					isRegisteredToLaunchOnStartup = enable;
				}
			}
		}

		private bool IsRegisteredToLaunchOnStartup()
		{
			if (!isRegisteredToLaunchOnStartup.HasValue)
			{
				isRegisteredToLaunchOnStartup = false;

				string? currentExecPathForStartup = null;

				using (RegistryKey? key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false))
				{
					if (key != null && Application.ProductName != null)
					{
						currentExecPathForStartup = key.GetValue(Application.ProductName) as string;
					}
				}

				if (currentExecPathForStartup == null || currentExecPathForStartup == $"\"{Application.ExecutablePath}\"")
				{
					isRegisteredToLaunchOnStartup = currentExecPathForStartup != null;
				}
				else
				{
					var result = MessageBox.Show($"{Application.ProductName} is already registered to launch on startup but with a different executable path.\n{currentExecPathForStartup}\nDo you want to replace it with the current executable?", $"{Application.ProductName} - Already a startup app", MessageBoxButtons.YesNo);

					if (result == DialogResult.Yes)
					{
						RegisteredToLaunchOnStartup(true);
					}
					else
					{
						isRegisteredToLaunchOnStartup = false;
					}
				}
			}

			return isRegisteredToLaunchOnStartup.Value;
		}

		private void OnToggleDarkModeChanged(object? sender, EventArgs e)
		{
			Properties.Settings.Default.UseDarkMode = !Properties.Settings.Default.UseDarkMode;
			Properties.Settings.Default.Save();

			Cleanup();
			Init();
		}

		private void AddFolderToMainContextMenu(string path)
		{
			if (mainSysTrayIcon?.ContextMenuStrip == null)
			{
				return;
			}

			DirectoryInfo di = new DirectoryInfo(path);
			if (!di.Exists)
			{
				return;
			}

			int index = 0;
			foreach (var item in mainSysTrayIcon.ContextMenuStrip.Items)
			{
				if (item == mainContextMenuFolderSeparator)
				{
					mainContextMenuFolderSeparator.Visible = true;
					break;
				}
				index++;
			}

			var folderMenuItem = new ToolStripMenuItem(di.Name, FileSystemIconHelper.GetFolderIcon())
			{
				Tag = path,
				DropDownItems =
				{
					new ToolStripMenuItem("Open Folder", null, new EventHandler(OnOpenFileOrFolderRequested)) { Tag = path },
					new ToolStripMenuItem("Remove Folder", null, new EventHandler(OnRemoveFolderRequested)) { Tag = path },
					new ToolStripSeparator()
				}
			};

			mainSysTrayIcon.ContextMenuStrip.Items.Insert(index, folderMenuItem);

			foreach (var folder in di.EnumerateDirectories())
			{
				folderMenuItem.DropDownItems.Add(CreateFolderMenuItem(folder));
			}
			
			foreach (var file in di.EnumerateFiles())
			{
				folderMenuItem.DropDownItems.Add(CreateFileMenuItem(file));
			}
		}

		private void RemoveFolderFromMainContextMenu(string path)
		{
			if (mainSysTrayIcon?.ContextMenuStrip != null)
			{
				foreach (var item in mainSysTrayIcon.ContextMenuStrip.Items)
				{
					if (item is ToolStripMenuItem tsmi)
					{
						if ((string?)tsmi?.Tag == path)
						{
							mainSysTrayIcon.ContextMenuStrip.Items.Remove(tsmi);
							break;
						}
					}
				}

				List<string> remainingFolders = DeserializeFoldersSetting();
				if (!remainingFolders.Any())
				{
					mainContextMenuFolderSeparator.Visible = false;
				}
			}
		}

		private void MaxFolderDepth_LostFocus(object? sender, EventArgs e)
		{
			SubmitMaxFolderDepth();
		}

		private void MaxFolderDepth_KeyDown(object? sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Enter)
			{
				if (int.TryParse(MaxFolderDepthToolStripTextBox.Text, out int depth) && depth > 0)
				{
					e.Handled = true;
					mainContextMenuFolderSeparator.PerformClick();
				}
			}
		}

		private void SubmitMaxFolderDepth()
		{
			bool hasError = true;
			if (int.TryParse(MaxFolderDepthToolStripTextBox.Text, out int depth) && depth > 0)
			{
				hasError = false;
				if (Properties.Settings.Default.MaxFolderDepth != depth)
				{
					Properties.Settings.Default.MaxFolderDepth = depth;
					Properties.Settings.Default.Save();

					Cleanup();
					Init();
					return;
				}
				else
				{
					MaxFolderDepthToolStripTextBox.Text = "";
				}
			}

			SetMaxFolderDepthBackgroundColor(hasError);
		}

		private void SetMaxFolderDepthBackgroundColor(bool hasError)
		{
			MaxFolderDepthToolStripTextBox.TextBox.BackColor = hasError ? Color.DarkRed : Color.LightGray;
		}
	}
}