namespace SystemTrayFolderShortcuts
{
	// Custom renderer for dark theme
	internal class DarkRenderer : ToolStripProfessionalRenderer
	{
		public DarkRenderer() : base(new DarkColorTable()) { }

		private readonly Color MenuBackColor = Color.FromArgb(30, 30, 30);
		private readonly Color MenuForeColor = Color.FromArgb(208, 208, 208);
		private readonly Color ArrowColor = Color.FromArgb(80, 80, 80);
		private readonly Color SeparatorColor = Color.FromArgb(60, 60, 60);

		protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
		{
			e.Graphics.FillRectangle(new SolidBrush(MenuBackColor), e.AffectedBounds);
			base.OnRenderToolStripBackground(e);
		}

		protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
		{
			e.TextColor = MenuForeColor;
			base.OnRenderItemText(e);
		}

		protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
		{
			e.ArrowColor = ArrowColor;
			base.OnRenderArrow(e);
		}

		protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
		{
			Pen forecolorpen = new Pen(new SolidBrush(SeparatorColor), 1);

			Graphics g = e.Graphics;

			Rectangle bounds = new Rectangle(Point.Empty, e.Item.Size);

			if (bounds.Width >= 4)
			{
				bounds.Inflate(-4, 0);
			}

			int startY = bounds.Height / 2;
			
			g.DrawLine(forecolorpen, bounds.Left, startY, bounds.Right - 1, startY);
			base.OnRenderSeparator(e);
		}
	}

	// Custom color table for dark theme
	internal class DarkColorTable : ProfessionalColorTable
	{
		private readonly Color MenuBackColor = Color.FromArgb(30, 30, 30);
		private readonly Color MenuBorderColor = Color.FromArgb(80, 80, 80);
		private readonly Color SelectionColor = Color.FromArgb(40, 40, 40);
		private readonly Color SeparatorColor = Color.FromArgb(60, 60, 60);
		private readonly Color CheckPressedColor = Color.FromArgb(20, 20, 20);

		public override Color CheckBackground => CheckPressedColor;
		public override Color CheckPressedBackground => SelectionColor;
		public override Color CheckSelectedBackground => MenuBackColor;
		public override Color MenuItemSelected => SelectionColor;
		public override Color ToolStripDropDownBackground => MenuBackColor;
		public override Color SeparatorDark => SeparatorColor;
		public override Color ImageMarginGradientBegin => MenuBackColor;
		public override Color ImageMarginGradientEnd => MenuBackColor;
		public override Color ImageMarginGradientMiddle => MenuBackColor;
		public override Color MenuItemSelectedGradientBegin => SelectionColor;
		public override Color MenuItemSelectedGradientEnd => SelectionColor;
		public override Color MenuItemPressedGradientBegin => SelectionColor;
	}
}

