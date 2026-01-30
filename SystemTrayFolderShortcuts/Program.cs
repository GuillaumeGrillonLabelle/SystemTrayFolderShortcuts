using System.Diagnostics;

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
		private Dictionary<string, NotifyIcon> foldersMap = [];
		private ContextMenuStrip folderMenu = new();
		private bool isFolderMenuOpened = false;
		private ToolStripTextBox MaxFolderDepthToolStripTextBox = new();

		private ToolStripSeparator mainContextMenuFolderSeparator = new ToolStripSeparator() { Visible = false };

		public MyApplicationContext()
		{
			Application.ApplicationExit += new EventHandler(OnApplicationExit);

			MaxFolderDepthToolStripTextBox.TextBox.KeyDown += MaxFolderDepth_KeyDown;
			MaxFolderDepthToolStripTextBox.TextBox.LostFocus += MaxFolderDepth_LostFocus;

			folderMenu.Opened += OnFolderMenu_Opened;
			folderMenu.Closed += OnFolderMenu_Closed;

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
								new ToolStripMenuItem("Folders are in the Main Context Menu", null, new EventHandler(OnToggleFoldersAreInTheMainContextMenuChanged))
								{
									CheckOnClick = true,
									Checked = Properties.Settings.Default.FoldersAreInTheMainContextMenu
								},
								new ToolStripMenuItem("Dark Mode", null, new EventHandler(OnToggleDarkModeChanged))
								{
									CheckOnClick = true,
									Checked = Properties.Settings.Default.UseDarkMode
								},
								new ToolStripMenuItem("Launch on Startup", null, new EventHandler(OnToggleLaunchOnStartupChanged))
								{
									CheckOnClick = true,
									Checked = Properties.Settings.Default.LaunchOnStartup,
									Enabled = false
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

				if (Properties.Settings.Default.FoldersAreInTheMainContextMenu)
				{
					foreach (var path in folders)
					{
						AddFolderToMainContextMenu(path);
					}
				}
				else
				{
					foreach (var path in folders)
					{
						InitFolderSystemTrayIcon(path);
					}
				}
			}
			catch (Exception ex)
			{
				var result = MessageBox.Show($"There was an error reading you settings.\n{ex.ToString()}\n\nSorry.\nClearing your setting might help fix the issue. Do you want to clear your settings?", "ERROR", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
				if (result == DialogResult.Yes)
				{
					if (Properties.Settings.Default.LaunchOnStartup)
					{
						OnToggleLaunchOnStartupChanged(null, EventArgs.Empty);
					}
					Properties.Settings.Default.Reset();
					Properties.Settings.Default.Save();
				}
			}

			//folderMenu.MouseLeave += OnFolderMenu_MouseLeave;
			folderMenu.AutoClose = true;
		}

		private void OnFolderMenu_Closed(object? sender, ToolStripDropDownClosedEventArgs e)
		{
			isFolderMenuOpened = false;
		}

		private void OnFolderMenu_Opened(object? sender, EventArgs e)
		{
			isFolderMenuOpened = true;
		}

		private void OnFolderMenu_MouseLeave(object? sender, EventArgs e)
		{
			folderMenu.Close();
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
			Cleanup();
		}

		private void Cleanup()
		{
			foreach (var x in foldersMap)
			{
				RemoveIcon(x.Value, true);
			}
			foldersMap.Clear();

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

			// A simple input dialog box, to get a 4 letter name
			// https://www.makeuseof.com/winforms-input-dialog-box-create-display/

			AddFolderToSettings(selectedPath);

			if (Properties.Settings.Default.FoldersAreInTheMainContextMenu)
			{
				AddFolderToMainContextMenu(selectedPath);
			}
			else
			{
				InitFolderSystemTrayIcon(selectedPath);
			}
		}

		private void AddFolderToSettings(string path)
		{
			List<string> folders = DeserializeFoldersSetting();
			folders.Add(path);
			Properties.Settings.Default.Folders = SerializeFoldersSetting(folders);
			Properties.Settings.Default.Save();
		}

		private void InitFolderSystemTrayIcon(string path)
		{
			DirectoryInfo di = new DirectoryInfo(path);
			
			NotifyIcon icon = new NotifyIcon
			{
				Text = di.Name,
				Icon = CreateTextIcon(di.Name),
				Tag = path,
				Visible = true,
				ContextMenuStrip = new ContextMenuStrip
				{
					RenderMode = ToolStripRenderMode.Professional,
					Renderer = Properties.Settings.Default.UseDarkMode ? new DarkRenderer() : null,
					Items =
					{
						new ToolStripMenuItem("Open Folder", null, new EventHandler(OnOpenFileOrFolderRequested)) { Tag = path },
						new ToolStripMenuItem("Remove Folder", null, new EventHandler(OnRemoveFolderRequested)) { Tag = path }
					}
				}
			};
			foldersMap.Add(path, icon);

			icon.Click += OnFolderIcon_Clicked;
		}

		private Icon CreateTextIcon(string text)
		{
			Font fontToUse = new Font("Tahoma", 10, FontStyle.Regular, GraphicsUnit.Pixel);
			Brush brushToUse = new SolidBrush(Color.White);
			Bitmap bitmapText = new Bitmap(16, 16);
			Graphics g = Graphics.FromImage(bitmapText);

			IntPtr hIcon;

			g.Clear(Color.Black);
			g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
			g.DrawString(text.Substring(0, 2), fontToUse, brushToUse, 0, -2);
			g.DrawString(text.Substring(2, 2), fontToUse, brushToUse, 0, 5);

			hIcon = (bitmapText.GetHicon());
			return Icon.FromHandle(hIcon);
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

			if (Properties.Settings.Default.FoldersAreInTheMainContextMenu)
			{
				RemoveFolderFromMainContextMenu(path);
			}
			else
			{
				RemoveIcon(foldersMap[path], true);

				foldersMap.Remove(path);
			}
		}

		private void RemoveIcon(NotifyIcon icon, bool destroyIcon)
		{
			icon.Visible = false;
			if (destroyIcon)
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

		private void OnFolderIcon_Clicked(object? sender, EventArgs e)
		{
			// TODO, I might want to have 2 context menu strips to fix the auto close issue.

			var icon = sender as NotifyIcon;
			var path = icon?.Tag as string;
			var mea = e as MouseEventArgs;
			if (path == null || mea == null || mea.Button != MouseButtons.Left)
			{
				return;
			}

			if (isFolderMenuOpened)
			{
				folderMenu.Close();
				return;
			}

			folderMenu.Items.Clear();
			
			DirectoryInfo di = new DirectoryInfo(path);
			if (di.Exists)
			{
				foreach (var folder in di.EnumerateDirectories())
				{
					folderMenu.Items.Add(CreateFolderMenuItem(folder));
				}

				foreach (var file in di.EnumerateFiles())
				{
					folderMenu.Items.Add(CreateFileMenuItem(file));
				}
			}
			else
			{
				folderMenu.Items.Add("The folder does not exist anymore.");
			}

			folderMenu.Show(Cursor.Position);
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
			Properties.Settings.Default.LaunchOnStartup = !Properties.Settings.Default.LaunchOnStartup;
			Properties.Settings.Default.Save();

			//TODO: Do the thing
		}

		private void OnToggleFoldersAreInTheMainContextMenuChanged(object? sender, EventArgs e)
		{
			Properties.Settings.Default.FoldersAreInTheMainContextMenu = !Properties.Settings.Default.FoldersAreInTheMainContextMenu;
			Properties.Settings.Default.Save();

			Cleanup();
			Init();
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